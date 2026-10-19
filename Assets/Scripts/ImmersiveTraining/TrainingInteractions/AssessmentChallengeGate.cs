using UnityEngine;
using UnityEngine.Events;
using Valari.Managers;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Gates completion of the M1_09 assessment step on a passing score and bridges it back to the
    /// Valari tutorial framework. Subscribes to <see cref="AssessmentManager.OnAssessmentFinishedEvent"/>
    /// (fired when the learner closes the quiz result screen). If the score is at least
    /// <see cref="_passPercent"/> of the total questions it completes <see cref="_challengeTrigger"/>
    /// (TRIGGER_M1_AssessmentComplete) so the M1_09 modal challenge completes; otherwise it invokes
    /// <see cref="_onFailed"/> and shows the fail screen or, when <see cref="_retakeOnFail"/> is set,
    /// relaunches the quiz from the first question.
    ///
    /// The finished event is shared by every module's quiz, so the gate only reacts while
    /// <see cref="_trainingId"/> is the running module and its challenge step is active — otherwise the
    /// pass/fail handling would hijack other modules' assessments (e.g. the Safety quiz).
    ///
    /// The assessment framework produces only a raw score; the >=80% pass rule lives here, since the
    /// framework has no built-in pass/fail threshold.
    /// </summary>
    public class AssessmentChallengeGate : MonoBehaviour
    {
        [Header("Assessment")]
        [Tooltip("The AssessmentManager whose finished event this gate listens to.")]
        [SerializeField] private AssessmentManager _assessmentManager;

        [Tooltip("Module this gate grades. Quizzes finished while another module is running are ignored.")]
        [SerializeField] private TrainingID _trainingId = TrainingID.OrderPicking;

        [Tooltip("Minimum percentage of correct answers required to pass (module spec = 80).")]
        [SerializeField] [Range(0, 100)] private int _passPercent = 80;

        [Tooltip("If true, a failing score relaunches the quiz from the first question.")]
        [SerializeField] private bool _retakeOnFail = true;

        [Header("Challenge (direct wiring)")]
        [Tooltip("Completed when the learner passes, completing the M1_09 step.")]
        [SerializeField] private ChallengeTutorialTrigger _challengeTrigger;

        [Tooltip("Optional: a UI object (the M1_09 modal container) hidden during the quiz, re-shown here on a pass so the 'Next' button is reachable.")]
        [SerializeField] private GameObject _showOnPass;

        [Header("Events")]
        [Tooltip("Fired when the learner passes (score >= pass percent).")]
        [SerializeField] private UnityEvent _onPassed;

        [Tooltip("Fired when the learner fails (before any retake relaunch).")]
        [SerializeField] private UnityEvent _onFailed;

        [Header("Fail screen (optional)")]
        [Tooltip("Shown on a failing score instead of an immediate relaunch; its Retry button relaunches the quiz.")]
        [SerializeField] private AssessmentFailPanel _failPanel;

        private TrainingID _currentTraining = TrainingID.None;

        private void OnEnable()
        {
            AssessmentManager.OnAssessmentFinishedEvent += HandleAssessmentFinished;
            TutorialManager.OnTrainingStartedEvent += HandleTrainingStarted;
        }

        private void OnDisable()
        {
            AssessmentManager.OnAssessmentFinishedEvent -= HandleAssessmentFinished;
            TutorialManager.OnTrainingStartedEvent -= HandleTrainingStarted;
        }

        private void HandleTrainingStarted(TrainingID id) => _currentTraining = id;

        private void HandleAssessmentFinished(int score)
        {
            // Another module's quiz, or this module's quiz outside its assessment step: not ours to grade.
            if (_currentTraining != _trainingId) return;
            if (_challengeTrigger != null && !_challengeTrigger.IsArmed) return;

            int total = _assessmentManager != null ? _assessmentManager.TotalQuestions : 0;
            bool passed = total > 0 && score * 100 >= _passPercent * total;

            Debug.Log($"#{GetType().Name}# Assessment finished -> {score}/{total} " +
                      $"(need {_passPercent}%) => {(passed ? "PASS" : "FAIL")}");

            if (passed)
            {
                // Re-show the M1_09 modal (hidden while the quiz was up) so its "Next" button is
                // reachable once the challenge completes.
                if (_showOnPass != null) _showOnPass.SetActive(true);
                if (_challengeTrigger != null) _challengeTrigger.TryComplete();
                _onPassed?.Invoke();
            }
            else
            {
                _onFailed?.Invoke();
                if (_failPanel != null)
                    _failPanel.Show(score, total, _passPercent);
                else if (_retakeOnFail && _assessmentManager != null)
                    _assessmentManager.RetakeAssessment();
            }
        }
    }
}
