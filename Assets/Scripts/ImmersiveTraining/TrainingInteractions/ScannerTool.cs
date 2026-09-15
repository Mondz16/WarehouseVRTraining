using ImmersiveTraining.Management;
using UnityEngine;
using UnityEngine.Events;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Handheld barcode scanner used for SKU verification (M1_04).
    /// Call <see cref="Scan"/> from the grab tool's trigger/activate event (AutoHand or XRI).
    /// It raycasts from <see cref="_muzzle"/> and compares the hit <see cref="SkuItem"/>'s
    /// Sku against <see cref="_expectedSku"/>, firing <see cref="_onScanVerified"/> on a match
    /// and <see cref="_onScanMismatch"/> on a mismatch. Wire <see cref="_onScanVerified"/> in the
    /// Inspector to a ChallengeTutorialTrigger.OnTriggerComplete so this stays decoupled from the
    /// tutorial framework (same pattern as <see cref="ToteCounter"/>).
    /// </summary>
    public class ScannerTool : MonoBehaviour
    {
        [Header("Beam")]
        [Tooltip("Origin/direction of the scan ray. Its local +Z (forward) is the beam direction. Defaults to this transform.")]
        [SerializeField] private Transform _muzzle;

        [Tooltip("Maximum scan range in metres.")]
        [SerializeField] private float _maxDistance = 3f;

        [Tooltip("Layers a scan ray can hit. Set to the shelf/product layer(s) so the beam ignores hands, UI, etc.")]
        [SerializeField] private LayerMask _scannableMask = ~0;

        [Header("Target")]
        [Tooltip("SKU the current pick expects. Set at runtime via SetExpectedSku for each pick line. Leave empty to accept any scanned SKU.")]
        [SerializeField] private string _expectedSku;

        [Header("Events")]
        [Tooltip("Fired when a scanned item's SKU matches the expected SKU (green confirmation).")]
        [SerializeField] private UnityEvent _onScanVerified;

        [Tooltip("Fired with the offending item when a scanned SKU does NOT match (red flag).")]
        [SerializeField] private UnityObjectEvent _onScanMismatch;

        [Tooltip("Fired when the beam hits nothing scannable (no item under the beam).")]
        [SerializeField] private UnityEvent _onNothingScanned;

        [Header("Challenge (optional, direct wiring)")]
        [Tooltip("If set, its OnTriggerComplete() is called when a scan is verified.")]
        [SerializeField] private ChallengeTutorialTrigger _challengeTrigger;

        /// <summary>The SKU the scanner is currently checking against.</summary>
        public string ExpectedSku => _expectedSku;

        private Transform Muzzle => _muzzle != null ? _muzzle : transform;

        /// <summary>Sets the pick line the scanner should verify against (call when a step begins).</summary>
        public void SetExpectedSku(string sku) => _expectedSku = sku;

        /// <summary>
        /// Fires the scan beam. Wire this to the scanner tool's trigger/activate event.
        /// </summary>
        public void Scan()
        {
            Transform origin = Muzzle;
            if (!Physics.Raycast(origin.position, origin.forward, out RaycastHit hit, _maxDistance,
                    _scannableMask, QueryTriggerInteraction.Ignore))
            {
                _onNothingScanned?.Invoke();
                return;
            }

            SkuItem item = hit.collider.GetComponentInParent<SkuItem>();
            if (item == null)
            {
                _onNothingScanned?.Invoke();
                return;
            }

            bool matches = string.IsNullOrEmpty(_expectedSku) || item.Sku == _expectedSku;
            if (matches)
            {
                Debug.Log($"#{GetType().Name}# Scan verified -> {item.Sku}");
                
                if (_challengeTrigger != null) _challengeTrigger.OnTriggerComplete();
_onScanVerified?.Invoke();
            }
            else
            {
                Debug.Log($"#{GetType().Name}# Scan mismatch -> got {item.Sku}, expected {_expectedSku}");
                _onScanMismatch?.Invoke(item.gameObject);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Transform origin = Muzzle;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin.position, origin.position + origin.forward * _maxDistance);
        }
    }
}
