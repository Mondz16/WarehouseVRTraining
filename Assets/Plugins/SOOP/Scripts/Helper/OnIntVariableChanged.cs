using UnityEngine;
using UnityEngine.Events;
using SOOP.Variables;

public class OnIntVariableChanged : MonoBehaviour
{
    [SerializeField]
    private IntVariable _variable;

    [System.Serializable]
    private class ResponseInt : UnityEvent<int> { }
    [SerializeField]
    private ResponseInt OnChanged;

    private void OnEnable()
    {
        _variable.OnChanged += OnVariableChanged;
    }

    private void OnDisable()
    {
        _variable.OnChanged -= OnVariableChanged;
    }


    private void OnVariableChanged(int value)
    {
        OnChanged?.Invoke(value);
    }
}
