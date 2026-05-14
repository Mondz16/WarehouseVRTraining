// ----------------------------------------------------------------------------
// Unite 2017 - Game Architecture with Scriptable Objects
// 
// Author: Ryan Hipple
// Date:   10/04/17
//
// Modified by: Scott H Cameronm
// Date: 10/18/18
// ----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;

namespace SOOP.Events
{
    [CustomEditor(typeof(GameEvent))]
    public class EventEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUI.enabled = Application.isPlaying;

            GameEvent e = target as GameEvent;
            if (GUILayout.Button("Raise"))
                e.Raise();


            // list all the registered game event listeners for this game event at runtime in the editor
            if (Application.isEditor && Application.isPlaying
                && e.eventListeners.Count > 0)
            {
                GUILayout.Space(20);
                GUILayout.BeginVertical(EditorStyles.helpBox);

                GUI.color = Color.white;
                GUILayout.Label("Registered GameEvent Listeners", EditorStyles.boldLabel);
                foreach (var item in e.eventListeners)
                {
                    if (GUILayout.Button(item.gameObject.name))
                        Selection.activeGameObject = item.gameObject;
                }

                GUILayout.EndVertical();
            }
        }
    }
}