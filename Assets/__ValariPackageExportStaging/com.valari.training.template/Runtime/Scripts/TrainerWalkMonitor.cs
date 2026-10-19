using System;

namespace Valari.FoundryTraining.Controller
{
    public enum TrainerWalkEnd { None, Arrived, Stalled, TimedOut }

    /// <summary>
    /// Decides when the trainer's walk (<see cref="TrainerController.MoveTrainer"/>) is over. Plain C# (no UnityEngine) so
    /// it can be unit-tested outside Unity: dotnet run --project Tests/TrainerLogic
    ///
    /// The walk used to loop until the NavMeshAgent was inside its stopping distance. A trainer that could not get there
    /// (pushed around, blocked by the player, hovering just outside the distance) walked forever, and every tutorial
    /// modal waits for the walk to finish - the whole training stalled. A walk now also ends when it stops making
    /// progress, or after a timeout.
    /// </summary>
    public sealed class TrainerWalkMonitor
    {
        /// <summary>How far inside / beyond the stopping distance still counts as arrived (metres).</summary>
        public const float ArrivalTolerance = 0.05f;

        private float _elapsed;
        private float _stalled;
        private float _closest = float.NaN;

        public TrainerWalkMonitor(float stallSeconds = 2f, float minProgress = 0.05f, float timeoutSeconds = 20f)
        {
            StallSeconds = stallSeconds;
            MinProgress = minProgress;
            TimeoutSeconds = timeoutSeconds;
        }

        public float StallSeconds { get; }
        public float MinProgress { get; }
        public float TimeoutSeconds { get; }
        public TrainerWalkEnd End { get; private set; }

        /// <summary>
        /// Advances the walk by <paramref name="deltaTime"/> with the agent's current remaining distance (may be
        /// infinite / NaN while unknown). Returns true once the walk is over; <see cref="End"/> says why.
        /// </summary>
        public bool Update(float remainingDistance, float stoppingDistance, bool pathPending, float deltaTime)
        {
            if (End != TrainerWalkEnd.None) return true;

            _elapsed += Math.Max(0f, deltaTime);
            if (!pathPending && remainingDistance <= stoppingDistance + ArrivalTolerance) return Finish(TrainerWalkEnd.Arrived);
            if (_elapsed >= TimeoutSeconds) return Finish(TrainerWalkEnd.TimedOut);
            if (pathPending) return false; // still planning: only the timeout applies

            // Progress = getting closer than ever before by at least MinProgress. The first known distance is only a
            // baseline, so jitter around one spot never counts as progress.
            float previousClosest = _closest;
            if (IsFinite(remainingDistance) && (!IsFinite(_closest) || remainingDistance < _closest - MinProgress))
            {
                _closest = remainingDistance;
                if (IsFinite(previousClosest))
                {
                    _stalled = 0f;
                    return false;
                }
            }

            _stalled += Math.Max(0f, deltaTime);
            return _stalled >= StallSeconds && Finish(TrainerWalkEnd.Stalled);
        }

        private bool Finish(TrainerWalkEnd end)
        {
            End = end;
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
