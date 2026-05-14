// ----------------------------------------------------------------------------
// Unite 2017 - Game Architecture with Scriptable Objects
// 
// Author: Ryan Hipple
// Date:   10/04/17
//
// Author:  Scott Cameron
// Date:    05/01/19
// ----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

namespace SOOP.Events
{
    public class UnityEventRaiser : MonoBehaviour
    {
        public UnityEvent OnEnableEvent;

        public void OnEnable()
        {
            StartCoroutine(OnEndOfFrame());
        }

        private IEnumerator OnEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
            OnEnableEvent.Invoke();
        }
    }
}