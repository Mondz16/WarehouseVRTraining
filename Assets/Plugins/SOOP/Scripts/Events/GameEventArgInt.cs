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
using System.Collections.Generic;
using UnityEngine.Events;

namespace SOOP.Events
{
    [CreateAssetMenu(menuName = "SOOP/Game Event/Int Argument")]
    public class GameEventArgInt : ScriptableObject
    {

#if UNITY_EDITOR
        [Multiline]
        public string DeveloperDescription = "";
#endif

        /// <summary>
        /// The list of listeners that this event will notify if it is raised.
        /// </summary>
        [HideInInspector]
        public readonly List<GameEventListenerArgInt> eventListeners =
            new List<GameEventListenerArgInt>();
        
        private readonly List<UnityAction<int>> unityActionListeners = new List<UnityAction<int>>();
        
        public void Raise(int value)
        {
            for (int i = eventListeners.Count - 1; i >= 0; i--)
                eventListeners[i].OnEventRaised(value);
            
            for (int i = unityActionListeners.Count - 1; i >= 0; i--)
                unityActionListeners[i].Invoke(value);
        }
        public void RegisterListener(UnityAction<int> unityAction)
        {
            if(!unityActionListeners.Contains(unityAction))
                unityActionListeners.Add(unityAction);
        }
        public void RegisterListener(GameEventListenerArgInt listener)
        {
            if (!eventListeners.Contains(listener))
                eventListeners.Add(listener);
        }
        public void UnregisterListener(UnityAction<int> unityAction)
        {
            if (unityActionListeners.Contains(unityAction))
                unityActionListeners.Remove(unityAction);
        }
        public void UnregisterListener(GameEventListenerArgInt listener)
        {
            if (eventListeners.Contains(listener))
                eventListeners.Remove(listener);
        }
    }
}