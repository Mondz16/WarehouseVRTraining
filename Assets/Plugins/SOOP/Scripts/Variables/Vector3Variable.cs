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
    [CreateAssetMenu(menuName = "SOOP/Vector3 Variable")]
    public class Vector3Variable : ScriptableObject
    {
#if UNITY_EDITOR
        [TextArea(4,10)]
        public string DeveloperDescription = "";
#endif
        public Action<Vector3> OnChanged;

        [SerializeField]
        private Vector3 _value = Vector3.zero;

        private void OnEnable()
        {
            hideFlags = HideFlags.DontUnloadUnusedAsset;
        }
        
        public Vector3 Value
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