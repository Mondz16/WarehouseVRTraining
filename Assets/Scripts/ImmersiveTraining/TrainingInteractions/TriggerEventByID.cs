using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

namespace ImmersiveTraining.TrainingInteractions
{
    public class TriggerEventByID : MonoBehaviour
    {
        [SerializeField] private string _allowedId;
        [SerializeField] private UnityEvent _onTriggerEnter;
        [ReadOnly] public bool _isActivated = false;

        public void ActivateTriggerEvent() => _isActivated = true;

        private void OnTriggerEnter(Collider other)
        {
            if (!_isActivated) return;
            
            var identity = other.GetComponent<TriggerIdentity>();
            if (identity != null && identity.Id == _allowedId)
            {
                _onTriggerEnter.Invoke();
                _isActivated = false;
                gameObject.SetActive(false);
            }
        }
    }
}
