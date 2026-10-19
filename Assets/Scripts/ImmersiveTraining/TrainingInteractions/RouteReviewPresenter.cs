using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using Valari.Managers;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Presents the path-efficiency route overlay when the M1_08 modal opens. Place on the M1_08
    /// tutorial panel. Gated on the OrderPicking module having started (the tutorial panels are saved
    /// active, so OnEnable also fires once at scene load) — when the step genuinely opens it stops the
    /// route recording and shows the overlay for review. The step then completes itself: after
    /// <see cref="_autoCompleteDelay"/> it offers the completion to <see cref="_completeTrigger"/> every
    /// frame until the framework has armed it. No grab marker is needed.
    /// </summary>
    public class RouteReviewPresenter : MonoBehaviour
    {
        [SerializeField] private RouteRecorder _recorder;
        [SerializeField] private RouteOverlayController _overlay;

        [Tooltip("Completed after the overlay is shown (replaces the grab marker).")]
        [SerializeField] private ChallengeTutorialTrigger _completeTrigger;

        [Tooltip("Minimum seconds the overlay is shown before the step completes.")]
        [SerializeField] private float _autoCompleteDelay = 1.5f;

        [Tooltip("Fired once the review step has completed.")]
        [SerializeField] private UnityEvent _onCompleted;

        private bool _armed;
        private bool _done;

        private void Awake()
        {
            TutorialManager.OnTrainingStartedEvent -= HandleTrainingStarted;
            TutorialManager.OnTrainingStartedEvent += HandleTrainingStarted;
        }

        private void OnDestroy()
        {
            TutorialManager.OnTrainingStartedEvent -= HandleTrainingStarted;
        }

        private void HandleTrainingStarted(TrainingID id)
        {
            if (id != TrainingID.OrderPicking) return;
            _armed = true;
            _done = false;
        }

        private void OnEnable()
        {
            if (_done) return;
            if (!_armed) return; // ignore the scene-load / pre-module enable

            if (_recorder != null) _recorder.StopRecording();
            if (_overlay != null) _overlay.ShowOverlay();
            _done = true;

            StartCoroutine(CompleteAfterDelay());
        }

        private IEnumerator CompleteAfterDelay()
        {
            // Let the trainee see the route lines first.
            if (_autoCompleteDelay > 0f)
                yield return new WaitForSeconds(_autoCompleteDelay);

            // The framework arms the M1_08 trigger a frame or two after this panel is enabled (later
            // while the trainer is still walking), so keep offering until the completion is accepted.
            while (_completeTrigger != null && !_completeTrigger.TryComplete())
                yield return null;

            _onCompleted?.Invoke();
        }
    }
}
