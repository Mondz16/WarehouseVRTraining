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

        [Header("Walking")]
        [Tooltip("A walk ends once the trainer has stopped getting closer to its target for this long (seconds).")]
        [SerializeField] private float _walkStallSeconds = 2f;

        [Tooltip("Seconds a walk may take on top of twice the time its distance needs at the agent's speed.")]
        [SerializeField] private float _walkExtraSeconds = 5f;

        [Tooltip("Longest time (seconds) the trainer spends turning to face the player after a walk.")]
        [SerializeField] private float _faceTimeout = 1.5f;

        private bool _isWalking = false;
        private bool _animationPlaying = false;
        private Coroutine _talkingCoroutine;
        private Coroutine _typingCoroutine;
        private Coroutine _moveCoroutine;

        /// <summary>
        /// True while the trainer walks to a tutorial step's position. Every tutorial modal waits for this to be false
        /// before it shows, so a walk must always end (see <see cref="TrainerWalkMonitor"/>).
        /// </summary>
        public bool IsWalking => _isWalking;

        private void Awake()
        {
            // The NavMeshAgent moves the trainer. A non-kinematic Rigidbody let physics shove and spin it on top of that,
            // so it could hover outside the agent's stopping distance forever and the walk never finished.
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic) body.isKinematic = true;
        }

        private void OnDisable()
        {
            // The walk coroutine dies with the object; never leave the tutorial waiting on a walk that cannot finish.
            _moveCoroutine = null;
            _isWalking = false;
        }

        public void StartTalking(string message, AudioClip voiceOver = null)
        {
            if (_talkingCoroutine != null)
            {
                StopCoroutine(_talkingCoroutine);
                _talkingCoroutine = null;
            }

            if (_typingCoroutine != null)
            {
                StopCoroutine(_typingCoroutine);
                _typingCoroutine = null;
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
            _typingCoroutine = StartCoroutine(StartTypingAnimationRoutine(_dialogueText, message, effectiveDuration - 3f));
            HandleIsCharacterTalkingAnimation(true);
            _talkingCoroutine = StartCoroutine(WaitForVoiceToFinish());
        }


        // Reveals the message with maxVisibleCharacters: the text is parsed, laid out and auto-sized once,
        // instead of being rebuilt (and re-allocated) for every letter.
        private IEnumerator StartTypingAnimationRoutine(TMP_Text tmp, string text, float durationSeconds)
        {
            tmp.text = text ?? string.Empty;
            tmp.maxVisibleCharacters = 0;
            tmp.ForceMeshUpdate();

            TMP_TextInfo textInfo = tmp.textInfo;
            int totalCharacters = Mathf.Max(1, textInfo.characterCount);
            float secondsPerCharacter = durationSeconds / totalCharacters;

            Debug.Log($"#{GetType().Name}# seconds per character ->  {durationSeconds} / {totalCharacters} = {secondsPerCharacter}");

            var wait = new WaitForSeconds(Mathf.Max(0f, secondsPerCharacter));
            int lastLine = Mathf.Max(1, textInfo.lineCount - 1);
            int shownLine = -1;
            for (int visible = 1; visible <= textInfo.characterCount; visible++)
            {
                tmp.maxVisibleCharacters = visible;

                // The full text is laid out from the start, so scroll to the line being typed
                // (the old per-letter rebuild kept it in view by scrolling to the bottom).
                int line = textInfo.characterInfo[visible - 1].lineNumber;
                if (line != shownLine)
                {
                    shownLine = line;
                    _dialogueScroll.value = 1f - (float)line / lastLine;
                }

                yield return wait;
            }

            tmp.maxVisibleCharacters = 99999; // TMP's default: no limit
            _typingCoroutine = null;
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
            // A new walk replaces one still in progress (two walks would each clear IsWalking when they end).
            if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
            _moveCoroutine = StartCoroutine(MoveTo(target));
        }

        private IEnumerator MoveTo(Transform target)
        {
            _isWalking = true;
            SetupAnimationAndNavigation(_animator, _navMeshAgent);

            Vector3 destination = target.position;
            _navMeshAgent.SetDestination(destination);
            yield return null;

            yield return MoveTowardsTarget(_navMeshAgent, destination);

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
                    float targetYaw = targetRotation.eulerAngles.y;
                    float turning = 0f;
                    // Compare yaw only (a small tilt must not keep the loop alive) and give up after _faceTimeout.
                    while (Mathf.Abs(Mathf.DeltaAngle(self.eulerAngles.y, targetYaw)) > 0.5f && turning < _faceTimeout)
                    {
                        self.rotation = Quaternion.Slerp(self.rotation, targetRotation, Time.deltaTime * faceSpeed);
                        turning += Time.deltaTime;
                        yield return null;
                    }
                    self.rotation = targetRotation;
                }
            }

            FinishMovement(_animator, target);
            _moveCoroutine = null;
            _isWalking = false;
        }

        private void SetupAnimationAndNavigation(Animator animator, NavMeshAgent navMeshAgent)
        {
            animator.CrossFade(Animator.StringToHash("Walking"), 0.01f);
            animator.applyRootMotion = false;
            navMeshAgent.updateRotation = false;
        }

        private IEnumerator MoveTowardsTarget(NavMeshAgent navMeshAgent, Vector3 destination)
        {
            float rotationSpeed = 8f;
            Transform self = transform;
            float distance = Vector3.Distance(self.position, destination);
            var walk = new TrainerWalkMonitor(_walkStallSeconds, 0.05f,
                2f * distance / Mathf.Max(0.1f, navMeshAgent.speed) + _walkExtraSeconds);

            while (!walk.Update(navMeshAgent.remainingDistance, navMeshAgent.stoppingDistance, navMeshAgent.pathPending, Time.deltaTime))
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

            if (walk.End != TrainerWalkEnd.Arrived)
                Debug.LogWarning($"#{GetType().Name}# Walk ended without arriving ({walk.End}, " +
                                 $"{navMeshAgent.remainingDistance:0.00} m left); the tutorial carries on.", this);

            // Stop steering: with a path still set, the agent would keep pulling the trainer back toward the target.
            if (navMeshAgent.isOnNavMesh) navMeshAgent.ResetPath();
        }

        private void FinishMovement(Animator animator, Transform target)
        {
            animator.CrossFade(Animator.StringToHash("Idle"), 0.1f);
            animator.applyRootMotion = true;
        }
    }
}

