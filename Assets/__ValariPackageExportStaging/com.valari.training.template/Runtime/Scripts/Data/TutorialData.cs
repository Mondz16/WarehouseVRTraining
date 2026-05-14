using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

namespace Valari.Data
{
    [CreateAssetMenu(menuName = "TutorialData", fileName = "TutorialData")]
    public class TutorialData : ScriptableObject
    {
        [field: SerializeField] public string Title { get; set; }
        [field: SerializeField] [field: TextArea(5, 15)] public string Description { get; set; }
        [field: SerializeField] public AudioClip VoiceOver { get; set; }
        [field: SerializeField] public bool HasChallenges { get; set; }
        [field: SerializeField] public bool IsInOrder { get; set; }
        [SerializeField] [ShowIf("HasChallenges")] private List<TutorialChallengeData> TutorialChallengeLists;

        public List<TutorialChallengeData> GetTutorialChallengeData()
        {
            return HasChallenges ? TutorialChallengeLists : null;
        }
    }
}

