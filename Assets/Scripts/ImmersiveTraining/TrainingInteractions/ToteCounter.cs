using ImmersiveTraining.Management;
using UnityEngine;
using UnityEngine.Events;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Trigger volume that counts pickable <see cref="SkuItem"/>s placed into a tote / bin.
    /// Fires <see cref="_onTargetReached"/> once <see cref="_requiredCount"/> items carrying
    /// <see cref="_requiredSku"/> are inside. Wire that event in the Inspector to a
    /// ChallengeTutorialTrigger.OnTriggerComplete (or any step-completion hook) so this stays
    /// decoupled from the tutorial framework.
    ///
    /// With <see cref="_challengeTrigger"/> set, the target only counts once that challenge accepts it:
    /// items placed before the step is active don't latch anything, and the tote is re-checked when the
    /// step opens. With <see cref="_gate"/> set, the counter reports whether its target is met right now,
    /// so items taken back out stop counting toward the multi-line order.
    ///
    /// Requires a Collider with Is Trigger enabled on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ToteCounter : MonoBehaviour
    {
        [Tooltip("SKU the tote expects. Leave empty to accept any SKU.")]
        [SerializeField] private string _requiredSku;

        [Tooltip("How many matching items complete the pick.")]
        [SerializeField] private int _requiredCount = 1;

        [Tooltip("If true, the pick only counts while no other items are in the tote (e.g. M1_05: exactly the listed item, no decoys). SKUs in Ignored Skus never block.")]
        [SerializeField] private bool _requireNoOtherItems;

        [Tooltip("SKUs that never count as other items (e.g. the M1_07 mis-pick scenario items that live in the tote).")]
        [SerializeField] private string[] _ignoredSkus = new string[0];

        [Header("Events")]
        [Tooltip("Fired once when the target counts. Latches - it will not fire again if items are later removed.")]
        [SerializeField] private UnityEvent _onTargetReached;

        [Tooltip("Fired with the current matching count each time it changes.")]
        [SerializeField] private IntEvent _onCountChanged;

        [Tooltip("Fired with the offending item when a non-matching, non-ignored SKU is placed (mis-pick, M1_07).")]
        [SerializeField] private UnityObjectEvent _onWrongItemAdded;

        [Header("Challenge (optional, direct wiring)")]
        [Tooltip("If set, its TryComplete() is called when the target is met while the step is active.")]
        [SerializeField] private ChallengeTutorialTrigger _challengeTrigger;

        [Tooltip("If set, the counter reports whether its target is currently met (multi-line aggregation, M1_06).")]
        [SerializeField] private MultiCounterGate _gate;

        private readonly ToteTally _tally = new ToteTally();
        private readonly ArmedLatch _latch = new ArmedLatch();

        /// <summary>Number of matching items currently counted inside the tote.</summary>
        public int CurrentCount => _tally.MatchingCount;

        private void Awake()
        {
            _tally.RequiredSku = _requiredSku;
            _tally.RequiredCount = _requiredCount;
            _tally.RequireNoOtherItems = _requireNoOtherItems;
            _tally.SetIgnoredSkus(_ignoredSkus);
        }

        private void OnEnable()
        {
            if (_challengeTrigger != null) _challengeTrigger.Armed += HandleArmed;
        }

        private void OnDisable()
        {
            if (_challengeTrigger != null) _challengeTrigger.Armed -= HandleArmed;
        }

        private void OnTriggerEnter(Collider other)
        {
            SkuItem item = other.GetComponentInParent<SkuItem>();
            if (item == null) return;

            int id = item.gameObject.GetInstanceID();
            if (_tally.Contains(id)) return;

            _tally.Enter(id, item.Sku);
            if (_tally.Matches(item.Sku))
                _onCountChanged?.Invoke(_tally.MatchingCount);
            else if (!_tally.IsIgnored(item.Sku))
                _onWrongItemAdded?.Invoke(item.gameObject);

            Evaluate();
        }

        private void OnTriggerExit(Collider other)
        {
            SkuItem item = other.GetComponentInParent<SkuItem>();
            if (item == null) return;

            if (!_tally.Exit(item.gameObject.GetInstanceID(), out string sku)) return;

            if (_tally.Matches(sku))
                _onCountChanged?.Invoke(_tally.MatchingCount);

            Evaluate();
        }

        // Items may already be in the tote when the step opens.
        private void HandleArmed(ChallengeTutorialTrigger trigger) => Evaluate();

        private void Evaluate()
        {
            bool met = _tally.IsTargetMet;
            if (_gate != null) _gate.SetSourceMet(this, met);

            if (_latch.Offer(met, TryCompleteChallenge))
                _onTargetReached?.Invoke();
        }

        private bool TryCompleteChallenge() => _challengeTrigger == null || _challengeTrigger.TryComplete();

        /// <summary>Clears the counted items and re-arms the target event (e.g. on step reset).</summary>
        public void ResetTote()
        {
            _tally.Clear();
            _latch.Reset();
            if (_gate != null) _gate.SetSourceMet(this, false);
            _onCountChanged?.Invoke(0);
        }
    }

    [System.Serializable]
    public class IntEvent : UnityEvent<int> { }
}
