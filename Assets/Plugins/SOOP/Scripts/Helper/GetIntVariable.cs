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
    public class GetIntVariable : MonoBehaviour
    {
        [SerializeField]
        private IntVariable _intVariable = null;
        [HideInInspector]
        public int Value;


        private void OnEnable()
        {
            _intVariable.OnChanged += OnVariableValueChanged;
        }

        private void OnDisable()
        {
            _intVariable.OnChanged -= OnVariableValueChanged;
        }

        private void Awake()
        {
            // set the initial value
            Value = _intVariable.Value;
        }


        /// <summary>
        /// When the value changes, update the Value field.
        /// </summary>
        /// <param name="value"></param>
        private void OnVariableValueChanged(int value)
        {
            Value = value;
        }
    }
}



#if UNITY_EDITOR
/// <summary>
/// Used to make the Value field read-only in the Inspector.
/// </summary>
[CustomEditor(typeof(SOOP.Helpers.GetIntVariable))]
public class GetIntVariableEditor : Editor
{
    //private SOOP.Helpers.GetIntVariable _myScript;
    private SerializedProperty _intValue;


    void OnEnable()
    {
        // fetch the objects from the GameObject script to display in the inspector
        _intValue = serializedObject.FindProperty("Value");
    }

    public override void OnInspectorGUI()
    {
        //_myScript = (SOOP.Helpers.GetIntVariable)target;

        // TODO: replace this with custom fields?
        DrawDefaultInspector();

        SOOP.Helpers.GetIntVariable scenarioData = target as SOOP.Helpers.GetIntVariable;

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.PropertyField(_intValue, new GUIContent("Value"), GUILayout.Height(20));
        EditorGUI.EndDisabledGroup();

        // Apply changes to the serializedProperty - always do this at the end of OnInspectorGUI.
        serializedObject.ApplyModifiedProperties();
    }
}
#endif