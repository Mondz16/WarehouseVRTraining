using UnityEngine;
using UnityEngine.Events;

using System.Collections;
using Valari.Managers;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Launches the knowledge assessment when the M1_09 modal becomes active. Place this on the
    /// M1_09 tutorial panel (or a child that is active only while that step is shown). On enable it
    /// calls <see cref="AssessmentManager.LaunchAssessment"/> so the quiz appears while the M1_09
    /// challenge is armed; a passing score is bridged back to the step by
    /// <see cref="AssessmentChallengeGate"/>.
    ///
    /// LaunchAssessment is a no-op until the assessment has been initialized (module started), so an
    /// early enable at scene load is ignored and does not consume the one-shot latch.
    /// </summary>
    public class AssessmentLauncher : MonoBehaviour
    {
        [Tooltip("The AssessmentManager to launch when this object is shown.")]
        [SerializeField] private AssessmentManager _assessmentManager;

        [Tooltip("If true, only launches the first time it is successfully shown.")]
        [SerializeField] private bool _launchOnce = true;

        [Tooltip("Optional: a UI object (the M1_09 modal container) to hide while the quiz is on screen. AssessmentChallengeGate re-shows it on a pass.")]
        [SerializeField] private GameObject _hideDuringQuiz;

        [SerializeField] private UnityEvent _onLaunched;

        private bool _launched;

private void OnEnable()
        {
            if (_launchOnce && _launched) return;
            if (_assessmentManager == null) return;

            // Only latch if the launch actually happened (assessment initialized / ready).
            if (_assessmentManager.LaunchAssessment())
            {
                _launched = true;
                _onLaunched?.Invoke();
                if (_hideDuringQuiz != null)
                    StartCoroutine(HideDuringQuizRoutine());
            }
        }

        // The tutorial framework activates the modal container a frame or two after this panel is
        // enabled (via its show coroutine). Wait until it is visible, then hide it so only the quiz
        // is on screen; AssessmentChallengeGate re-shows it on a pass so "Next" is reachable.
        private IEnumerator HideDuringQuizRoutine()
        {
            float timeout = 3f;
            while (timeout > 0f && _hideDuringQuiz != null && !_hideDuringQuiz.activeSelf)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (_hideDuringQuiz != null)
                _hideDuringQuiz.SetActive(false);
        }

        public void ResetLauncher() => _launched = false;
    }
}
