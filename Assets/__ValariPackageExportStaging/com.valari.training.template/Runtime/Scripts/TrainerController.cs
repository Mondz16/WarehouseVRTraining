using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Valari.Services;

namespace Valari.FoundryTraining.Controller
{
    public class TrainerController : MonoBehaviour
    {
        public static TrainerController Service
        {
            get
            {
                if (_ == null)
                    _ = Game.Services.Get<TrainerController>();

                return _;
            }
        }

        private static TrainerController _;

        [SerializeField] private Animator _animator;
        [SerializeField] private NavMeshAgent _navMeshAgent;
        [SerializeField] private AudioSource _voiceAudioSource;
        [SerializeField] private GameObject _dialogueHolder;
        [SerializeField] private Scrollbar _dialogueScroll;
        [SerializeField] private TMP_Text _dialogueText;
        private bool _isWalking = false;
        private bool _animationPlaying = false;
        private Coroutine _talkingCoroutine;

        public bool IsWalking => _isWalking;

        public void StartTalking(string message, AudioClip voiceOver = null)
        {
            if (_talkingCoroutine != null)
            {
                StopCoroutine(_talkingCoroutine);
                _talkingCoroutine = null;
            }

            if (voiceOver == null)
            {
                HandleIsCharacterTalkingAnimation(false);
                return;
            }

            _voiceAudioSource.clip = voiceOver;
            _voiceAudioSource.Play();
            _dialogueHolder.SetActive(true);
            float effectiveDuration = voiceOver.length / Mathf.Max(0.01f, _voiceAudioSource.pitch);
            StartCoroutine(StartTypingAnimationRoutine(_dialogueText, message, effectiveDuration - 3f));
            HandleIsCharacterTalkingAnimation(true);
            _talkingCoroutine = StartCoroutine(WaitForVoiceToFinish());
        }


        private IEnumerator StartTypingAnimationRoutine(TMP_Text tmp, string text, float durationSeconds)
        {
            tmp.text = "";
            string temp = "";

            int totalCharacters = Mathf.Max(1, text?.Length ?? 0);
            float secondsPerCharacter = durationSeconds / totalCharacters;

            Debug.Log($"#{GetType().Name}# seconds per character ->  {durationSeconds} / {totalCharacters} = {secondsPerCharacter}");
            if (totalCharacters == 0)
            {
                // No characters to type; wait out the duration to keep sync
                yield return new WaitForSeconds(Mathf.Max(0f, durationSeconds));
                yield break;
            }

            foreach (var letter in text)
            {
                temp += letter;
                tmp.text = temp;
                _dialogueScroll.value = 0;
                yield return new WaitForSeconds(secondsPerCharacter);
            }

            // Ensure final text is set exactly at the end of duration
            tmp.text = temp;
        }

        private IEnumerator WaitForVoiceToFinish()
        {
            // Ensure we exit if audio source or clip becomes invalid during playback
            while (_voiceAudioSource != null && _voiceAudioSource.isPlaying)
            {
                yield return null;
            }

            HandleIsCharacterTalkingAnimation(false);
            _talkingCoroutine = null;
            _dialogueHolder.SetActive(false);
        }

        private void HandleIsCharacterTalkingAnimation(bool isTalking)
        {
            if (isTalking)
            {
                if (!_animationPlaying)
                {
                    _animationPlaying = true;
                    _animator.SetBool("Talk", true);
                }
            }
            else
            {
                _animationPlaying = false;
                _animator.SetBool("Talk", false);
            }
        }

        public void MoveTrainer(Transform target)
        {
            StartCoroutine(MoveTo(target));
        }

        private IEnumerator MoveTo(Transform target)
        {
            _isWalking = true;
            SetupAnimationAndNavigation(_animator, _navMeshAgent);

            _navMeshAgent.SetDestination(target.position);
            yield return null;

            yield return MoveTowardsTarget(_navMeshAgent);

            // After reaching destination, face the main camera smoothly
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                float faceSpeed = 8f;
                Transform self = transform;
                Vector3 toCamera = mainCamera.transform.position - self.position;
                toCamera.y = 0f;
                if (toCamera.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(toCamera);
                    while (Quaternion.Angle(self.rotation, targetRotation) > 0.5f)
                    {
                        self.rotation = Quaternion.Slerp(self.rotation, targetRotation, Time.deltaTime * faceSpeed);
                        yield return null;
                    }
                    self.rotation = targetRotation;
                }
            }

            FinishMovement(_animator, target);
            _isWalking = false;
        }

        private void SetupAnimationAndNavigation(Animator animator, NavMeshAgent navMeshAgent)
        {
            animator.CrossFade(Animator.StringToHash("Walking"), 0.01f);
            animator.applyRootMotion = false;
            navMeshAgent.updateRotation = false;
        }

        private IEnumerator MoveTowardsTarget(NavMeshAgent navMeshAgent)
        {
            float rotationSpeed = 8f;
            Transform self = transform;
            while (navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
            {
                // Rotate towards current movement direction or steering target
                Vector3 desiredDirection = navMeshAgent.velocity.sqrMagnitude > 0.001f
                    ? navMeshAgent.velocity
                    : (navMeshAgent.steeringTarget - self.position);
                desiredDirection.y = 0f;
                if (desiredDirection.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(desiredDirection);
                    self.rotation = Quaternion.Slerp(self.rotation, targetRotation, Time.deltaTime * rotationSpeed);
                }
                yield return null;
            }
        }

        private void FinishMovement(Animator animator, Transform target)
        {
            animator.CrossFade(Animator.StringToHash("Idle"), 0.1f);
            animator.applyRootMotion = true;
        }
    }
}

