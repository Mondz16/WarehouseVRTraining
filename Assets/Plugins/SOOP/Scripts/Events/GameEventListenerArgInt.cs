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
    public class GameEventListenerArgInt : MonoBehaviour
    {
#if UNITY_EDITOR
        [Multiline]
        public string DeveloperDescription = "";
#endif
        [System.Serializable]
        public class ResponseEvent : UnityEvent<int> { }

        [Tooltip("Event to register with.")]
        public GameEventArgInt Event;

        [Tooltip("Response with int argument to invoke when Event is raised.")]
        public ResponseEvent Response;


        private void OnEnable()
        {
            Event.RegisterListener(this);
        }

        private void OnDisable()
        {
            Event.UnregisterListener(this);
        }

        public void OnEventRaised(int value)
        {
            Response.Invoke(value);
        }
    }
}