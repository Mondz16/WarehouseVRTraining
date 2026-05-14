using UnityEngine;
using Valari.Services;

public class SampleBind : MonoBehaviour
{
    public static SampleBind Services { get { if (_sampleBind == null) _sampleBind = Game.Services.Get<SampleBind>(); return _sampleBind; } }
    private static SampleBind _sampleBind;

    public void SampleMethod()
    {
        Debug.Log("Sample Bind: Game Binding is working!");
    }
}

