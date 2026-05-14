using UnityEngine;
using UnityEngine.Events;
using SOOP.Variables;

public class OnBoolVariableChange : MonoBehaviour
{
    public bool IsEnabled = false;
    public BoolVariable Variable = null;

    public enum Value { True, False };
    public Value EquatesTo = Value.True;

    [Header("Triggered Events")]
    public UnityEvent OnChangeValueEquals;

    private bool _state;


    // Use this for initialization
    private void Start()
    {
        _state = Variable.Value;
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsEnabled || OnChangeValueEquals == null)
            return;


        if (Variable.Value != _state)
        {
            switch (Variable.Value)
            {
                case true:
                    if (EquatesTo.Equals(Value.True))
                    {
                        OnChangeValueEquals?.Invoke();
                        IsEnabled = false;
                    }
                    break;

                case false:
                    if (EquatesTo.Equals(Value.False))
                    {
                        OnChangeValueEquals?.Invoke();
                        IsEnabled = false;
                    }
                    break;
            }

            _state = Variable.Value;
        }
    }


    public void SetIsEnabled(bool value)
    {
        IsEnabled = value;
    }
}
