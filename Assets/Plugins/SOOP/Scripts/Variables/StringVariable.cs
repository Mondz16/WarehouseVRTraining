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
    [CreateAssetMenu(menuName = "SOOP/String Variable")]
    public class StringVariable : ScriptableObject
    {
#if UNITY_EDITOR
        [Multiline]
        public string DeveloperDescription = "";
#endif
        public Action<string> OnChanged;

        [SerializeField]
        private string _value = "";

        public string Value
        {
            get => _value;
            set
            {
                _value = value;
                OnChanged?.Invoke(value);
            }
        }
    }
}