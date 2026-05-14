using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Valari.Services;
using Valari.Utilities;

public class UIManager : MonoBehaviour
{
    public static UIManager Services
    {
        get
        {
            if (_ == null)
                _ = Game.Services.Get<UIManager>();
            return _;
        }
    }

    private static UIManager _;

    [SerializeField] private Animator _gameOverPanel;
    [SerializeField] private Animator _fadePanel;
    [SerializeField] private Animator _animator;
    [SerializeField] private Slider _animationSlider;

    public void OnShowGameOverPanel(Action onGameOverPanelShown = null)
    {
        _gameOverPanel.gameObject.SetActive(true);
        _gameOverPanel.SetBool("fadeIn", true);
        Delay.RunLater(this, 5f, () =>
        {
            onGameOverPanelShown?.Invoke();
        });
    }

    public void OnFadeOutPanelAnimation()
    {
        // _fadePanel.SetTrigger("fadeOut");
    }

    public void OnAnimationValueChanged()
    {
        _animator.SetFloat("animationValue", _animationSlider.value);
    }

    public void OnAssembleButtonClicked()
    {
        StartCoroutine(StartAnimationRoutine(true));
    }

    public void OnDisassembleButtonClicked()
    {
        StartCoroutine(StartAnimationRoutine(false));
    }

    public void OnTestButton()
    {
        Debug.Log("Test Button!");
    }

    private IEnumerator StartAnimationRoutine(bool assemble)
    {
        float animationValue = assemble ? 1 : 0;
        if (assemble && _animator.GetFloat("animationValue") >= 0)
        {
            while (animationValue > 0)
            {
                animationValue -= .01f;
                _animator.SetFloat("animationValue", animationValue);
                yield return null;
            }
        }
        else if (_animator.GetFloat("animationValue") <= 1)
        {
            while (animationValue < 1)
            {
                animationValue += .01f;
                _animator.SetFloat("animationValue", animationValue);
                yield return null;
            }
        }
    }
}

