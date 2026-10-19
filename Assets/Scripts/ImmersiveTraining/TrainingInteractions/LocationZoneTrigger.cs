using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Trigger volume that completes a challenge when an object carrying a matching
    /// <see cref="TriggerIdentity"/> is inside it — used to detect the trainee arriving at a
    /// warehouse location (navigation challenge, e.g. M1_03).
    ///
    /// Wire <see cref="_challengeTrigger"/> to the step's ChallengeTutorialTrigger. The direct
    /// reference exists because MCP cannot wire UnityEvents; <see cref="_onReached"/> is still
    /// provided for Inspector-driven hookups.
    ///
    /// Arrival only counts once the challenge accepts it: walking through the zone before the step
    /// is active is ignored, and if the trainee is already standing in the zone when the step opens
    /// it counts straight away.
    ///
    /// Requires a Collider with Is Trigger enabled on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LocationZoneTrigger : MonoBehaviour
    {
        [Tooltip("TriggerIdentity.Id that is allowed to complete this zone. Leave empty to accept any identity.")]
        [SerializeField] private string _requiredId = "Player";

        [Header("Challenge (optional, direct wiring)")]
        [Tooltip("If set, its TryComplete() is called when a matching object is in the zone while the step is active.")]
        [SerializeField] private ChallengeTutorialTrigger _challengeTrigger;

        [Header("Events")]
        [Tooltip("Fired once when the arrival counts. Latches - it will not fire again.")]
        [SerializeField] private UnityEvent _onReached;

        private readonly HashSet<Collider> _matchingInside = new HashSet<Collider>();
        private readonly ArmedLatch _latch = new ArmedLatch();

        /// <summary>True once the arrival has counted.</summary>
        public bool Reached => _latch.IsLatched;

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
            if (!Matches(other)) return;

            _matchingInside.Add(other);
            OfferArrival();
        }

        private void OnTriggerExit(Collider other)
        {
            _matchingInside.Remove(other);
        }

        // The trainee may already be standing in the zone when the step opens.
        private void HandleArmed(ChallengeTutorialTrigger trigger) => OfferArrival();

        private void OfferArrival()
        {
            // Colliders disabled or destroyed inside the zone never raise OnTriggerExit.
            _matchingInside.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);

            if (_latch.Offer(_matchingInside.Count > 0, TryCompleteChallenge))
                _onReached?.Invoke();
        }

        private bool Matches(Collider other)
        {
            TriggerIdentity identity = other.GetComponentInParent<TriggerIdentity>();
            if (identity == null) return false;

            return string.IsNullOrEmpty(_requiredId) || identity.Id == _requiredId;
        }

        private bool TryCompleteChallenge() => _challengeTrigger == null || _challengeTrigger.TryComplete();

        /// <summary>Re-arms the zone so it can fire again (e.g. on step reset).</summary>
        public void ResetZone() => _latch.Reset();
    }
}
