using UnityEngine;

namespace Valari.FoundryTraining.Samples
{
    [CreateAssetMenu(menuName = "Valari/FoundryTraining/SampleConfig", fileName = "FoundryTrainingSampleConfig")]
    public sealed class FoundryTrainingSampleConfig : ScriptableObject
    {
        [field: SerializeField] public string Message { get; private set; } = "Package import OK.";
    }
}

