using UnityEngine;

namespace Valari.Data
{
    [System.Serializable]
    public class TutorialChallengeData
    {
        [field: SerializeField] public string ChallengeTitle { get; set; }
        [field: SerializeField] public string ChallengeTriggerName { get; set; }
        [field: SerializeField] public bool IsComplete { get; set; }
    }
}

