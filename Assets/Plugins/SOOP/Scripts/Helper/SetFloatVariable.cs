// ----------------------------------------------------------------------------
// SOOP Framework
// 
// Author: Scott Cameron
// Date:   03/23/18
// ----------------------------------------------------------------------------

using UnityEngine;
using SOOP.Variables;

namespace SOOP.Helpers
{
    public class SetFloatVariable : MonoBehaviour
    {
        [SerializeField]
        private FloatVariable _floatVariable = null;

        public void Set(float value)
        {
            _floatVariable.SetValue(value);
        }
    }
}