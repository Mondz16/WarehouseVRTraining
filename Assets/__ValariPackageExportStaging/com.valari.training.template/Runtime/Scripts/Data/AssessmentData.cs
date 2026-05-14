using System;
using System.Collections.Generic;
using UnityEngine;

namespace Valari.Data
{
    [Serializable]
    public class AssessmentData
    {
        public string Question;
        public int AnswerIndex;
        public List<string> Choices;
        public bool IsCorrect;

        public void ValidateAnswer(string answer)
        {
            IsCorrect = this.Choices[AnswerIndex].Equals(answer, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}

