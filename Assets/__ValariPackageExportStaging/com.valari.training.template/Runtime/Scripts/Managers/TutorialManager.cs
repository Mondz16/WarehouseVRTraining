using System;
using System.Collections.Generic;
using Meta.XR.BuildingBlocks.AIBlocks;
using ObjectiveManagerandQuestEngine;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Valari.UI;
using Valari.Utilities;

namespace Valari.Managers
{
    public class TutorialManager : MonoBehaviour
    {
        [SerializeField] private LLMHandler _llmHandler;
        [SerializeField] private Transform _startingPosition;
        [SerializeField] private List<TutorialModalUI> _guidedInspectionList;
        [SerializeField] private List<TutorialModalUI> _foundryTrainingModalUIList;
        [SerializeField] private List<TutorialModalUI> _permanentCastMoldingTrainingModalUIList;
        [SerializeField] private List<TutorialModalUI> _dieCastingTrainingModalUIList;
        [SerializeField] private UnityEvent _onGameOverEvent;

        private TrainingID _trainingID = TrainingID.None;
        private int _trainingIndex = 0;
        private List<TutorialModalUI> _currentTutorial = new List<TutorialModalUI>();
        private GameDataManager _gameDataManager => GameDataManager.Services;


        public static Action<Vector3, Quaternion> OnTeleportPlayerEvent { get; set; }
        public static Action OnTutorialResetEvent { get; set; }
        public static Action<TrainingID> OnTrainingStartedEvent { get; set; }
        public static Action OnTrainingCompleteEvent { get; set; }
        public static Action OnUpdateMistakeCountEvent { get; set; }
        public static Action<string> OnShowObjectByTriggerNameEvent { get; set; }
        public static Action<string> OnHideObjectByTriggerNameEvent { get; set; }

        private void Start()
        {
            // OnPlayOfficeAmbianceBGM();
            OnTutorialReset(_guidedInspectionList);
            OnTutorialReset(_foundryTrainingModalUIList);
            OnTutorialReset(_permanentCastMoldingTrainingModalUIList);
            OnTutorialReset(_dieCastingTrainingModalUIList);
            AudioManager.instance.PlayRandomBGM();
            
            Delay.RunLater(this, 10f, () =>
                OnStartSafetyEquipmentTraining());
        }
        public void SendPrompt(string userPrompt, bool withAudio = false)
        {
            _llmHandler.SendPrompt(userPrompt, withAudio);
        }

        public void OnTriggerMistakeCounter()
        {
            OnUpdateMistakeCountEvent?.Invoke();
        }

        public void OnTutorialReset(List<TutorialModalUI> modalUIList)
        {
            OnTutorialResetEvent?.Invoke();
            OnTeleportPlayer(_startingPosition.position, _startingPosition.rotation);
            foreach (var modalUI in modalUIList)
            {
                modalUI.gameObject.SetActive(false);
                modalUI.InjectTutorialManager(this);
            }

            if (_gameDataManager.StartFromBeginning)
                modalUIList[0].ShowTutorialModal();
        }

        [Button]
        public void OpenNextTutorialModal()
        {
            _trainingIndex++;
            if (_trainingIndex > _currentTutorial.Count - 1)
            {
                Debug.Log($"#{GetType().Name}# Foundry Training Completed!");
                OnTutorialResetEvent?.Invoke();
                OnTeleportPlayer(_startingPosition.position, _startingPosition.rotation);
                OnTrainingCompleteEvent?.Invoke();
                return;
            }

            _currentTutorial[_trainingIndex].ShowTutorialModal();
        }

        [Button]
        public void OnStartSafetyEquipmentTraining()
        {
            _trainingIndex = 0;
            _trainingID = TrainingID.WarehouseSafety;
            _currentTutorial = _guidedInspectionList;
            _currentTutorial[_trainingIndex].ShowTutorialModal();
            OnTrainingStartedEvent?.Invoke(_trainingID);
        }

        [Button]
        public void OnStartSandCasting()
        {
            _trainingIndex = 0;
           // _trainingID = TrainingID.SandCasting;
            _currentTutorial = _foundryTrainingModalUIList;
            _currentTutorial[_trainingIndex].ShowTutorialModal();
            OnTrainingStartedEvent?.Invoke(_trainingID);
        }

        [Button]
        public void OnStartPermanentMoldCasting()
        {
            _trainingIndex = 0;
            //_trainingID = TrainingID.PermanentMoldCasting;
            _currentTutorial = _permanentCastMoldingTrainingModalUIList;
            _currentTutorial[_trainingIndex].ShowTutorialModal();
            OnTrainingStartedEvent?.Invoke(_trainingID);
        }

        [Button]
        public void OnStartDieCasting()
        {
            _trainingIndex = 0;
            //_trainingID = TrainingID.DieCasting;
            _currentTutorial = _dieCastingTrainingModalUIList;
            _currentTutorial[_trainingIndex].ShowTutorialModal();
            OnTrainingStartedEvent?.Invoke(_trainingID);
        }

        public void OnTeleportPlayer(Vector3 pos, Quaternion rotation)
        {
            OnTeleportPlayerEvent?.Invoke(pos, rotation);
        }

        public void OnPlayOfficeAmbianceBGM()
        {
            AudioManager.instance.PlayOfficeAmbianceBGM();
        }

        public void OnPlayElevatorAmbianceBGM()
        {
            AudioManager.instance.PlayElevatorAmbianceBGM();
        }
    }
}

