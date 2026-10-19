using UnityEngine;
using UnityEngine.Events;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Drives the mis-pick recovery scenario (M1_07): a wrong item starts inside the tote and
    /// must be removed, then the correct item added — in that order.
    ///
    /// Attach to the tote GameObject (shares its trigger Collider with any <see cref="ToteCounter"/>s).
    /// Completes <see cref="_removeTrigger"/> once the <see cref="_wrongSku"/> item that was in the tote
    /// is out of it, then <see cref="_replaceTrigger"/> once a <see cref="_correctSku"/> item is in it.
    /// Each only counts once its challenge accepts it, so acting before M1_07 opens doesn't latch
    /// anything; the state is re-checked when each challenge arms. Direct ChallengeTutorialTrigger
    /// references are used because MCP cannot wire UnityEvents (same pattern as <see cref="ToteCounter"/>).
    ///
    /// Requires a Collider with Is Trigger enabled on the same GameObject, large enough that the
    /// pre-placed wrong item starts inside it.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class MisPickRecovery : MonoBehaviour
    {
        [Tooltip("SKU of the mis-picked item that must be removed from the tote.")]
        [SerializeField] private string _wrongSku = "SKU-5099";

        [Tooltip("SKU of the correct item that must be added after the mis-pick is removed.")]
        [SerializeField] private string _correctSku = "SKU-5005";

        [Tooltip("If true, the correct item only counts once the wrong item has been removed.")]
        [SerializeField] private bool _requireRemoveBeforeReplace = true;

        [Header("Challenge (optional, direct wiring)")]
        [Tooltip("Its TryComplete() is called once the mis-picked item is out of the tote.")]
        [SerializeField] private ChallengeTutorialTrigger _removeTrigger;

        [Tooltip("Its TryComplete() is called once the correct item is in the tote.")]
        [SerializeField] private ChallengeTutorialTrigger _replaceTrigger;

        [Header("Events")]
        [SerializeField] private UnityEvent _onMisPickRemoved;
        [SerializeField] private UnityEvent _onCorrectPlaced;

        private readonly MisPickTracker _tracker = new MisPickTracker();
        private readonly ArmedLatch _removed = new ArmedLatch();
        private readonly ArmedLatch _replaced = new ArmedLatch();

        /// <summary>True once the removal has counted.</summary>
        public bool Removed => _removed.IsLatched;

        /// <summary>True once the replacement has counted.</summary>
        public bool Replaced => _replaced.IsLatched;

        private void Awake()
        {
            _tracker.WrongSku = _wrongSku;
            _tracker.CorrectSku = _correctSku;
        }

        private void OnEnable()
        {
            if (_removeTrigger != null) _removeTrigger.Armed += HandleRemoveArmed;
            if (_replaceTrigger != null) _replaceTrigger.Armed += HandleReplaceArmed;
        }

        private void OnDisable()
        {
            if (_removeTrigger != null) _removeTrigger.Armed -= HandleRemoveArmed;
            if (_replaceTrigger != null) _replaceTrigger.Armed -= HandleReplaceArmed;
        }

        private void OnTriggerEnter(Collider other)
        {
            SkuItem item = other.GetComponentInParent<SkuItem>();
            if (item == null) return;

            _tracker.Enter(item.gameObject.GetInstanceID(), item.Sku);
            Evaluate();
        }

        private void OnTriggerExit(Collider other)
        {
            SkuItem item = other.GetComponentInParent<SkuItem>();
            if (item == null) return;

            _tracker.Exit(item.gameObject.GetInstanceID());
            Evaluate();
        }

        private void HandleRemoveArmed(ChallengeTutorialTrigger trigger)
        {
            if (!_tracker.WrongSeen)
                Debug.LogWarning($"#{GetType().Name}# No {_wrongSku} item has been inside the tote's trigger, so " +
                                 $"'{trigger.TriggerName}' cannot complete. Place the mis-pick item inside the trigger collider.", this);
            Evaluate();
        }

        private void HandleReplaceArmed(ChallengeTutorialTrigger trigger) => Evaluate();

        private void Evaluate()
        {
            if (_removed.Offer(_tracker.WrongItemRemoved, () => TryComplete(_removeTrigger)))
                _onMisPickRemoved?.Invoke();

            bool replaceAllowed = !_requireRemoveBeforeReplace || _removed.IsLatched;
            if (_replaced.Offer(replaceAllowed && _tracker.CorrectItemInside, () => TryComplete(_replaceTrigger)))
                _onCorrectPlaced?.Invoke();
        }

        private static bool TryComplete(ChallengeTutorialTrigger trigger) => trigger == null || trigger.TryComplete();

        /// <summary>Re-arms the scenario (e.g. on step reset).</summary>
        public void ResetRecovery()
        {
            _removed.Reset();
            _replaced.Reset();
        }
    }
}
