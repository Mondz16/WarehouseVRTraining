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
    public class GetFloatVariable : MonoBehaviour
    {
        [SerializeField]
        private FloatVariable _floatVariable = null;
        [HideInInspector]
        public float Value;


        private void OnEnable()
        {
            _floatVariable.OnChanged += OnVariableValueChanged;
        }

        private void OnDisable()
        {
            _floatVariable.OnChanged -= OnVariableValueChanged;
        }

        private void Awake()
        {
            // set the initial value
            Value = _floatVariable.Value;
        }


        /// <summary>
        /// When the value changes, update the Value field.
        /// </summary>
        /// <param name="value"></param>
        private void OnVariableValueChanged(float value)
        {
            Value = value;
        }
    }
}



#if UNITY_EDITOR
/// <summary>
/// Used to make the Value field read-only in the Inspector.
/// </summary>
[CustomEditor(typeof(SOOP.Helpers.GetFloatVariable))]
public class GetFloatVariableEditor : Editor
{
    //private SOOP.Helpers.GetFloatVariable _myScript;
    private SerializedProperty _floatValue;


    void OnEnable()
    {
        // fetch the objects from the GameObject script to display in the inspector
        _floatValue = serializedObject.FindProperty("Value");
    }

    public override void OnInspectorGUI()
    {
        //_myScript = (SOOP.Helpers.GetFloatVariable)target;

        // TODO: replace this with custom fields?
        DrawDefaultInspector();

        SOOP.Helpers.GetFloatVariable scenarioData = target as SOOP.Helpers.GetFloatVariable;

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.PropertyField(_floatValue, new GUIContent("Value"), GUILayout.Height(20));
        EditorGUI.EndDisabledGroup();

        // Apply changes to the serializedProperty - always do this at the end of OnInspectorGUI.
        serializedObject.ApplyModifiedProperties();
    }
}
#endif