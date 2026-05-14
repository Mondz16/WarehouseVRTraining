using UnityEngine;
using UnityEngine.Events;
using SOOP.Variables;

public class OnFloatVariableChanged : MonoBehaviour
{
    [SerializeField]
    private FloatVariable _variable;

    [System.Serializable]
    private class ResponseFloat : UnityEvent<float> { }
    [SerializeField]
    private ResponseFloat OnChanged;


    private void OnEnable()
    {
        _variable.OnChanged += OnVariableChanged;
    }

    private void OnDisable()
    {
        _variable.OnChanged -= OnVariableChanged;
    }


    private void OnVariableChanged(float value)
    {
        OnChanged?.Invoke(value);
    }
}
