using Meta.XR.BuildingBlocks.AIBlocks;
using UnityEngine;
using Valari.FoundryTraining.Controller;

public class LLMHandler : MonoBehaviour
{
    [SerializeField] private LlmAgent _llmAgent;
    [SerializeField] private TextToSpeechAgent _textToSpeechAgent;

    private bool _withAudio = false;
    private AudioClip _clip;

    private void Awake()
    {
        _llmAgent.onResponseReceived.AddListener(GenerateSpeech);

        _textToSpeechAgent.onClipReady.AddListener(clip =>
        {
            _clip = clip;
        });

        _textToSpeechAgent.onSpeakStarting.AddListener(text =>
        {
            Debug.Log($"#{GetType().Name}# On Agent Started Speaking!");
            _textToSpeechAgent.StopSpeaking();
            TrainerController.Service.StartTalking(text, _clip);
        });
    }

    public void SendPrompt(string prompt, bool withAudio = false)
    {
        _llmAgent.SendPromptAsync(prompt);
        _withAudio = withAudio;

        Debug.Log($"#{GetType().Name}# Sent Prompt to LLM Agent!");
    }

    public void GenerateSpeech(string text)
    {
        if (!_withAudio) return;
        _textToSpeechAgent.SpeakText(text);
        Debug.Log($"#{GetType().Name}# Generated Speech!");
    }
}

