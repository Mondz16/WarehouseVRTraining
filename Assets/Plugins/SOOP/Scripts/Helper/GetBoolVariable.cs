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
    public class GetBoolVariable : MonoBehaviour
    {
        [SerializeField]
        private BoolVariable _boolVariable = null;
        [HideInInspector]
        public bool Value;


        private void OnEnable()
        {
            _boolVariable.OnChanged += OnVariableValueChanged;
        }

        private void OnDisable()
        {
            _boolVariable.OnChanged -= OnVariableValueChanged;
        }

        private void Awake()
        {
            // set the initial value
            Value = _boolVariable.Value;
        }


        /// <summary>
        /// When the value changes, update the Value field.
        /// </summary>
        /// <param name="value"></param>
        private void OnVariableValueChanged(bool value)
        {
            Value = value;
        }
    }
}



#if UNITY_EDITOR
/// <summary>
/// Used to make the Value field read-only in the Inspector.
/// </summary>
[CustomEditor(typeof(SOOP.Helpers.GetBoolVariable))]
public class GetBoolVariableEditor : Editor
{
    //private SOOP.Helpers.GetBoolVariable _myScript;
    private SerializedProperty _boolValue;


    void OnEnable()
    {
        // fetch the objects from the GameObject script to display in the inspector
        _boolValue = serializedObject.FindProperty("Value");
    }

    public override void OnInspectorGUI()
    {
        //_myScript = (SOOP.Helpers.GetBoolVariable)target;

        // TODO: replace this with custom fields?
        DrawDefaultInspector();

        SOOP.Helpers.GetBoolVariable scenarioData = target as SOOP.Helpers.GetBoolVariable;

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.PropertyField(_boolValue, new GUIContent("Value"), GUILayout.Height(20));
        EditorGUI.EndDisabledGroup();

        // Apply changes to the serializedProperty - always do this at the end of OnInspectorGUI.
        serializedObject.ApplyModifiedProperties();
    }
}
#endif