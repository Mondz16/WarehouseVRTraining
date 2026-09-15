using UnityEngine;
using UnityEngine.Events;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Aggregates several independent completions into one, for steps that require multiple picks
    /// (M1_06 "Complete all 3 picks"). Wire each source's completion event
    /// (e.g. every <see cref="ToteCounter"/>._onTargetReached) to <see cref="NotifyComplete"/>;
    /// once <see cref="_requiredCount"/> notifications arrive it fires <see cref="_onAllComplete"/>
    /// (latched). Wire that to the M1_06 ChallengeTutorialTrigger.OnTriggerComplete
    /// (TRIGGER_M1_MultiLine_AllPicked).
    ///
    /// Each source is expected to fire once (ToteCounter._onTargetReached latches), so a simple
    /// count is safe.
    /// </summary>
    public class MultiCounterGate : MonoBehaviour
    {
        [Tooltip("How many source completions are required before _onAllComplete fires.")]
        [SerializeField] private int _requiredCount = 3;

        [Header("Events")]
        [Tooltip("Fired once when the required number of completions is reached.")]
        [SerializeField] private UnityEvent _onAllComplete;

        [Tooltip("Fired with the running completion count each time NotifyComplete is called.")]
        [SerializeField] private IntEvent _onProgress;

        [Header("Challenge (optional, direct wiring)")]
        [Tooltip("If set, its OnTriggerComplete() is called when all completions arrive.")]
        [SerializeField] private ChallengeTutorialTrigger _challengeTrigger;

        private int _current;
        private bool _done;

        /// <summary>Completions received so far.</summary>
        public int CurrentCount => _current;

        /// <summary>Register one source completion. Call from each pick's completion event.</summary>
        public void NotifyComplete()
        {
            if (_done) return;

            _current++;
            _onProgress?.Invoke(_current);

            if (_current >= _requiredCount)
            {
                _done = true;
                
                if (_challengeTrigger != null) _challengeTrigger.OnTriggerComplete();
_onAllComplete?.Invoke();
            }
        }

        /// <summary>Resets the gate (e.g. on step reset).</summary>
        public void ResetGate()
        {
            _current = 0;
            _done = false;
            _onProgress?.Invoke(0);
        }
    }
}
