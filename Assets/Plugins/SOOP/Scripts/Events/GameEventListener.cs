// ----------------------------------------------------------------------------
// Unite 2017 - Game Architecture with Scriptable Objects
// 
// Author: Ryan Hipple
// Date:   10/04/17
// ----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace SOOP.Events
{
    public class GameEventListener : MonoBehaviour
    {
#if UNITY_EDITOR
        // [TextArea(1, 10)]
        public string DeveloperDescription = "";

        // [SerializeField]
        // private Developer _developer; 
        //
        // [System.Serializable]
        // public class Developer
        // {
        //     public string Description;
        // }
        //
#endif
        [Tooltip("Event to register with.")]
        public GameEvent Event;

        [Tooltip("Response to invoke when Event is raised.")]
        public UnityEvent Response;

        private void OnEnable()
        {
            Event.RegisterListener(this);
        }

        private void OnDisable()
        {
            Event.UnregisterListener(this);
        }

        public void OnEventRaised()
        {
            Response.Invoke();
        }
    }
}