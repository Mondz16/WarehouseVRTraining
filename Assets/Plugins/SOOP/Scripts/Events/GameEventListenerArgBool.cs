// ----------------------------------------------------------------------------
// SOOP Framework
// 
// Original
// Author:  Ryan Hipple
// Date:    10/04/17
//
// Author:  Scott Cameron
// Date:    03/14/18
// ----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.Events;

namespace SOOP.Events
{
    public class GameEventListenerArgBool : MonoBehaviour
    {
#if UNITY_EDITOR
        [TextArea(1,10)]
        public string DeveloperDescription = "";
#endif
        [System.Serializable]
        public class ResponseEvent : UnityEvent<bool> { }

        [Tooltip("Event to register with.")]
        public GameEventArgBool Event;

        [Tooltip("Response with bool argument to invoke when Event is raised.")]
        public ResponseEvent Response;


        private void OnEnable()
        {
            Event.RegisterListener(this);
        }

        private void OnDisable()
        {
            Event.UnregisterListener(this);
        }

        public void OnEventRaised(bool value)
        {
            Response.Invoke(value);
        }
    }
}