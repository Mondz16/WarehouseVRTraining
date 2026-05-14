// ----------------------------------------------------------------------------
// SOOP Framework
// 
// Original
// Author:	Ryan Hipple
// Date:	10/04/17
//
// Author: 	Scott Cameron
// Date:	04/13/18
// ----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;

namespace SOOP.Events
{
    [CustomEditor(typeof(GameEventArgFloat))]
    public class EventArgFloatEditor : Editor
    {
        private float EditorValue;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUI.enabled = Application.isPlaying;

            EditorValue = EditorGUILayout.FloatField("Editor Value", EditorValue);

            GameEventArgFloat e = target as GameEventArgFloat;
            if (GUILayout.Button("Raise"))
                e.Raise(EditorValue);


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