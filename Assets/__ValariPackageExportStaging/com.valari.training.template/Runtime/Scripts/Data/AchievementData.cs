using UnityEngine;
using System;

[Serializable]
public class AchievementData
{
    [field: SerializeField] public TrainingID ID { get; set; }
    [field: SerializeField] public string Title { get; set; }
    [field: SerializeField] [TextArea(0, 50)] public string Description { get; set; }
    [field: SerializeField] public Sprite Icon { get; set; }
    [field: SerializeField] public GameObject TrophyPrefab { get; set; }
    [field: SerializeField] public AudioClip VoiceOver { get; set; }
}

public enum TrainingID
{
    None, WarehouseSafety, OrderPicking, PackingAndDispatch, RouteOptimisation
}

