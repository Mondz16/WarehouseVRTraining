using UnityEngine;
using Valari.Managers;

public class XRTeleportHandler : MonoBehaviour
{
    private void OnEnable()
    {
        TutorialManager.OnTeleportPlayerEvent += TeleportPlayer;
    }

    private void OnDisable()
    {
        TutorialManager.OnTeleportPlayerEvent -= TeleportPlayer;
    }
    
    private void TeleportPlayer(Vector3 pos, Quaternion rotation)
    {
        transform.position = pos;
        transform.rotation = rotation;
    }
}
