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
        [Serializable]
        public class TutorialFlowList
        {
            public TrainingID ID;
            public List<TutorialModalUI> List;
        }
        
        [SerializeField] private LLMHandler _llmHandler;
        [SerializeField] private Transform _startingPosition;
        [SerializeField] private List<TutorialFlowList> _tutorialFlowList;
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
            // OnPlayOfficeAmbianceBGM();.
            foreach (TutorialFlowList tutorialFlowList in _tutorialFlowList)
                OnTutorialReset(tutorialFlowList.List);
            
            AudioManager.instance.PlayRandomBGM();
            
            Delay.RunLater(this, 10f, () =>
                OnStartWarehouseSafety());
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
        public void OnStartWarehouseSafety()
        {
            _trainingIndex = 0;
            _trainingID = TrainingID.WarehouseSafety;
            _currentTutorial = _tutorialFlowList.Find(x => x.ID == _trainingID).List;
            _currentTutorial[_trainingIndex].ShowTutorialModal();
            OnTrainingStartedEvent?.Invoke(_trainingID);
        }

        [Button]
        public void OnStartOrderPicking()
        {
            _trainingIndex = 0;
           // _trainingID = TrainingID.SandCasting;
           _trainingID = TrainingID.OrderPicking;
           _currentTutorial = _tutorialFlowList.Find(x => x.ID == _trainingID).List;
            _currentTutorial[_trainingIndex].ShowTutorialModal();
            OnTrainingStartedEvent?.Invoke(_trainingID);
        }

        [Button]
        public void OnStartRouteOptimisation()
        {
            _trainingIndex = 0;
            _trainingID = TrainingID.RouteOptimisation;
            _currentTutorial = _tutorialFlowList.Find(x => x.ID == _trainingID).List;
            _currentTutorial[_trainingIndex].ShowTutorialModal();
            OnTrainingStartedEvent?.Invoke(_trainingID);
        }

        [Button]
        public void OnStartPackingAndDispatch()
        {
            _trainingIndex = 0;
            _trainingID = TrainingID.PackingAndDispatch;
            _currentTutorial = _tutorialFlowList.Find(x => x.ID == _trainingID).List;
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

