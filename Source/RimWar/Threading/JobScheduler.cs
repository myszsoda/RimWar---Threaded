using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using RimWar.Planet;
using Verse;

namespace RimWar.Threading
{
    /// <summary>
    /// A unit of RimWar work split into a pure part and a game-thread part.
    ///
    /// Compute() runs on the RimWar worker (or inline when threading is disabled). It may read only the snapshot and
    /// this job's own fields, and may write only this job's own fields. It must not touch Find.*, Verse.Rand, Log,
    /// GetComponent, or any property of a live WorldObject/Faction/RimWarData/comp.
    ///
    /// Commit() runs on the game thread at a fixed tick. It must re-validate everything Compute() decided against live
    /// state (destroyed/spawned, faction, points, heat gates) and silently do nothing if the decision is stale.
    /// Nothing may be mutated before Commit(), so a job that is dropped (save/load, new game) leaves no trace.
    /// </summary>
    public abstract class RimWarJob
    {
        internal int epoch;
        internal int commitTick;
        internal int seed;
        internal WorldSnapshot snapshot;
        internal Exception error;
        internal volatile bool computed;

        protected internal abstract void Compute(WorldSnapshot snapshot, System.Random rng);

        protected internal abstract void Commit();
    }

    /// <summary>
    /// Runs RimWarJobs: schedule on the game thread, compute on one background worker, commit on the game thread
    /// exactly CommitDelayTicks later in FIFO order. The snapshot, seed and commit tick do not depend on the threading
    /// setting, so threading on and off produce the same simulation.
    /// </summary>
    public static class JobScheduler
    {
        public const int CommitDelayTicks = 30;
        public const int SnapshotMaxAgeTicks = 15;
        private const int WorkerStallTimeoutMs = 10000;

        private static readonly List<RimWarJob> inFlight = new List<RimWarJob>();
        private static readonly List<RimWarJob> toDispatch = new List<RimWarJob>();
        private static readonly ConcurrentQueue<RimWarJob> workQueue = new ConcurrentQueue<RimWarJob>();
        private static readonly SemaphoreSlim workSignal = new SemaphoreSlim(0);
        private static readonly object computedSync = new object();
        private static Thread worker;
        private static int epoch;
        private static int lastCommitTick = -1;
        private static int lastDispatchTick = -1;

        public static int PendingCount => inFlight.Count;
        public static int Scheduled { get; private set; }
        public static int Committed { get; private set; }
        public static int Failed { get; private set; }
        public static int Discarded { get; private set; }
        public static int Waits { get; private set; }
        public static long WaitMilliseconds { get; private set; }

        /// <summary>Game thread. The job is computed from a snapshot of this tick and committed CommitDelayTicks later.</summary>
        public static void Schedule(RimWarJob job)
        {
            MainThread.Verify();
            job.epoch = epoch;
            job.seed = Rand.Int;
            job.commitTick = GenTicks.TicksGame + CommitDelayTicks;
            toDispatch.Add(job);
            inFlight.Add(job);
            Scheduled++;
        }

        /// <summary>Game thread, start of WorldComponent_PowerTracker.WorldComponentTick.</summary>
        public static void CommitDue()
        {
            MainThread.Verify();
            int now = GenTicks.TicksGame;
            if (now == lastCommitTick)
            {
                return;
            }
            lastCommitTick = now;

            int due = 0;
            while (due < inFlight.Count && inFlight[due].commitTick <= now)
            {
                due++;
            }
            if (due == 0)
            {
                return;
            }
            if (toDispatch.Count > 0 && toDispatch[0].commitTick <= now)
            {
                // A tick that threw before Dispatch() left due jobs uncomputed; compute them from a fresh snapshot now.
                DispatchPending(WorldUtility.Get_WCPT());
            }
            for (int i = 0; i < due; i++)
            {
                RimWarJob job = inFlight[i];
                if (job.epoch != epoch)
                {
                    Discarded++;
                    continue;
                }
                if (!WaitForCompute(job))
                {
                    Failed++;
                    Log.Error("RimWar: worker did not finish " + job.GetType().Name + " within " + WorkerStallTimeoutMs + " ms; job dropped.");
                    continue;
                }
                if (job.error != null)
                {
                    Failed++;
                    Log.Error("RimWar: job " + job.GetType().Name + " failed in Compute: " + job.error);
                    continue;
                }
                try
                {
                    job.Commit();
                    Committed++;
                }
                catch (Exception e)
                {
                    Failed++;
                    Log.Error("RimWar: job " + job.GetType().Name + " failed in Commit: " + e);
                }
            }
            inFlight.RemoveRange(0, due);
        }

