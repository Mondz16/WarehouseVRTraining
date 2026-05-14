using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    public class TriggerIdentity : MonoBehaviour
    {
        [SerializeField] private string _id;

        public string Id => _id;
    }
}
