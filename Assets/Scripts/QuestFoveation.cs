using UnityEngine;

// Turns on Quest fixed foveated rendering. The Meta XR Foveation OpenXR feature is enabled, but with no
// OVRManager in the scene nothing sets a level, so it stays Off. Foveation only applies while URP renders
// straight into the eye buffers, i.e. with Depth/Opaque Texture off in Performance URP Config.
public class QuestFoveation : MonoBehaviour
{
    private const OVRManager.FoveatedRenderingLevel MaxLevel = OVRManager.FoveatedRenderingLevel.High;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOnStartup()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var go = new GameObject(nameof(QuestFoveation));
        DontDestroyOnLoad(go);
        go.AddComponent<QuestFoveation>();
#endif
    }

    private void Start() => Apply();

    // Cheap to repeat, and covers the runtime resetting the level while the app was paused.
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) Apply();
    }

    private static void Apply()
    {
        // Dynamic: the runtime only raises foveation (up to MaxLevel) when the GPU needs it.
        OVRManager.foveatedRenderingLevel = MaxLevel;
        OVRManager.useDynamicFoveatedRendering = true;
    }
}
