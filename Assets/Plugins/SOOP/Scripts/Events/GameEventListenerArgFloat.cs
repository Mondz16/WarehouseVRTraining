// ----------------------------------------------------------------------------
// SOOP Framework
// 
// Original
// Author:  Ryan Hipple
// Date:    10/04/17
//
// Author:  Scott Cameron
// Date:    04/13/18
// ----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.Events;

namespace SOOP.Events
{
    public class GameEventListenerArgFloat : MonoBehaviour
    {
#if UNITY_EDITOR
        [Multiline]
        public string DeveloperDescription = "";
#endif
        [System.Serializable]
        public class ResponseEvent : UnityEvent<float> { }

        [Tooltip("Event to register with.")]
        public GameEventArgFloat Event;

        [Tooltip("Response with float argument to invoke when Event is raised.")]
        public ResponseEvent Response;


        private void OnEnable()
        {
            Event.RegisterListener(this);
        }

        private void OnDisable()
        {
            Event.UnregisterListener(this);
        }

        public void OnEventRaised(float value)
        {
            Response.Invoke(value);
        }
    }
}