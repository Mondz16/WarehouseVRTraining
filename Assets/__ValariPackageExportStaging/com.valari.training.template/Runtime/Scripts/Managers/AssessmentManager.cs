using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Valari.Collections;
using Valari.Data;
using Valari.UI;

namespace Valari.Managers
{
    public class AssessmentManager : MonoBehaviour
    {
        [SerializeField] private AssessmentDataCollection _assessmentDataCollection;
        [SerializeField] private AssessmentUI _assessmentUI;

        private AssessmentData _currentQuestion;
        private Assessment _currentAssessment = null;
        private int _assessmentIndex = 0;
        
        private bool _manualLaunchConsumed = false;
private List<AssessmentData> _currentAssessmentDataList = new List<AssessmentData>();

        public static Action<int> OnAssessmentFinishedEvent { get; set; }

private void OnEnable()
        {
            TutorialManager.OnTrainingStartedEvent += InitializeAssessment;
            TutorialManager.OnTrainingCompleteEvent += HandleTrainingComplete;
        }

private void OnDisable()
        {
            TutorialManager.OnTrainingStartedEvent -= InitializeAssessment;
            TutorialManager.OnTrainingCompleteEvent -= HandleTrainingComplete;
        }

        private void InitializeAssessment(TrainingID id)
        {
            _currentAssessment = _assessmentDataCollection.GetAssessmentByID(id);
            _currentAssessmentDataList = _currentAssessment.Data;
            _assessmentIndex = 0;
            _assessmentUI.InjectAssessmentManager(this);
        }

        private void ShowQuestion()
        {
            _currentQuestion = _currentAssessmentDataList[_assessmentIndex];
            _assessmentUI.ShowQuestion(_currentAssessment.ID, _currentQuestion);
        }

        public void SubmitAnswer(string answer)
        {
            _currentQuestion.ValidateAnswer(answer);
            _assessmentIndex++;
            if (_assessmentIndex >= _currentAssessmentDataList.Count)
            {
                Debug.Log($"#{GetType().Name}# Assessment Finished! -> {_currentAssessment.GetTotalScore()}");
                _assessmentIndex = 0;
                _assessmentUI.ShowResult(_currentAssessment.GetTotalScore(), _currentAssessmentDataList.Count);
            }
            else
            {
                ShowQuestion();
            }
        }

        public void OnFinishAssessment()
        {
            OnAssessmentFinishedEvent?.Invoke(_currentAssessment.GetTotalScore());
        }

public int TotalQuestions => _currentAssessmentDataList != null ? _currentAssessmentDataList.Count : 0;

        // Auto-launch handler (after the last tutorial modal). If a step already launched the quiz
        // manually (e.g. M1_09 gates progression on the score), consume that flag and skip so the
        // quiz does not reappear after the module-complete screen. Other modules that never launch
        // manually keep the original auto-launch behaviour unchanged.
        private void HandleTrainingComplete()
        {
            if (_manualLaunchConsumed)
            {
                _manualLaunchConsumed = false;
                return;
            }

            ShowQuestion();
        }

        // Launches the quiz on demand (used by AssessmentLauncher when the M1_09 modal opens).
        // Returns false if the assessment has not been initialized yet, so early callers can avoid
        // latching a one-shot.
        public bool LaunchAssessment()
        {
            if (_currentAssessment == null || _currentAssessmentDataList == null || _currentAssessmentDataList.Count == 0)
            {
                Debug.LogWarning($"#{GetType().Name}# LaunchAssessment skipped: assessment not initialized yet.");
                return false;
            }

            _manualLaunchConsumed = true;
            _assessmentIndex = 0;
            ShowQuestion();
            return true;
        }

        // Restarts the current assessment from the first question (used for retake-on-fail).
        public void RetakeAssessment()
        {
            if (_currentAssessment == null || _currentAssessmentDataList == null || _currentAssessmentDataList.Count == 0)
                return;

            _assessmentIndex = 0;
            ShowQuestion();
        }

    }
}

