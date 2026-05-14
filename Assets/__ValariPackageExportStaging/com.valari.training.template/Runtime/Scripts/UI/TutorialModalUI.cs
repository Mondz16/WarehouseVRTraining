using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using ObjectiveManagerandQuestEngine;
using Pathfinding.Serialization.JsonFx;
using Sirenix.OdinInspector;
using TMPro;
using Unity.Loading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Valari.Data;
using Valari.FoundryTraining.Controller;
using Valari.Managers;
using Valari.Utilities;

namespace Valari.UI
{
    public class TutorialModalUI : MonoBehaviour
    {
        [SerializeField] private GameObject _container;
        [SerializeField] private Animator _animator;
        [SerializeField] private bool _animateText = false;
        [SerializeField] private TutorialData _tutorialData;
        [SerializeField] private TMP_Text _headerText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private Button _continueButton;
        [SerializeField] private bool _disableNextSFX = false;
        [SerializeField] private ContentSizeFitter _contentSizeFitter;

        [Header("If Tutorial Will Teleport Player")] [SerializeField]
        private bool _teleportPlayer = false;

        [SerializeField] [ShowIf("_teleportPlayer")]
        private Transform _teleportPosition;

        [Header("If Tutorial Has Challenges")] [SerializeField]
        private bool _hasChallenges = false;

        [Header("If Tutorial will Review the steps")] [SerializeField]
        private bool _isStepReview = false;

        [SerializeField] [ShowIf("_isStepReview")]
        private List<string> _stepsList;

        [SerializeField] [ShowIf("_hasChallenges")]
        private Transform _challengeHolder;

        [FormerlySerializedAs("_challengeToggleUI")] [SerializeField] [ShowIf("_hasChallenges")]
        private ChallengeToggleUI _challengeToggle;

        [SerializeField] [ShowIf("_hasChallenges")]
        private List<ChallengeTutorialTrigger> _challengeTutorialTriggerList;

        [SerializeField] [ReadOnly] [ShowIf("_hasChallenges")]
        private List<TutorialChallengeData> _tutorialChallengeList = new List<TutorialChallengeData>();

        [SerializeField] [ReadOnly] [ShowIf("_hasChallenges")]
        private List<ChallengeToggleUI> _challengeToggleUIList = new List<ChallengeToggleUI>();

        private TutorialManager _tutorialManager;
        private bool _isComplete = false;
        private int _taskAttemptCounter = 0;
        private TMP_Text _continueText;

        private TrainerController _trainerController => TrainerController.Service;

        public UnityEvent OnTriggerNextTutorialEvent;

        public void SetTutorialData(TutorialData tutorialData)
        {
            _tutorialData = tutorialData;
        }

        private void Initialize()
        {
            gameObject.SetActive(true);
            _container.SetActive(false);

            _headerText.text = "";
            _descriptionText.text = "";
            _tutorialChallengeList = _tutorialData.GetTutorialChallengeData();

            _continueButton.onClick.AddListener(OnTriggerNextTutorial);
            // _continueButton.gameObject.SetActive(!_hasChallenges);

            if (_hasChallenges)
            {
                _continueText = _continueButton.GetComponentInChildren<TMP_Text>();
                _continueText.text = "Check";
                _isComplete = false;
                _challengeHolder.DeleteChildren();
                _challengeToggleUIList.Clear();
                foreach (TutorialChallengeData challengeData in _tutorialChallengeList)
                {
                    ChallengeToggleUI toggleUI = Instantiate(_challengeToggle, _challengeHolder);
                    toggleUI.SetChallengeUI(challengeData.ChallengeTitle);
                    _challengeToggleUIList.Add(toggleUI);
                }
            }

            ResetChallenges();
        }

        public void InjectTutorialManager(TutorialManager tutorialManager)
        {
            _tutorialManager = tutorialManager;
            if (_tutorialData.HasChallenges)
            {
                ResetChallenges();
                foreach (TutorialChallengeData challengeData in _tutorialData.GetTutorialChallengeData())
                    challengeData.IsComplete = false;
            }
        }

        public void ResetChallenges()
        {
            foreach (var trigger in _challengeTutorialTriggerList)
            {
                trigger.ResetChallenge();
                trigger.InjectTutorialModalUI(this);
                trigger.OnUpdateTutorialTriggerState(false);
            }
        }

        [Button]
        public void OnTriggerNextTutorial()
        {
            if (_hasChallenges)
            {
                foreach (TutorialChallengeData challengeData in _tutorialChallengeList)
                {
                    if (!challengeData.IsComplete)
                    {
                        _tutorialManager.OnTriggerMistakeCounter();
                        EvaluateTask();
                        return;
                    }
                }
            }

            PlayCloseAnimation();
        }

        [Button]
        public void EvaluateTask()
        {
            _taskAttemptCounter++;
            var json = JsonWriter.Serialize(_tutorialChallengeList);
            _tutorialManager.SendPrompt($"{LLMPrompt.Step_Evaluation_Prompt} \nAttempt #{_taskAttemptCounter} \nJson: {json}",
                true);
        }

