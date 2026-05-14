// ----------------------------------------------------------------------------
// SOOP Framework
// 
// Original
// Author:  Ryan Hipple
// Date:    10/04/17
//
// Author:  Scott Cameron
// Date:    03/22/18
// ----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.Events;

namespace SOOP.Events
{
    public class GameEventListenerArgString : MonoBehaviour
    {
#if UNITY_EDITOR
        [Multiline]
        public string DeveloperDescription = "";
#endif
        [System.Serializable]
        public class ResponseEvent : UnityEvent<string> { }

        [Tooltip("Event to register with.")]
        public GameEventArgString Event;

        [Tooltip("Response with string argument to invoke when Event is raised.")]
        public ResponseEvent Response;


        private void OnEnable()
        {
            Event.RegisterListener(this);
        }

        private void OnDisable()
        {
            Event.UnregisterListener(this);
        }

        public void OnEventRaised(string value)
        {
            Response.Invoke(value);
        }
    }
}