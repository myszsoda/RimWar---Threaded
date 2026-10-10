using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace RimWar.Threading
{
    /// <summary>
    /// Thread-ownership guard for RimWar.
    ///
    /// All live game state (world objects, factions, RimWarData, settlement comps, Verse.Rand, letters, ...)
    /// is owned by the game-logic thread. RimWar's own worker thread may only read an immutable
    /// <see cref="WorldSnapshot"/> and write the fields of the job it is computing.
    ///
    /// RimWorld itself runs some game logic on its LongEventHandler loader thread (loading saves,
    /// generating worlds), so the guard does not compare against Unity's main thread id. It rejects
    /// exactly the threads RimWar created.
    /// </summary>
    public static class MainThread
    {
        [ThreadStatic]
        private static bool isRimWarWorker;

        /// <summary>True when the calling thread is a RimWar worker thread.</summary>
        public static bool IsWorker => isRimWarWorker;

        /// <summary>Marks the calling thread as a RimWar worker. Called once by the worker loop.</summary>
        internal static void MarkCurrentThreadAsWorker()
        {
            isRimWarWorker = true;
        }

        /// <summary>
        /// Throws when called from a RimWar worker thread. Place at every gate to live game state.
        /// The exception fails the job; the scheduler reports it on the game thread.
        /// </summary>
        public static void Verify([CallerMemberName] string where = null)
        {
            if (isRimWarWorker)
            {
                throw new InvalidOperationException("RimWar: game-thread-only access from worker thread: " + where);
            }
        }
    }
}
