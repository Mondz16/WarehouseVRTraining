using Autohand;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Bridges an AutoHand <see cref="Grabbable"/>'s squeeze/activate input to
    /// <see cref="ScannerTool.Scan"/>. Attach to the scanner tool: while the tool is
    /// held, squeezing (the hand's activate gesture) fires a scan.
    ///
    /// This exists because MCP cannot wire UnityEvents — subscribing to AutoHand's
    /// C# <see cref="Grabbable.OnSqueezeEvent"/> keeps the whole scan flow settable via
    /// object references (same decoupling pattern as the rest of the M1 interactions).
    /// </summary>
    [RequireComponent(typeof(ScannerTool))]
    public class ScannerActivator : MonoBehaviour
    {
        [Tooltip("The grabbable scanner. Defaults to a Grabbable on this GameObject.")]
        [SerializeField] private Grabbable _grabbable;

        [Tooltip("The scanner to fire. Defaults to a ScannerTool on this GameObject.")]
        [SerializeField] private ScannerTool _scanner;

        private void Awake()
        {
            if (_grabbable == null) _grabbable = GetComponent<Grabbable>();
            if (_scanner == null) _scanner = GetComponent<ScannerTool>();
        }

        private void OnEnable()
        {
            if (_grabbable != null) _grabbable.OnSqueezeEvent += HandleSqueeze;
        }

        private void OnDisable()
        {
            if (_grabbable != null) _grabbable.OnSqueezeEvent -= HandleSqueeze;
        }

        private void HandleSqueeze(Hand hand, Grabbable grabbable)
        {
            if (_scanner != null) _scanner.Scan();
        }
    }
}
