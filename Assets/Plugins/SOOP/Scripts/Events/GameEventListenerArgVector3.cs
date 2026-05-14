// ----------------------------------------------------------------------------
// SOOP Framework
// 
// Original
// Author:  Ryan Hipple
// Date:    10/04/17
//
// Author:  Scott Cameron
// Date:    05/01/18
// ----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.Events;

namespace SOOP.Events
{
    public class GameEventListenerArgVector3 : MonoBehaviour
    {
#if UNITY_EDITOR
        [Multiline]
        public string DeveloperDescription = "";
#endif
        [System.Serializable]
        public class ResponseEvent : UnityEvent<Vector3> { }

        [Tooltip("Event to register with.")]
        public GameEventArgVector3 Event;

        [Tooltip("Response with Vector3 argument to invoke when Event is raised.")]
        public ResponseEvent Response;


        private void OnEnable()
        {
            Event.RegisterListener(this);
        }

        private void OnDisable()
        {
            Event.UnregisterListener(this);
        }

        public void OnEventRaised(Vector3 value)
        {
            Response.Invoke(value);
        }
    }
}