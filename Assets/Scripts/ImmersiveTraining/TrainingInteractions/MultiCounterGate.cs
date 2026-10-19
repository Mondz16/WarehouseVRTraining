using UnityEngine;
using UnityEngine.Events;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Aggregates several independent completions into one, for steps that require multiple picks
    /// (M1_06 "Complete all 3 picks"). Each <see cref="ToteCounter"/> with this gate reports whether its
    /// line is met right now through <see cref="SetSourceMet"/> (a line whose items are taken back out
    /// stops counting); sources wired through a UnityEvent can call <see cref="NotifyComplete"/> instead.
    /// Once <see cref="_requiredCount"/> completions are in and the challenge accepts them it fires
    /// <see cref="_onAllComplete"/> (latched). If the picks were finished before the step opened, it
    /// completes when the step's challenge arms.
    /// </summary>
    public class MultiCounterGate : MonoBehaviour
    {
        [Tooltip("How many source completions are required before _onAllComplete fires.")]
        [SerializeField] private int _requiredCount = 3;

        [Header("Events")]
        [Tooltip("Fired once when the required number of completions counts.")]
        [SerializeField] private UnityEvent _onAllComplete;

        [Tooltip("Fired with the running completion count each time it changes.")]
        [SerializeField] private IntEvent _onProgress;

        [Header("Challenge (optional, direct wiring)")]
        [Tooltip("If set, its TryComplete() is called when all completions are in while the step is active.")]
        [SerializeField] private ChallengeTutorialTrigger _challengeTrigger;

        private readonly MultiSourceGate _sources = new MultiSourceGate();
        private readonly ArmedLatch _latch = new ArmedLatch();

        /// <summary>Completions currently counted.</summary>
        public int CurrentCount => _sources.Count;

        private void OnEnable()
        {
            if (_challengeTrigger != null) _challengeTrigger.Armed += HandleArmed;
        }

        private void OnDisable()
        {
            if (_challengeTrigger != null) _challengeTrigger.Armed -= HandleArmed;
        }

        /// <summary>Registers one source completion (for sources wired through a UnityEvent).</summary>
        public void NotifyComplete()
        {
            if (_latch.IsLatched) return;

            _sources.Notify();
            HandleChanged();
        }

        /// <summary>Reports whether <paramref name="source"/> is currently met.</summary>
        public void SetSourceMet(Object source, bool met)
        {
            if (_latch.IsLatched) return;

            if (_sources.SetSourceMet(source, met))
                HandleChanged();
        }

        // The picks may already be complete when the step opens.
        private void HandleArmed(ChallengeTutorialTrigger trigger) => TryFinish();

        private void HandleChanged()
        {
            _onProgress?.Invoke(_sources.Count);
            TryFinish();
        }

        private void TryFinish()
        {
            if (_latch.Offer(_sources.Count >= _requiredCount, TryCompleteChallenge))
                _onAllComplete?.Invoke();
        }

        private bool TryCompleteChallenge() => _challengeTrigger == null || _challengeTrigger.TryComplete();

        /// <summary>Resets the gate (e.g. on step reset).</summary>
        public void ResetGate()
        {
            _sources.Reset();
            _latch.Reset();
            _onProgress?.Invoke(0);
        }
    }
}
