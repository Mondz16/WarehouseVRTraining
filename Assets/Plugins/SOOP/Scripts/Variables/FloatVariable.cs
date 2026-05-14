// ----------------------------------------------------------------------------
// Unite 2017 - Game Architecture with Scriptable Objects
// 
// Author: Ryan Hipple
// Date:   10/04/17
// Modified by: Scott Cameron
// Date:   11/08/19
// ----------------------------------------------------------------------------

using UnityEngine;
using System;

namespace SOOP.Variables
{
    [CreateAssetMenu(menuName = "SOOP/Float Variable")]
    public class FloatVariable : ScriptableObject
    {
#if UNITY_EDITOR
        [Multiline]
        public string DeveloperDescription = "";
#endif
        public Action<float> OnChanged;

        [SerializeField]
        private float _value;
        public float Value { get => _value; }


        public void SetValue(float value)
        {
            _value = value;
            OnChanged?.Invoke(_value);
        }

        public void SetValue(FloatVariable value)
        {
            SetValue(value.Value);
        }


        public void ApplyChange(float amount)
        {
            _value += amount;
            OnChanged?.Invoke(_value);
        }

        public void ApplyChange(FloatVariable amount)
        {
            ApplyChange(amount.Value);
        }
    }
}