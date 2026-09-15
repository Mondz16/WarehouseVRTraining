using UnityEngine;
using UnityEngine.Events;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Drives the mis-pick recovery scenario (M1_07): a wrong item starts inside the tote and
    /// must be removed, then the correct item added — in that order.
    ///
    /// Attach to the tote GameObject (shares its trigger Collider with any <see cref="ToteCounter"/>s).
    /// Fires <see cref="_removeTrigger"/> when an item whose SKU is <see cref="_wrongSku"/> leaves the
    /// tote, then fires <see cref="_replaceTrigger"/> when an item whose SKU is <see cref="_correctSku"/>
    /// enters — but only after the wrong item has been removed. Both latch. Direct
    /// ChallengeTutorialTrigger references are used because MCP cannot wire UnityEvents (same pattern
    /// as <see cref="ToteCounter"/>).
    ///
    /// Requires a Collider with Is Trigger enabled on the same GameObject.
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
        [Tooltip("Its OnTriggerComplete() is called when the mis-picked item leaves the tote.")]
        [SerializeField] private ChallengeTutorialTrigger _removeTrigger;

        [Tooltip("Its OnTriggerComplete() is called when the correct item is placed in the tote.")]
        [SerializeField] private ChallengeTutorialTrigger _replaceTrigger;

        [Header("Events")]
        [SerializeField] private UnityEvent _onMisPickRemoved;
        [SerializeField] private UnityEvent _onCorrectPlaced;

        private bool _removed;
        private bool _replaced;

        /// <summary>True once the mis-picked item has left the tote.</summary>
        public bool Removed => _removed;

        /// <summary>True once the correct item has been placed.</summary>
        public bool Replaced => _replaced;

        private void OnTriggerExit(Collider other)
        {
            if (_removed) return;

            SkuItem item = other.GetComponentInParent<SkuItem>();
            if (item == null || item.Sku != _wrongSku) return;

            _removed = true;
            if (_removeTrigger != null) _removeTrigger.OnTriggerComplete();
            _onMisPickRemoved?.Invoke();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_replaced) return;
            if (_requireRemoveBeforeReplace && !_removed) return;

            SkuItem item = other.GetComponentInParent<SkuItem>();
            if (item == null || item.Sku != _correctSku) return;

            _replaced = true;
            if (_replaceTrigger != null) _replaceTrigger.OnTriggerComplete();
            _onCorrectPlaced?.Invoke();
        }

        /// <summary>Re-arms the scenario (e.g. on step reset).</summary>
        public void ResetRecovery()
        {
            _removed = false;
            _replaced = false;
        }
    }
}
