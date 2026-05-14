// ----------------------------------------------------------------------------
// SOOP Framework
// 
// Author: Scott Cameron
// Date:   03/23/18
// ----------------------------------------------------------------------------

using UnityEngine;
using System;

namespace SOOP.Variables
{
    [CreateAssetMenu(menuName = "SOOP/Bool Variable")]
    public class BoolVariable : ScriptableObject
    {
#if UNITY_EDITOR
        [TextArea(3, 10)]
        public string DeveloperDescription = "";
#endif
        public Action<bool> OnChanged;

        [SerializeField]
        private bool _value;

        private void OnEnable()
        {
            hideFlags = HideFlags.DontUnloadUnusedAsset;
        }

        public bool Value
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