        private void PlayCloseAnimation()
        {
            Delay.RunLater(this, .25f, () =>
            {
                if (!_disableNextSFX)
                    AudioManager.instance.PlayNextSFX();
                _animator.SetTrigger("fadeOut");
                transform.DOPunchScale(-(transform.localScale * .2f), .5f, 5, 0);
                Delay.RunLater(this, .2f, () =>
                {
                    OnTriggerNextTutorialEvent?.Invoke();
                    _tutorialManager.OpenNextTutorialModal();
                    _container.SetActive(false);
                    gameObject.SetActive(false);
                });
            });
        }

        [Button]
        public void ShowTutorialModal()
        {
            Initialize();

            StartCoroutine(ShowTutorialRoutine());
        }

        private IEnumerator ShowTutorialRoutine()
        {
            if (_teleportPlayer)
            {
                _trainerController.MoveTrainer(_teleportPosition);
                AudioManager.instance.PlayTeleportSFX();
                UIManager.Services.OnFadeOutPanelAnimation();
                _teleportPosition.gameObject.SetActive(false);
                _tutorialManager.OnTeleportPlayer(_teleportPosition.position, _teleportPosition.rotation);
            }
            yield return new WaitUntil(() => _trainerController.IsWalking == false);

            _container.SetActive(true);
            if (_tutorialData.Title.Equals(String.Empty))
                _headerText.gameObject.SetActive(false);

            if (_animateText)
                StartTypingAnimation();
            else
            {
                _headerText.text = _tutorialData.Title;
                _descriptionText.text = _tutorialData.Description;
                _contentSizeFitter.SetLayoutVertical();
            }

            _trainerController.StartTalking(_tutorialData.Description, _tutorialData.VoiceOver);
            if (!_tutorialData.IsInOrder)
            {
                foreach (var trigger in _challengeTutorialTriggerList)
                    trigger.OnUpdateTutorialTriggerState(true);
            }
            else
            {
                if (_challengeTutorialTriggerList.Count > 0)
                    _challengeTutorialTriggerList[0].OnUpdateTutorialTriggerState(true);
            }
        }

        public void PlayVoiceOver()
        {
            Debug.Log($"#{GetType().Name}#Voice Over: {_tutorialData.VoiceOver == null}");
            if (_tutorialData.VoiceOver)
                AudioManager.instance.PlayVoiceOver(_tutorialData.VoiceOver);
        }

        public void TurnOnVoiceOver(bool isOn)
        {
            AudioManager.instance.TurnOnVoiceOverVolume(isOn);
        }

        public void OnUpdateChallengesState(string triggerName)
        {
            bool completed = true;
            for (int i = 0; i < _tutorialChallengeList.Count; i++)
            {
                var challenge = _tutorialChallengeList[i];
                if (challenge.IsComplete) continue;

                if (_tutorialData.IsInOrder)
                {
                    if (challenge.ChallengeTriggerName.ToLower().Equals(triggerName.ToLower()))
                    {
                        challenge.IsComplete = true;
                        _challengeToggleUIList[i].UpdateChallengeUI(true);
                        AudioManager.instance.Play_ObjectiveCompleted();

                        Debug.Log(
                            $"#{GetType().Name}# i : {i + 1} triggerCount: {_challengeTutorialTriggerList.Count} = {i + 1 < _challengeTutorialTriggerList.Count}");
                        if (i + 1 < _challengeTutorialTriggerList.Count)
                            _challengeTutorialTriggerList[i + 1].OnUpdateTutorialTriggerState(true);
                    }

                    if (!challenge.IsComplete)
                    {
                        completed = false;
                        break;
                    }
                }
                else
                {
                    if (challenge.ChallengeTriggerName.ToLower().Equals(triggerName.ToLower()))
                    {
                        challenge.IsComplete = true;
                        _challengeToggleUIList[i].UpdateChallengeUI(true);
                        AudioManager.instance.Play_ObjectiveCompleted();
                    }

                    if (!challenge.IsComplete)
                        completed = false;
                }
            }

            if (completed && !_isComplete)
            {
                foreach (var trigger in _challengeTutorialTriggerList)
                    trigger.OnUpdateTutorialTriggerState(false);
                _isComplete = completed;
                _continueText.text = "Next";
            }
        }

        private void StartTypingAnimation()
        {
            StartCoroutine(StartTypingAnimationRoutine(_headerText, _tutorialData.Title));
            StartCoroutine(StartTypingAnimationRoutine(_descriptionText, _tutorialData.Description));
        }

        private IEnumerator StartTypingAnimationRoutine(TMP_Text tmp, string text)
        {
            tmp.text = "";
            string temp = "";
            foreach (var letter in text)
            {
                temp += letter;
                tmp.text = temp;
                yield return new WaitForSeconds(.01f);
            }

            tmp.text = temp;
            yield return new WaitForSeconds(1f);
        }
    }
}

