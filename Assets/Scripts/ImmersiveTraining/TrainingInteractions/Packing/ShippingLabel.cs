using Autohand;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// The printed shipping label (M2_06). Scanning it with the bench scanner verifies it (address / weight class /
    /// carrier readout) and completes <see cref="_verifiedTrigger"/>. Once verified, letting go of it on the top face
    /// of a sealed carton applies it: the carton shows its own label and this card disappears.
    /// The verify completion is arm-aware: a scan made before M2_06 opens counts when it opens.
    /// </summary>
    public class ShippingLabel : MonoBehaviour, IScanListener
    {
        [SerializeField] private Grabbable _grabbable;
        [SerializeField] private Rigidbody _body;
        [SerializeField] private PackingFeedback _feedback;

        [Tooltip("Readout shown once a scan verifies the label (address / weight class / carrier).")]
        [SerializeField] private GameObject _verifiedReadout;

        [Tooltip("Completed when the label has been verified by a scan (TRIGGER_M2_LabelVerified).")]
        [SerializeField] private ChallengeTutorialTrigger _verifiedTrigger;

        [SerializeField] private string _verifiedMessage = "Label verified: address OK, weight class OK, carrier OK.";

        private readonly ArmedLatch _latch = new ArmedLatch();
        private Vector3 _homePosition;
        private Quaternion _homeRotation;

        public bool IsVerified { get; private set; }
        public bool IsApplied { get; private set; }
        public bool IsHeld => _grabbable != null && _grabbable.IsHeld();

        private void Awake()
        {
            if (_grabbable == null) _grabbable = GetComponent<Grabbable>();
            if (_body == null) _body = GetComponent<Rigidbody>();
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            if (_verifiedReadout != null) _verifiedReadout.SetActive(false);

            // Subscribed for the whole lifetime: the card is deactivated once applied, but a scan made before M2_06
            // opened must still count when it opens.
            if (_verifiedTrigger != null) _verifiedTrigger.Armed += HandleArmed;
        }

        private void OnDestroy()
        {
            if (_verifiedTrigger != null) _verifiedTrigger.Armed -= HandleArmed;
        }

        public void OnScanned(ScannerTool scanner, bool verified)
        {
            if (!verified || IsApplied) return;
            if (!IsVerified)
            {
                IsVerified = true;
                if (_verifiedReadout != null) _verifiedReadout.SetActive(true);
                Say(_verifiedMessage, true);
            }

            Offer();
        }

        private void HandleArmed(ChallengeTutorialTrigger trigger) => Offer();

        private void Offer() => _latch.Offer(IsVerified, () => _verifiedTrigger == null || _verifiedTrigger.TryComplete());

        private void Update()
        {
            if (IsApplied || IsHeld) return;

            foreach (PackingCarton carton in PackingCarton.All)
            {
                if (carton == null || !carton.IsOnTopFace(transform.position)) continue;

                if (!IsVerified)
                {
                    Say("Scan the label to verify it before you apply it.", false);
                    ReturnHome();
                    return;
                }

                switch (carton.TryApplyLabel())
                {
                    case PackingCarton.LabelResult.Applied:
                        IsApplied = true;
                        gameObject.SetActive(false);
                        return;
                    case PackingCarton.LabelResult.NotSealed:
                        Say("Seal the box with the H-tape pattern before you label it.", false);
                        ReturnHome();
                        return;
                    default:
                        ReturnHome();
                        return;
                }
            }
        }

        /// <summary>Back on the printer, unverified (module restart).</summary>
        public void ResetLabel()
        {
            IsVerified = false;
            IsApplied = false;
            _latch.Reset();
            if (_verifiedReadout != null) _verifiedReadout.SetActive(false);
            gameObject.SetActive(true);
            ReturnHome();
        }

        private void ReturnHome()
        {
            if (_grabbable != null) _grabbable.ForceHandsRelease();
            transform.SetPositionAndRotation(_homePosition, _homeRotation);
            if (_body == null || _body.isKinematic) return;
            _body.position = _homePosition;
            _body.rotation = _homeRotation;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }

        private void Say(string message, bool good)
        {
            if (_feedback != null) _feedback.Show(message, good);
        }
    }
}
