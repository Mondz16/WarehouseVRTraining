using UnityEngine;

namespace Valari.FoundryTraining.Samples
{
    public sealed class FoundryTrainingSampleRunner : MonoBehaviour
    {
        [SerializeField] private FoundryTrainingSampleConfig _config;

        private void Start()
        {
            Debug.Log(_config != null ? _config.Message : "Package import OK (no config assigned).");
        }
    }
}

