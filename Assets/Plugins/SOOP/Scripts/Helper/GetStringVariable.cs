// ----------------------------------------------------------------------------
// SOOP Framework
// 
// Author: Scott Cameron
// Date:   11/08/19
// ----------------------------------------------------------------------------

using UnityEngine;
using SOOP.Variables;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SOOP.Helpers
{
    public class GetStringVariable : MonoBehaviour
    {
        [SerializeField]
        private StringVariable _stringVariable = null;
        [HideInInspector]
        public string Value;


        private void OnEnable()
        {
            _stringVariable.OnChanged += OnVariableValueChanged;
        }

        private void OnDisable()
        {
            _stringVariable.OnChanged -= OnVariableValueChanged;
        }

        private void Awake()
        {
            // set the initial value
            Value = _stringVariable.Value;
        }


        /// <summary>
        /// When the value changes, update the Value field.
        /// </summary>
        /// <param name="value"></param>
        private void OnVariableValueChanged(string value)
        {
            Value = value;
        }
    }
}



#if UNITY_EDITOR
/// <summary>
/// Used to make the Value field read-only in the Inspector.
/// </summary>
[CustomEditor(typeof(SOOP.Helpers.GetStringVariable))]
public class GetStringVariableEditor : Editor
{
    //private SOOP.Helpers.GetStringVariable _myScript;
    private SerializedProperty _stringValue;


    void OnEnable()
    {
        // fetch the objects from the GameObject script to display in the inspector
        _stringValue = serializedObject.FindProperty("Value");
    }

    public override void OnInspectorGUI()
    {
        //_myScript = (SOOP.Helpers.GetStringVariable)target;

        // TODO: replace this with custom fields?
        DrawDefaultInspector();

        SOOP.Helpers.GetStringVariable scenarioData = target as SOOP.Helpers.GetStringVariable;

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.PropertyField(_stringValue, new GUIContent("Value"), GUILayout.Height(20));
        EditorGUI.EndDisabledGroup();

        // Apply changes to the serializedProperty - always do this at the end of OnInspectorGUI.
        serializedObject.ApplyModifiedProperties();
    }
}
#endif