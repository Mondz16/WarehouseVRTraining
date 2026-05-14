using UnityEngine;
using System;
using System.Collections;

namespace Valari.Utilities
{
    public static class Delay
    {
        /// <summary>
        /// Like MonoBehavior.Invoke("FunctionName", 2f); but can include params. Must provide an instantiated MonoBehaviour instance to execute on!
        /// Usage:
        /// 	Utils.RunLater(this,2f,()=> FunctionName(true, Vector.one, "or whatever parameters you want"));
        /// </summary>
        public static void RunLater(MonoBehaviour instance, float waitSeconds, Action method)
        {
            if (waitSeconds < 0 || method == null)
                return;

            instance.StartCoroutine(IERunLater(method, waitSeconds));
        }

        private static IEnumerator IERunLater(Action method, float waitSeconds)
        {
            yield return new WaitForSeconds(waitSeconds);
            method();
        }

        public static void RunAtEndOfFrame(Action method, MonoBehaviour instance)
        {
            if (method == null)
                return;

            instance.StartCoroutine(IERunAtEndOfFrame(method));
        }
        private static IEnumerator IERunAtEndOfFrame(Action method)
        {
            yield return new WaitForEndOfFrame();
            method();
        }
    }
}

