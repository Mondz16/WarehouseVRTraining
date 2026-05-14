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
    [CreateAssetMenu(menuName = "SOOP/Int Variable")]
    public class IntVariable : ScriptableObject
    {
#if UNITY_EDITOR
        [TextArea(3,10)]
        public string DeveloperDescription = "";
#endif
        public Action<int> OnChanged;

        [SerializeField]
        private int _value;
        public int Value { get => _value; }


        public void SetValue(int value)
        {
            _value = value;
            OnChanged?.Invoke(_value);
        }

        public void SetValue(IntVariable value)
        {
            SetValue(value.Value);
        }


        public void ApplyChange(int amount)
        {
            _value += amount;
            OnChanged?.Invoke(_value);
        }

        public void ApplyChange(IntVariable amount)
        {
            ApplyChange(amount.Value);
        }
    }
}