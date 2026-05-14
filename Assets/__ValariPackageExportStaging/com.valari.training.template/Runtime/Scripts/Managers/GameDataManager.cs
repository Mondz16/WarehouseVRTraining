using System;
using Valari.Collections;
using UnityEngine;
using Valari.Services;

namespace Valari.Managers
{
    public class GameDataManager : MonoBehaviour
    {
        public static GameDataManager Services
        {
            get
            {
                if (_ == null)
                    _ = Game.Services.Get<GameDataManager>();
                return _;
            }
        }

        private static GameDataManager _;

        [SerializeField] private AchievementDataCollection _achievementDataCollection;
        [field: SerializeField] public bool StartFromBeginning { get; set; }
        [field: SerializeField] public bool IsMaintenanceModeEnabled { get; set; }

        private void Start()
        {
            // _achievementDataCollection.SetAchievementStateByID(TrainingID.SafetyEquipment , 
            //     PlayerPrefs.HasKey(PlayerPrefsKey.SAFETY_EQUIPMENT_TRAINING) && 
            //     PlayerPrefs.GetInt(PlayerPrefsKey.SAFETY_EQUIPMENT_TRAINING) == 1);
            //
            // _achievementDataCollection.SetAchievementStateByID(TrainingID.MaintenanceTraining , 
            //     PlayerPrefs.HasKey(PlayerPrefsKey.ELEVATOR_MAINTENANCE_TRAINING) && 
            //     PlayerPrefs.GetInt(PlayerPrefsKey.ELEVATOR_MAINTENANCE_TRAINING) == 1);
        }

        public AchievementDataCollection GetAchievementDataCollection => _achievementDataCollection;
    }
}

