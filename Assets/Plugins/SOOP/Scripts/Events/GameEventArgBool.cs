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
using System.Collections.Generic;
using UnityEngine.Events;

namespace SOOP.Events
{
    [CreateAssetMenu(menuName = "SOOP/Game Event/Bool Argument")]
    public class GameEventArgBool : ScriptableObject
    {

#if UNITY_EDITOR
        [TextArea(3,10)]
        public string DeveloperDescription = "";
#endif

        /// <summary>
        /// The list of listeners that this event will notify if it is raised.
        /// </summary>
        [HideInInspector]
        public readonly List<GameEventListenerArgBool> eventListeners =
            new List<GameEventListenerArgBool>();

        private readonly List<UnityAction<bool>> unityActionListeners = new List<UnityAction<bool>>();
        
        public void Raise(bool value)
        {
            for (int i = eventListeners.Count - 1; i >= 0; i--)
                eventListeners[i].OnEventRaised(value);
            
            for (int i = unityActionListeners.Count - 1; i >= 0; i--)
                unityActionListeners[i].Invoke(value);
        }
        public void RegisterListener(UnityAction<bool> unityAction)
        {
            if(!unityActionListeners.Contains(unityAction))
                unityActionListeners.Add(unityAction);
        }
        public void RegisterListener(GameEventListenerArgBool listener)
        {
            if (!eventListeners.Contains(listener))
                eventListeners.Add(listener);
        }
        public void UnregisterListener(UnityAction<bool> unityAction)
        {
            if (unityActionListeners.Contains(unityAction))
                unityActionListeners.Remove(unityAction);
        }
        public void UnregisterListener(GameEventListenerArgBool listener)
        {
            if (eventListeners.Contains(listener))
                eventListeners.Remove(listener);
        }
    }
}