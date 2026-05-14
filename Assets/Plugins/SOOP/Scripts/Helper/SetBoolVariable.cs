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
    public class SetBoolVariable : MonoBehaviour
    {
        [SerializeField]
        private BoolVariable _boolVariable = null;

        public void Set(int value) { Set(BoolParser.GetValue(value)); }
        public void Set(bool value)
        {
            _boolVariable.Value = value;
        }
    }
}