using Autohand;
using UnityEngine;
using UnityEngine.Events;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Fires a <see cref="ChallengeTutorialTrigger"/> when an AutoHand <see cref="Grabbable"/>
    /// is first grabbed — e.g. opening the wrist pick-list device (M1_02).
    ///
    /// Subscribes to AutoHand's C# <see cref="Grabbable.OnGrabEvent"/> so the whole flow stays
    /// settable via object references (MCP cannot wire UnityEvents). Same decoupling pattern as
    /// <see cref="ScannerActivator"/> and <see cref="LocationZoneTrigger"/>.
    /// </summary>
    public class GrabChallengeTrigger : MonoBehaviour
    {
        [Tooltip("The grabbable to watch. Defaults to a Grabbable on this GameObject.")]
        [SerializeField] private Grabbable _grabbable;

        [Header("Challenge (optional, direct wiring)")]
        [Tooltip("If set, its OnTriggerComplete() is called when the grabbable is first grabbed.")]
        [SerializeField] private ChallengeTutorialTrigger _challengeTrigger;

        [Header("Events")]
        [Tooltip("Fired when the grabbable is grabbed (every grab, or once if Fire Once is set).")]
        [SerializeField] private UnityEvent _onGrabbed;

        [Tooltip("If true, only fires the first time the grabbable is grabbed.")]
        [SerializeField] private bool _fireOnce = true;

        private bool _fired;

        /// <summary>True once the grabbable has been grabbed (only meaningful when Fire Once is set).</summary>
        public bool Fired => _fired;

        private void Awake()
        {
            if (_grabbable == null) _grabbable = GetComponent<Grabbable>();
        }

        private void OnEnable()
        {
            if (_grabbable != null) _grabbable.OnGrabEvent += HandleGrab;
        }

        private void OnDisable()
        {
            if (_grabbable != null) _grabbable.OnGrabEvent -= HandleGrab;
        }

        private void HandleGrab(Hand hand, Grabbable grabbable)
        {
            if (_fireOnce && _fired) return;
            _fired = true;

            if (_challengeTrigger != null) _challengeTrigger.OnTriggerComplete();
            _onGrabbed?.Invoke();
        }

        /// <summary>Re-arms the trigger so it can fire again (e.g. on step reset).</summary>
        public void ResetTrigger()
        {
            _fired = false;
        }
    }
}
