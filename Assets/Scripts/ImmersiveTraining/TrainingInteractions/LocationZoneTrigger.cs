using UnityEngine;
using UnityEngine.Events;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Trigger volume that fires once when an object carrying a matching
    /// <see cref="TriggerIdentity"/> enters it — used to detect the trainee
    /// arriving at a warehouse location (navigation challenge, e.g. M1_03).
    ///
    /// Wire <see cref="_challengeTrigger"/> to the step's ChallengeTutorialTrigger
    /// (its OnTriggerComplete() is called on arrival). The direct reference exists
    /// because MCP cannot wire UnityEvents; <see cref="_onReached"/> is still
    /// provided for Inspector-driven hookups.
    ///
    /// Requires a Collider with Is Trigger enabled on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LocationZoneTrigger : MonoBehaviour
    {
        [Tooltip("TriggerIdentity.Id that is allowed to complete this zone. Leave empty to accept any identity.")]
        [SerializeField] private string _requiredId = "Player";

        [Header("Challenge (optional, direct wiring)")]
        [Tooltip("If set, its OnTriggerComplete() is called when a matching object first enters the zone.")]
        [SerializeField] private ChallengeTutorialTrigger _challengeTrigger;

        [Header("Events")]
        [Tooltip("Fired once when a matching object first enters the zone. Latches - it will not fire again.")]
        [SerializeField] private UnityEvent _onReached;

        private bool _reached;

        /// <summary>True once the zone has been entered by a matching object.</summary>
        public bool Reached => _reached;

        private void OnTriggerEnter(Collider other)
        {
            if (_reached) return;

            TriggerIdentity identity = other.GetComponentInParent<TriggerIdentity>();
            if (identity == null) return;

            bool matches = string.IsNullOrEmpty(_requiredId) || identity.Id == _requiredId;
            if (!matches) return;

            _reached = true;

            if (_challengeTrigger != null) _challengeTrigger.OnTriggerComplete();
            _onReached?.Invoke();
        }

        /// <summary>Re-arms the zone so it can fire again (e.g. on step reset).</summary>
        public void ResetZone()
        {
            _reached = false;
        }
    }
}
