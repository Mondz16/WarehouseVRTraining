using UnityEngine;
using UnityEngine.Events;
using SOOP.Variables;

public class OnBoolVariableChanged : MonoBehaviour
{
    [SerializeField]
    private BoolVariable _variable;

    [System.Serializable]
    private class ResponseBool : UnityEvent<bool> { }
    [SerializeField]
    private ResponseBool OnChanged;


    private void OnEnable()
    {
        _variable.OnChanged += OnVariableChanged;
    }

    private void OnDisable()
    {
        _variable.OnChanged -= OnVariableChanged;
    }


    private void OnVariableChanged(bool value)
    {
        OnChanged?.Invoke(value);
    }
}