        /// <summary>Game thread, end of WorldComponent_PowerTracker.WorldComponentTick.</summary>
        public static void Dispatch(WorldComponent_PowerTracker wcpt)
        {
            MainThread.Verify();
            int now = GenTicks.TicksGame;
            if (toDispatch.Count == 0 || now == lastDispatchTick)
            {
                return;
            }
            lastDispatchTick = now;
            DispatchPending(wcpt);
        }

        private static void DispatchPending(WorldComponent_PowerTracker wcpt)
        {
            WorldSnapshot snap = WorldSnapshotStore.Acquire(SnapshotMaxAgeTicks, epoch, wcpt);
            bool threaded = Options.Settings.Instance.threadingEnabled;
            for (int i = 0; i < toDispatch.Count; i++)
            {
                RimWarJob job = toDispatch[i];
                snap.AddRef();
                job.snapshot = snap;
                if (threaded)
                {
                    EnsureWorker();
                    workQueue.Enqueue(job);
                    workSignal.Release();
                }
                else
                {
                    RunCompute(job);
                }
            }
            toDispatch.Clear();
            snap.Release(); // the reference taken by Acquire
        }

        /// <summary>Game thread. Drops every pending job; called when a game is loaded or started.</summary>
        public static void ResetForNewGame()
        {
            MainThread.Verify();
            epoch++;
            Discarded += inFlight.Count;
            inFlight.Clear();
            toDispatch.Clear();
            lastCommitTick = -1;
            lastDispatchTick = -1;
            WorldSnapshotStore.Reset();
        }

        public static string Stats()
        {
            return string.Format("RimWar jobs: scheduled {0}, committed {1}, failed {2}, discarded {3}, pending {4}, waits {5} ({6} ms), snapshots {7}, worker {8}",
                Scheduled, Committed, Failed, Discarded, PendingCount, Waits, WaitMilliseconds, WorldSnapshotStore.Captures,
                worker != null && worker.IsAlive ? "running" : "idle");
        }

        private static bool WaitForCompute(RimWarJob job)
        {
            if (job.computed)
            {
                return true;
            }
            Waits++;
            Stopwatch stopwatch = Stopwatch.StartNew();
            lock (computedSync)
            {
                while (!job.computed)
                {
                    if (worker == null || !worker.IsAlive)
                    {
                        break;
                    }
                    if (stopwatch.ElapsedMilliseconds > WorkerStallTimeoutMs)
                    {
                        WaitMilliseconds += stopwatch.ElapsedMilliseconds;
                        return false;
                    }
                    Monitor.Wait(computedSync, 100);
                }
            }
            WaitMilliseconds += stopwatch.ElapsedMilliseconds;
            if (!job.computed)
            {
                // The worker is gone; the job is still queued with its snapshot. Compute it here - same result.
                RimWarJob queued;
                while (workQueue.TryDequeue(out queued))
                {
                    RunCompute(queued);
                }
            }
            return job.computed;
        }

        private static void EnsureWorker()
        {
            if (worker != null && worker.IsAlive)
            {
                return;
            }
            worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "RimWar worker"
            };
            worker.Start();
        }

        private static void WorkerLoop()
        {
            MainThread.MarkCurrentThreadAsWorker();
            while (true)
            {
                workSignal.Wait();
                RimWarJob job;
                while (workQueue.TryDequeue(out job))
                {
                    RunCompute(job);
                }
            }
        }

        private static void RunCompute(RimWarJob job)
        {
            WorldSnapshot snap = job.snapshot;
            try
            {
                job.Compute(snap, new System.Random(job.seed));
            }
            catch (Exception e)
            {
                job.error = e;
            }
            finally
            {
                job.snapshot = null;
                if (snap != null)
                {
                    snap.Release();
                }
                job.computed = true;
                lock (computedSync)
                {
                    Monitor.PulseAll(computedSync);
                }
            }
        }
    }
}
