using UnityEngine;
using UnityEngine.Events;

using System.Collections;
using Valari.Managers;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Launches the knowledge assessment when an assessment step's modal becomes active (M1_09, M2_10). Place this on
    /// the tutorial panel (or a child that is active only while that step is shown). On enable it calls
    /// <see cref="AssessmentManager.LaunchAssessment"/> so the quiz appears while the step's challenge is armed; a
    /// passing score is bridged back to the step by <see cref="AssessmentChallengeGate"/>.
    ///
    /// LaunchAssessment is a no-op until the assessment has been initialized (module started), so an
    /// early enable at scene load is ignored and does not consume the one-shot latch.
    ///
    /// The quiz UI lives at the spawn. With <see cref="_quizRoot"/> and <see cref="_quizAnchor"/> set, the quiz is
    /// moved to the anchor (where the trainee is) while this step runs and put back when the panel closes, so other
    /// modules' quizzes still appear at the spawn.
    /// </summary>
    public class AssessmentLauncher : MonoBehaviour
    {
        [Tooltip("The AssessmentManager to launch when this object is shown.")]
        [SerializeField] private AssessmentManager _assessmentManager;

        [Tooltip("If true, only launches the first time it is successfully shown.")]
        [SerializeField] private bool _launchOnce = true;

        [Tooltip("Optional: a UI object (the step's modal container) to hide while the quiz is on screen. AssessmentChallengeGate re-shows it on a pass.")]
        [SerializeField] private GameObject _hideDuringQuiz;

        [Header("Quiz placement (optional)")]
        [Tooltip("The quiz UI root (Canvas/AssesmentModalUI).")]
        [SerializeField] private Transform _quizRoot;

        [Tooltip("Pose the quiz root takes while this step runs.")]
        [SerializeField] private Transform _quizAnchor;

        [SerializeField] private UnityEvent _onLaunched;

        private bool _launched;
        private bool _quizMoved;
        private Vector3 _quizHomePosition;
        private Quaternion _quizHomeRotation;

        private void OnEnable()
        {
            if (_launchOnce && _launched) return;
            if (_assessmentManager == null) return;

            // Only latch if the launch actually happened (assessment initialized / ready).
            if (_assessmentManager.LaunchAssessment())
            {
                _launched = true;
                MoveQuizToAnchor();
                _onLaunched?.Invoke();
                if (_hideDuringQuiz != null)
                    StartCoroutine(HideDuringQuizRoutine());
            }
        }

        private void OnDisable() => RestoreQuiz();

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

        private void MoveQuizToAnchor()
        {
            if (_quizRoot == null || _quizAnchor == null || _quizMoved) return;
            _quizHomePosition = _quizRoot.position;
            _quizHomeRotation = _quizRoot.rotation;
            _quizRoot.SetPositionAndRotation(_quizAnchor.position, _quizAnchor.rotation);
            _quizMoved = true;
        }

        private void RestoreQuiz()
        {
            if (!_quizMoved || _quizRoot == null) return;
            _quizRoot.SetPositionAndRotation(_quizHomePosition, _quizHomeRotation);
            _quizMoved = false;
        }

        public void ResetLauncher() => _launched = false;
    }
}
