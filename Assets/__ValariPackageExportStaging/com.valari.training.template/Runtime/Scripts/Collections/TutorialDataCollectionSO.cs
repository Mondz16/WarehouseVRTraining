using System;
using System.Collections.Generic;
using Pathfinding.Serialization.JsonFx;
using Sirenix.OdinInspector;
using UnityEngine;
using Valari.Data;

[CreateAssetMenu(menuName = "TutorialDataCollectionSO", fileName = "TutorialDataCollectionSO")]
public class TutorialDataCollectionSO : ScriptableObject
{
    [Serializable]
    public class TutorialDataGroup
    {
        [field: SerializeField] public TrainingID ID { get; set; }
        [field: SerializeField] public List<TutorialData> DataList { get; set; }

        [Button()]
        public void GetDataToJSON()
        {
            var data = JsonWriter.Serialize(DataList);
            Debug.Log($"#{GetType().Name}# {ID} -> {data}");
        }
    
        [Serializable]
        private class TutorialDataWrapper { public List<TutorialData> stages; }

        [Button()]
        public void Serialize(string json)
        {
            var wrapper = JsonUtility.FromJson<TutorialDataWrapper>(json);
            foreach (TutorialData data in wrapper.stages)
            {
                
            }
        }
    }

    [SerializeField] private List<TutorialDataGroup> _tutorialDataList = new List<TutorialDataGroup>();

    public List<TutorialData> GetDataListByID(TrainingID id)
    {
        return _tutorialDataList.Find(x => x.ID == id).DataList;
    }

}

