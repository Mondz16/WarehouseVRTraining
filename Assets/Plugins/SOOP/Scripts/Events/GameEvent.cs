// ----------------------------------------------------------------------------
// Unite 2017 - Game Architecture with Scriptable Objects
// 
// Author: Ryan Hipple
// Date:   10/04/17
//
// Modified by: Scott H Cameron
// Date: 10/18/18
//
// Modified by: Kendrick O Villaruel - UnityAction parameters for Registration
// Date: 06/16/22
// ----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SOOP.Events
{
    [CreateAssetMenu(menuName = "SOOP/Game Event/Game Event")]
    public class GameEvent : ScriptableObject
    {

#if UNITY_EDITOR
        [TextArea(4, 10)]
        public string DeveloperDescription = "";
#endif

        /// <summary>
        /// The list of listeners that this event will notify if it is raised.
        /// </summary>
        [HideInInspector]
        public readonly List<GameEventListener> eventListeners =
            new List<GameEventListener>();

        private readonly List<UnityAction> unityActionListeners = new List<UnityAction>();
        
        public void Raise()
        {
            for (int i = eventListeners.Count - 1; i >= 0; i--)
            {
              //  Debug.LogFormat("[GameEvent] {0} raised with listener '{1}'", eventListeners[i].Event.name, eventListeners[i].name);
                eventListeners[i].OnEventRaised();
            }
            
            for (int i = unityActionListeners.Count - 1; i >= 0; i--)
            {
                //  Debug.LogFormat("[GameEvent] {0} raised with listener '{1}'", eventListeners[i].Event.name, eventListeners[i].name);
                unityActionListeners[i].Invoke();
            }
        }

        public void RegisterListener(UnityAction unityAction)
        {
            if(!unityActionListeners.Contains(unityAction))
                unityActionListeners.Add(unityAction);
        }
        public void RegisterListener(GameEventListener listener)
        {
            if (!eventListeners.Contains(listener))
                eventListeners.Add(listener);
        }

        public void UnregisterListener(UnityAction unityAction)
        {
            if (unityActionListeners.Contains(unityAction))
                unityActionListeners.Remove(unityAction);
        }
        public void UnregisterListener(GameEventListener listener)
        {
            if (eventListeners.Contains(listener))
                eventListeners.Remove(listener);
        }
    }
}