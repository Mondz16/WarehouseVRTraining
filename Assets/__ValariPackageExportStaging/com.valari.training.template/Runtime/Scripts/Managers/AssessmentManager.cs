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
        private List<AssessmentData> _currentAssessmentDataList = new List<AssessmentData>();

        public static Action<int> OnAssessmentFinishedEvent { get; set; }

        private void OnEnable()
        {
            TutorialManager.OnTrainingStartedEvent += InitializeAssessment;
            TutorialManager.OnTrainingCompleteEvent += ShowQuestion;
        }

        private void OnDisable()
        {
            TutorialManager.OnTrainingStartedEvent -= InitializeAssessment;
            TutorialManager.OnTrainingCompleteEvent -= ShowQuestion;
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
    }
}

