using System;
using Pathfinding.Serialization.JsonFx;
using UnityEngine;
using Valari.Collections;
using Valari.UI;

namespace Valari.Managers
{
    public class PerformanceEvaluationManager : MonoBehaviour
    {
        [SerializeField] private PerformanceUI _performanceUI;
        [SerializeField] private EvaluationCollection _evaluationCollectionSO;
        [SerializeField] private LLMHandler _llmHandler;

        private int _mistakeCounter = 0;
        private float _startingTime = 0;
        private TrainingID _currentTrainingId = TrainingID.None;

        private void OnEnable()
        {
            TutorialManager.OnTrainingStartedEvent += InitializeTraining;
            TutorialManager.OnUpdateMistakeCountEvent += UpdateMistakeCounter;
            TutorialManager.OnTrainingCompleteEvent += EvaluateEfficiencyAndAccuracy;
            AssessmentManager.OnAssessmentFinishedEvent += EvaluateAssessmentScore;
        }

        private void OnDisable()
        {
            TutorialManager.OnTrainingStartedEvent -= InitializeTraining;
            TutorialManager.OnUpdateMistakeCountEvent -= UpdateMistakeCounter;
            TutorialManager.OnTrainingCompleteEvent -= EvaluateEfficiencyAndAccuracy;
            AssessmentManager.OnAssessmentFinishedEvent -= EvaluateAssessmentScore;
        }

        private void InitializeTraining(TrainingID id)
        {
            _currentTrainingId = id;
            _evaluationCollectionSO.ResetByID(id);
            _startingTime = Time.time;
            _mistakeCounter = 0;
        }

        private void UpdateMistakeCounter()
        {
            _mistakeCounter++;
        }

        private void EvaluateEfficiencyAndAccuracy()
        {
            float efficiencyTime = Time.time - _startingTime;
            _evaluationCollectionSO.SetEfficiencyTime(_currentTrainingId, efficiencyTime);
            _evaluationCollectionSO.SetMistakeCount(_currentTrainingId, _mistakeCounter);
            Debug.Log($"#{GetType().Name}# Efficiency Time -> {efficiencyTime}");
        }

        private void EvaluateAssessmentScore(int score)
        {
            _evaluationCollectionSO.SetQuizScore(_currentTrainingId, score);
            Debug.Log($"#{GetType().Name}# Assessment Score -> {score}");
            EvaluatePerformance();
        }

        private void EvaluatePerformance()
        {
            var data = _evaluationCollectionSO.GetEvaluationDataByTitle(_currentTrainingId);
            _performanceUI.ShowPerformanceResult(_currentTrainingId, data.GetEfficiencyResult(), data.GetAccuracyScore(),
                data.GetAsssessmentScore());
            var json = JsonWriter.Serialize(data);
            _llmHandler.SendPrompt(
                $"{LLMPrompt.LLM_Agent_PerformanceEvaluation_Prompt}\nTraining Module: {_currentTrainingId} \nHere is the evaluation data for analysis: {json}",
                true);
        }
    }
}

