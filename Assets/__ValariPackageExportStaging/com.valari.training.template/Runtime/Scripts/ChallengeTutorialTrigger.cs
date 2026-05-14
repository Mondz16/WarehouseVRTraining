using System;
using System.Numerics;
using Autohand;
using ObjectiveManagerandQuestEngine;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;
using Valari.UI;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

public class ChallengeTutorialTrigger : MonoBehaviour
{
    public string TriggerName => _triggerName;

    [SerializeField] private TutorialType _tutorialType;
    [SerializeField] [ShowIf("_tutorialType", TutorialType.Grab)] private bool _isGrabTrigger = false;
    [SerializeField] private string _triggerName;
    [SerializeField] private Outline _outline;
    [SerializeField] private Grabbable _grabbable;
    [SerializeField] private ParticleSystem _collectParticleEffect;
    [SerializeField] private bool _useChildTransform = false;
    [SerializeField] private UnityEvent _onChallengeActivated;
    [SerializeField] private UnityEvent _onChallengeTriggered;
    [SerializeField] [ShowIf("_useChildTransform")] private Transform _childTransform;

    [SerializeField] [ReadOnly] private bool _enabled = false;
    private TutorialModalUI _tutorialModalUI;
    private Vector3 _originPos;

    private void Awake()
    {
        if (_grabbable)
        {
            //_originPos = _grabbable.transform.position;
        }
    }

    private void Start() 
    {
        if (_grabbable)
        {
            _grabbable.OnGrabEvent += (hand, obj) => ShowOutline(false);
            _grabbable.OnReleaseEvent += (hand, obj) => ShowOutline(true);
        }

        if (_outline)
            _outline.OutlineWidth = 0;
    }

    public void InjectTutorialModalUI(TutorialModalUI tutorialModalUI)
    {
        _tutorialModalUI = tutorialModalUI;
        Debug.Log($"#{GetType().Name}# Injectec Tutorial ModalUI -> {_triggerName}");
    }

    public void OnUpdateTutorialTriggerState(bool enable)
    {
        _enabled = enable;
        if (_enabled)
            _onChallengeActivated?.Invoke();
        if (_outline)
            _outline.OutlineWidth = enable ? 10 : 0;
    }

    public void ShowOutline(bool show)
    {
        if (_enabled)
            _outline.OutlineWidth = show ? 10 : 0;
    }

    [Button]
    public void ResetChallenge()
    {
        if (_grabbable)
        {
            // _grabbable.transform.position = _originPos;
            _grabbable.gameObject.SetActive(true);
        }

        if (_outline)
            _outline.OutlineWidth = 0;
    }

    public void OnTriggerComplete()
    {
        TriggerChallengeComplete();
    }

    public object TriggerChallengeComplete()
    {
        _onChallengeTriggered?.Invoke();
        if (_enabled)
        {
            switch (_tutorialType)
            {
                case TutorialType.Trigger:
                    Debug.Log($"#{GetType().Name}# Trigger Challenge -> {_triggerName}");
                    _tutorialModalUI.OnUpdateChallengesState(_triggerName);
                    break;
                case TutorialType.Collect:
                    _tutorialModalUI.OnUpdateChallengesState(_triggerName);
                    // AudioManager.instance.Play_GrabSFX();
                    OnShowCollectEffect();
                    if (_grabbable)
                    {
                        _grabbable.gameObject.SetActive(false);
                    }

                    break;
                case TutorialType.Grab:
                    if (_isGrabTrigger)
                    {
                        OnShowCollectEffect();
                        _tutorialModalUI.OnUpdateChallengesState(_triggerName);
                    }

                    // AudioManager.instance.Play_GrabSFX();
                    // gameObject.SetActive(false);
                    return this;
            }
        }

        return null;
    }

    private void OnShowCollectEffect()
    {
        Vector3 spawnPoint = _useChildTransform ? _childTransform.position : transform.position;
        var collectFX = Instantiate(_collectParticleEffect, spawnPoint, Quaternion.identity);
        Destroy(collectFX, 2);
    }
}

public enum TutorialType
{
    Trigger, Collect, Grab
}

