using System;
using UnityEngine;

namespace Valari.Collections
{
    [CreateAssetMenu(fileName = "AchievementDataCollection", menuName = "AchievementDataCollection", order = 0)]
    public class AchievementDataCollection : ScriptableObject
    {
        [Serializable]
        public class Achievement
        {
            public AchievementData Data;
            public bool Completed;
        }

        [SerializeField] private Achievement _safetyEquipmentAchievementData;
        [SerializeField] private Achievement _elevatorMaintenanceTrainingData;

        public bool IsAchievementComplete(TrainingID id)
        {

            switch (id)
            {
                case TrainingID.WarehouseSafety:
                    return _safetyEquipmentAchievementData.Completed;

            }

            return false;
        }

        public void SetAchievementStateByID(TrainingID id, bool isComplete)
        {
            switch (id)
            {
                case TrainingID.WarehouseSafety:
                    _safetyEquipmentAchievementData.Completed = isComplete;
                    break;
            }
        }

        public AchievementData GetAchievementDataByID(TrainingID id)
        {
            switch (id)
            {
                case TrainingID.WarehouseSafety:
                    return _safetyEquipmentAchievementData.Data;
                case TrainingID.None:
                    return null;
            }

            return null;
        }
    }
}

