using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Verse;
using static RimWar.Planet.WorldComponent_PowerTracker;


namespace RimWar.RocketTools
{
    public class RocketTasker<T>
    {
        private List<RocketTask<T>> tasks = new List<RocketTask<T>>();
        private List<RocketTask<T>> finishedTasks = new List<RocketTask<T>>();

        public class RocketTask<T>
        {
            private Func<T> threaded;
            private Action<T> nonThreaded;

            private T context;
            public static object locker = new object();

            private string error;

            private ThreadStart threadStart;
            private Thread thread;

            private bool finishedOffThread = false;
            private bool failed = false;
            private bool started = false;
            private bool done = false;

            private static int actionCounter = 0;

            public bool IsAlive => thread.IsAlive;
            public bool Started => started;
            public bool Finished => finishedOffThread || failed;
            public bool Failed => failed;

            public bool ShouldBeRemoved => done;
            public T Context => context;

            public RocketTask(Func<T> threaded, Action<T> nonThreaded)
            {
                this.threaded = threaded;
                this.nonThreaded = nonThreaded;
                this.threadStart = new ThreadStart(RunOffMainThread);
                this.thread = new Thread(threadStart);
            }

            public bool TryStartBackgroundTask()
            {
                if (actionCounter <= 4 && !started)
                {
                    actionCounter += 1;
                    this.started = true;
                    this.thread.Start();
                }
                return this.started;
            }

            public bool TryFinalize()
            {
                if (this.Finished)
                {
                    this.done = true;
                    this.RunOnMainThread();
                }
                return this.done;
            }

            public void Interrupt(bool recycle = true)
            {
                thread?.Interrupt();
                actionCounter -= 1;
                if (recycle)
                {
                    this.started = false;
                    this.failed = false;
                    this.threadStart = new ThreadStart(RunOffMainThread);
                    this.thread = new Thread(threadStart);
                }
            }

            private void RunOffMainThread()
            {
                try
                {
                    context = threaded.Invoke();
                }
                catch (Exception er)
                {
                    this.error = string.Format("RIMWAR: [OFFTHREAD] error in {0} at {1}", er.Message, er.StackTrace);
                    this.failed = true;
                }
                finally
                {
                    this.finishedOffThread = true;
                    actionCounter -= 1;
                }
            }

            private void RunOnMainThread()
            {
                try
                {
                    this.done = true;
                    if (!failed)
                        this.nonThreaded.Invoke(context);
                    else
                        Log.Error(error);
                }
                catch (Exception er)
                {
                    error = string.Format("RIMWAR: [MAINTHREAD] error in {0} at {1}", er.Message, er.StackTrace);
                }
                finally
                {

                }
            }
        }

        public void Tick()
        {
            var remaining = new List<RocketTask<T>>();
            var startedCounter = 0;
            var finishedCounter = 0;

            for (int i = 0; i < this.tasks.Count; i++)
            {
                var task = this.tasks[i];
                if (!task.Started && task.TryStartBackgroundTask())
                {
                    finishedTasks.Add(task);
                    startedCounter++;
                }
                else
                {
                    remaining.Add(task);
                }
            }

            this.tasks = remaining;

            if (Prefs.LogVerbose && startedCounter > 0)
                Log.Message(string.Format("RIMWAR: started {0} tasks", startedCounter));

            remaining = new List<RocketTask<T>>();
            for (int i = 0; i < finishedTasks.Count; i++)
            {
                var task = finishedTasks[i];
                if (task.TryFinalize())
                {
                    finishedCounter++;
                }
                else
                {
                    remaining.Add(task);
                }
            }

            this.finishedTasks = remaining;
            if (Prefs.LogVerbose && finishedCounter > 0) Log.Message(
                string.Format("RIMWAR: finished {0} tasks", finishedCounter));
        }

        public void Register(Func<T> threaded, Action<T> nonThreaded)
        {
            var task = new RocketTask<T>(threaded, nonThreaded);
            tasks.Add(task);
            if (Prefs.LogVerbose) Log.Message(
                string.Format("RIMWAR: Added new task"));
        }

        public void Await(bool forceKill = false)
        {
            bool ShouldWait()
            {
                foreach (var task in finishedTasks)
                    if (task.IsAlive)
                        return true;
                return false;
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (ShouldWait() && stopwatch.ElapsedMilliseconds < 15)
            {
                Thread.Sleep(1);
            }
            stopwatch.Stop();
            if (forceKill && stopwatch.ElapsedMilliseconds >= 15)
            {
                foreach (var task in finishedTasks)
                    if (task.IsAlive)
                    {
                        task.Interrupt(recycle: true);
                        Log.Warning("RIMWAR: interrupted excution.");
                    }
            }

        }
    }
}

