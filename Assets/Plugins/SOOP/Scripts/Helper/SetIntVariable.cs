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
    public class SetIntVariable : MonoBehaviour
    {
        [SerializeField]
        private IntVariable _intVariable = null;

        public void Set(int value)
        {
            _intVariable.SetValue(value);
        }
    }
}