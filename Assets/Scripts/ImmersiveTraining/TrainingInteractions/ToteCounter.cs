using System.Collections.Generic;
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
    /// Requires a Collider with Is Trigger enabled on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ToteCounter : MonoBehaviour
    {
        [Tooltip("SKU the tote expects. Leave empty to accept any SKU.")]
        [SerializeField] private string _requiredSku;

        [Tooltip("How many matching items complete the pick.")]
        [SerializeField] private int _requiredCount = 1;

        [Header("Events")]
        [Tooltip("Fired once when the required count of matching items is reached. Latches - it will not fire again if items are later removed.")]
        [SerializeField] private UnityEvent _onTargetReached;

        [Tooltip("Fired with the current matching count each time it changes.")]
        [SerializeField] private IntEvent _onCountChanged;

        [Tooltip("Fired with the offending item when a non-matching SKU is placed (mis-pick, M1_07).")]
        [SerializeField] private UnityObjectEvent _onWrongItemAdded;

        [Header("Challenge (optional, direct wiring)")]
        [Tooltip("If set, its OnTriggerComplete() is called when the target count is reached.")]
        [SerializeField] private ChallengeTutorialTrigger _challengeTrigger;

        [Tooltip("If set, its NotifyComplete() is called when the target count is reached (multi-line aggregation, M1_06).")]
        [SerializeField] private MultiCounterGate _gate;

        private readonly HashSet<int> _countedItems = new HashSet<int>();
        private bool _targetReached;

        /// <summary>Number of matching items currently counted inside the tote.</summary>
        public int CurrentCount => _countedItems.Count;

        private void OnTriggerEnter(Collider other)
        {
            SkuItem item = other.GetComponentInParent<SkuItem>();
            if (item == null) return;

            int id = item.gameObject.GetInstanceID();
            if (_countedItems.Contains(id)) return;

            bool matches = string.IsNullOrEmpty(_requiredSku) || item.Sku == _requiredSku;
            if (!matches)
            {
                _onWrongItemAdded?.Invoke(item.gameObject);
                return;
            }

            _countedItems.Add(id);
            _onCountChanged?.Invoke(_countedItems.Count);

            if (!_targetReached && _countedItems.Count >= _requiredCount)
            {
                _targetReached = true;
                
                if (_challengeTrigger != null) _challengeTrigger.OnTriggerComplete();
if (_gate != null) _gate.NotifyComplete();
                _onTargetReached?.Invoke();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            SkuItem item = other.GetComponentInParent<SkuItem>();
            if (item == null) return;

            int id = item.gameObject.GetInstanceID();
            if (_countedItems.Remove(id))
                _onCountChanged?.Invoke(_countedItems.Count);
        }

        /// <summary>Clears the counted items and re-arms the target event (e.g. on step reset).</summary>
        public void ResetTote()
        {
            _countedItems.Clear();
            _targetReached = false;
            _onCountChanged?.Invoke(0);
        }
    }

    [System.Serializable]
    public class IntEvent : UnityEvent<int> { }
}
