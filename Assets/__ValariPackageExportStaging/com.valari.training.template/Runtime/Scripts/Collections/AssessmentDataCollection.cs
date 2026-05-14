using System;
using System.Collections.Generic;
using Pathfinding.Serialization.JsonFx;
using Sirenix.OdinInspector;
using UnityEngine;
using Valari.Data;

namespace Valari.Collections
{
    [CreateAssetMenu(fileName = "AssessmentDataCollectionSO", menuName = "AssessmentDataCollectionSO", order = 0)]
    public class AssessmentDataCollection : ScriptableObject
    {
        [SerializeField] private List<Assessment> _assessmentList = new List<Assessment>();

        public Assessment GetAssessmentByID(TrainingID id)
        {
            if (!_assessmentList.Exists(x => x.ID == id))
            {
                Debug.Log($"#{GetType().Name}# No Assessment found with -> {id}");
                return null;
            }

            return _assessmentList.Find(x => x.ID == id);
        }
    }

    [Serializable]
    public class Assessment
    {
        public TrainingID ID;
        public List<AssessmentData> Data;

        public int GetTotalScore()
        {
            int score = 0;
            foreach (AssessmentData data in Data)
            {
                if (data.IsCorrect)
                    score++;
            }

            return score;
        }


        [Button]
        public void LoadAssessmentDataByJSON(string json)
        {
            AssessmentWrapper wrapper =
                JsonUtility.FromJson<AssessmentWrapper>(json);

            Data = wrapper.Items;
        }
    }

    [Serializable]
    public class AssessmentWrapper
    {
        public List<AssessmentData> Items;
    }
}

