using UnityEngine;
using UnityEngine.Events;
using SOOP.Variables;

public class OnStringVariableChanged : MonoBehaviour
{
    [SerializeField]
    private StringVariable _variable;

    [System.Serializable]
    private class ResponseString : UnityEvent<string> { }
    [SerializeField]
    private ResponseString OnChanged;


    private void OnEnable()
    {
        _variable.OnChanged += OnVariableChanged;
    }

    private void OnDisable()
    {
        _variable.OnChanged -= OnVariableChanged;
    }


    private void OnVariableChanged(string value)
    {
        OnChanged?.Invoke(value);
    }
}
