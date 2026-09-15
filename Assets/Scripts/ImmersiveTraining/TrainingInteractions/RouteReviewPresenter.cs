using UnityEngine;
using System.Collections;
using Valari.Managers;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Presents the path-efficiency route overlay when the M1_08 modal opens. Place on the M1_08
    /// tutorial panel. Gated on the OrderPicking module having started (the tutorial panels are saved
    /// active, so OnEnable also fires once at scene load) — when the step genuinely opens it stops the
    /// route recording and shows the overlay for review. The step auto-completes a
    /// moment after the overlay is shown (via _completeTrigger); no grab marker is needed.
    /// </summary>
    public class RouteReviewPresenter : MonoBehaviour
    {
        [SerializeField] private RouteRecorder _recorder;
        [SerializeField] private RouteOverlayController _overlay;

        [Tooltip("Fired to complete the M1_08 step after the overlay is shown (replaces the grab marker).")]
        [SerializeField] private ChallengeTutorialTrigger _completeTrigger;

        [Tooltip("Seconds to show the overlay before auto-completing (also lets the framework arm the trigger).")]
        [SerializeField] private float _autoCompleteDelay = 1.5f;

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

            if (_completeTrigger != null)
                StartCoroutine(CompleteAfterDelay());
        }

        private IEnumerator CompleteAfterDelay()
        {
            // Small delay so the trainee sees the route lines, and so the framework has armed the
            // M1_08 challenge trigger (arming happens a frame or two after this panel is enabled).
            if (_autoCompleteDelay > 0f)
                yield return new WaitForSeconds(_autoCompleteDelay);

            if (_completeTrigger != null)
                _completeTrigger.OnTriggerComplete();
        }
    }
}
