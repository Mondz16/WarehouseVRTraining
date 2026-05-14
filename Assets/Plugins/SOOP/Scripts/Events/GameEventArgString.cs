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
using System.Collections.Generic;
using UnityEngine.Events;

namespace SOOP.Events
{
    [CreateAssetMenu(menuName = "SOOP/Game Event/String Argument")]
    public class GameEventArgString : ScriptableObject
    {

#if UNITY_EDITOR
        [Multiline]
        public string DeveloperDescription = "";
#endif

        /// <summary>
        /// The list of listeners that this event will notify if it is raised.
        /// </summary>
        [HideInInspector]
        public readonly List<GameEventListenerArgString> eventListeners =
            new List<GameEventListenerArgString>();

        private readonly List<UnityAction<string>> unityActionListeners = new List<UnityAction<string>>();
        
        public void Raise(string value)
        {
            for (int i = eventListeners.Count - 1; i >= 0; i--)
                eventListeners[i].OnEventRaised(value);
            
            for (int i = unityActionListeners.Count - 1; i >= 0; i--)
                unityActionListeners[i].Invoke(value);
        }
        public void RegisterListener(UnityAction<string> unityAction)
        {
            if(!unityActionListeners.Contains(unityAction))
                unityActionListeners.Add(unityAction);
        }
        public void RegisterListener(GameEventListenerArgString listener)
        {
            if (!eventListeners.Contains(listener))
                eventListeners.Add(listener);
        }
        public void UnregisterListener(UnityAction<string> unityAction)
        {
            if (unityActionListeners.Contains(unityAction))
                unityActionListeners.Remove(unityAction);
        }
        public void UnregisterListener(GameEventListenerArgString listener)
        {
            if (eventListeners.Contains(listener))
                eventListeners.Remove(listener);
        }
    }
}