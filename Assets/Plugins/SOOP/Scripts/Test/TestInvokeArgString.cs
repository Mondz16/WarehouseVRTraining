using UnityEngine;
using UnityEngine.Events;

public class TestInvokeArgString : MonoBehaviour
{
    [System.Serializable]
    public class ResponseEvent : UnityEvent<string> { }
    public ResponseEvent eventTest = null;

    public string stringArg = "Easy peasy lemon squeezy.";


    public void Button_OnClick()
    {
        eventTest.Invoke(stringArg);
    }
}
