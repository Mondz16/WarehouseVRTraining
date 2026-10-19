using Autohand;
using UnityEngine;
using UnityEngine.Events;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Fires a <see cref="ChallengeTutorialTrigger"/> when an AutoHand <see cref="Grabbable"/>
    /// is grabbed — e.g. opening the wrist pick-list device (M1_02).
    ///
    /// Subscribes to AutoHand's C# <see cref="Grabbable.OnGrabEvent"/> so the whole flow stays
    /// settable via object references (MCP cannot wire UnityEvents). Same decoupling pattern as
    /// <see cref="ScannerActivator"/> and <see cref="LocationZoneTrigger"/>.
    ///
    /// A grab only counts once the challenge accepts it: grabbing before the step is active is
    /// ignored (the trainee can simply grab again), and if the device is still held when the step
    /// opens it counts straight away.
    /// </summary>
    public class GrabChallengeTrigger : MonoBehaviour
    {
        [Tooltip("The grabbable to watch. Defaults to a Grabbable on this GameObject.")]
        [SerializeField] private Grabbable _grabbable;

        [Header("Challenge (optional, direct wiring)")]
        [Tooltip("If set, its TryComplete() is called when the grabbable is grabbed while the step is active.")]
        [SerializeField] private ChallengeTutorialTrigger _challengeTrigger;

        [Header("Events")]
        [Tooltip("Fired when a grab counts (every counted grab, or once if Fire Once is set).")]
        [SerializeField] private UnityEvent _onGrabbed;

        [Tooltip("If true, only the first counted grab fires.")]
        [SerializeField] private bool _fireOnce = true;

        private readonly ArmedLatch _latch = new ArmedLatch();

        /// <summary>True once a grab has counted (only meaningful when Fire Once is set).</summary>
        public bool Fired => _latch.IsLatched;

        private void Awake()
        {
            if (_grabbable == null) _grabbable = GetComponent<Grabbable>();
        }

        private void OnEnable()
        {
            if (_grabbable != null) _grabbable.OnGrabEvent += HandleGrab;
            if (_challengeTrigger != null) _challengeTrigger.Armed += HandleArmed;
        }

        private void OnDisable()
        {
            if (_grabbable != null) _grabbable.OnGrabEvent -= HandleGrab;
            if (_challengeTrigger != null) _challengeTrigger.Armed -= HandleArmed;
        }

        private void HandleGrab(Hand hand, Grabbable grabbable) => OfferGrab();

        // Grabbed before the step opened and still holding it: count it now.
        private void HandleArmed(ChallengeTutorialTrigger trigger)
        {
            if (_grabbable != null && _grabbable.IsHeld()) OfferGrab();
        }

        private void OfferGrab()
        {
            bool counted = _fireOnce ? _latch.Offer(true, TryCompleteChallenge) : TryCompleteChallenge();
            if (counted) _onGrabbed?.Invoke();
        }

        private bool TryCompleteChallenge() => _challengeTrigger == null || _challengeTrigger.TryComplete();

        /// <summary>Re-arms the trigger so it can fire again (e.g. on step reset).</summary>
        public void ResetTrigger() => _latch.Reset();
    }
}
