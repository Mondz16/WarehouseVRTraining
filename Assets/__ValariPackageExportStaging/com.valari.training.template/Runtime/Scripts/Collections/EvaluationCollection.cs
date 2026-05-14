using UnityEngine;
using System.Collections.Generic;
using Valari.Data;

namespace Valari.Collections
{
    [CreateAssetMenu(fileName = "EvaluationCollectionSO", menuName = "EvaluationCollection", order = 0)]
    public class EvaluationCollection : ScriptableObject
    {
        [SerializeField] private List<EvaluationData> DataList = new List<EvaluationData>();

        public void Reset()
        {
            foreach (EvaluationData evaluationData in DataList)
                evaluationData.ResetValues();
        }

        public void ResetByID(TrainingID id)
        {
            var data = GetEvaluationDataByTitle(id);
            data.ResetValues();
        }

        public void SetEfficiencyTime(TrainingID id, float efficiencyTime)
        {
            var data = GetEvaluationDataByTitle(id);
            data.EfficiencyTime = efficiencyTime;
            Debug.Log($"#{GetType().Name}# Updated Data -> {data.ToString()}");
        }

        public void SetMistakeCount(TrainingID id, int mistakeCount)
        {
            var data = GetEvaluationDataByTitle(id);
            data.MistakeCount = mistakeCount;
            Debug.Log($"#{GetType().Name}# Updated Data -> {data.ToString()}");
        }

        public void SetQuizScore(TrainingID id, int quizScore)
        {
            var data = GetEvaluationDataByTitle(id);
            data.QuizScore = quizScore;
            Debug.Log($"#{GetType().Name}# Updated Data -> {data.ToString()}");
        }

        public EvaluationData GetEvaluationDataByTitle(TrainingID id)
        {
            if (!IsTitleExist(id))
            {
                Debug.Log($"#{GetType().Name}# Title: {id} -> does not exists!");
                return null;
            }

            var data = DataList.Find(x => x.ID == id);
            return data;
        }

        private bool IsTitleExist(TrainingID title)
        {
            return DataList.Exists(x => x.ID == title);
        }
    }
}

