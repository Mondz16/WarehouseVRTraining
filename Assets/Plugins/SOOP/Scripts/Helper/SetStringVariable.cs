// ----------------------------------------------------------------------------
// SOOP Framework
// 
// Author: Scott Cameron
// Date:   03/22/18
// ----------------------------------------------------------------------------

using UnityEngine;
using SOOP.Variables;

namespace SOOP.Helpers
{
    public class SetStringVariable : MonoBehaviour
    {
        [SerializeField]
        private StringVariable _stringVariable = null;

        public void Set(string value)
        {
            _stringVariable.Value = value;
        }
    }
}