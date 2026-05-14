using UnityEngine;

namespace Valari.Data
{
    [System.Serializable]
    public class EvaluationData
    {
        public TrainingID ID;
        public float EfficiencyTime;
        public float OptimalEfficiencyTime;
        public int MistakeCount;
        public int QuizScore;
        public int TotalQuiz;

        public void ResetValues()
        {
            EfficiencyTime = 0;
            MistakeCount = 0;
            QuizScore = 0;
        }

        public float GetEfficiencyResult()
        {
            return (float)Mathf.Clamp((OptimalEfficiencyTime / EfficiencyTime) * 100f, 50f, 100f);
        }

        public float GetAccuracyScore()
        {
            return (float)Mathf.Clamp(100f - (MistakeCount * 5f), 40f, 100f);
        }

        public float GetAsssessmentScore()
        {
            return Mathf.Clamp(((float)QuizScore / (float)TotalQuiz) * 100, 0f, 100f);
        }

        public float GetPracticalScore()
        {
            return (GetEfficiencyResult() * .04f) + (GetAccuracyScore() * .6f);
        }

        public override string ToString()
        {
            return
                $"ID:{ID}\nEfficiencyTime:{EfficiencyTime}\nOptimalEfficiencyTime:{OptimalEfficiencyTime}\nMistakeCount:{MistakeCount}\nQuizScore:{QuizScore}";
        }
    }
}

