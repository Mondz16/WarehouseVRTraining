using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Meta.XR.BuildingBlocks.AIBlocks;
using Sirenix.OdinInspector;
using UnityEngine;
using Valari.Data;

namespace Valari.Utilities
{
    public class AudioClipGenerator : MonoBehaviour
    {
        public TutorialDataCollectionSO _tutorialDataCollectionSO;
        [SerializeField] private TextToSpeechAgent _textToSpeechAgent;

        private bool _isGenerationReady = false;
        private TrainingID _currentTrainingId = TrainingID.None;
        private int _indexCounter = 0;
        private AudioClip _currentClip = null;

        private void OnEnable()
        {
            _textToSpeechAgent.onClipReady.AddListener(GenerateAudioClip);
            
            _textToSpeechAgent.onSpeakStarting.AddListener(text =>
            {
                Debug.Log($"#{GetType().Name}# On Agent Started Speaking!");
                _textToSpeechAgent.StopSpeaking();
            });
        }

        private void OnDisable()
        {
            _textToSpeechAgent.onClipReady.RemoveAllListeners();
        }

        private void GenerateAudioClip(AudioClip audioClip)
        {
            _indexCounter++;
            SaveWav($"{_currentTrainingId}-{_indexCounter}", audioClip);
            _currentClip = audioClip;
            _isGenerationReady = true;
        }

        [Button]
        public void GenerateSpeechByID(TrainingID id)
        {
            _currentTrainingId = id;
            var dataList = _tutorialDataCollectionSO.GetDataListByID(id);
            StartCoroutine(GenerateSpeechRoutine(dataList));
        }

        private IEnumerator GenerateSpeechRoutine(List<TutorialData> dataList)
        {
            foreach (TutorialData tutorialData in dataList)
            {
                _isGenerationReady = false;
                _textToSpeechAgent.SpeakText(tutorialData.Description);
                yield return new WaitUntil(() => _isGenerationReady);

                tutorialData.VoiceOver = _currentClip;
                Debug.Log($"#{GetType().Name}# Generated Audio Clip!");
                yield return new WaitForSeconds(1f);
            }
        }
        
        private string SaveWav(string filename, AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogError($"#{GetType().Name}# SaveWav failed: clip is null.");
                return null;
            }

            var safeName = string.Join("_", filename.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            var baseDir =
#if UNITY_EDITOR
                Application.dataPath; // project-relative, easy to find during authoring
#else
                Application.persistentDataPath; // writable in builds
#endif
            var outputDir = Path.Combine(baseDir, "GeneratedAudio");
            Directory.CreateDirectory(outputDir);

            var path = Path.Combine(outputDir, $"{safeName}.wav");

            try
            {
                var sampleCount = clip.samples * clip.channels;
                var samples = new float[sampleCount];
                clip.GetData(samples, 0);

                var wavData = ConvertToWav(samples, clip.channels, clip.frequency);
                using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                fileStream.Write(wavData, 0, wavData.Length);

// #if UNITY_EDITOR
//                 // Ensure the file shows up in the Project window.
//                 UnityEditor.AssetDatabase.Refresh();
// #endif
                Debug.Log($"#{GetType().Name}# Wrote WAV: {path}");
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogError($"#{GetType().Name}# SaveWav failed for '{path}': {ex}");
                return null;
            }
        }

        // Convert float samples to WAV byte array
        private byte[] ConvertToWav(float[] samples, int channels, int sampleRate)
        {
            using var stream = new MemoryStream();
            var byteRate = sampleRate * channels * 2; // 16-bit audio

            // WAV header
            stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"), 0, 4);
            stream.Write(System.BitConverter.GetBytes(36 + samples.Length * 2), 0, 4);
            stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"), 0, 4);
            stream.Write(System.Text.Encoding.ASCII.GetBytes("fmt "), 0, 4);
            stream.Write(System.BitConverter.GetBytes(16), 0, 4);
            stream.Write(System.BitConverter.GetBytes((short)1), 0, 2);
            stream.Write(System.BitConverter.GetBytes((short)channels), 0, 2);
            stream.Write(System.BitConverter.GetBytes(sampleRate), 0, 4);
            stream.Write(System.BitConverter.GetBytes(byteRate), 0, 4);
            stream.Write(System.BitConverter.GetBytes((short)(channels * 2)), 0, 2);
            stream.Write(System.BitConverter.GetBytes((short)16), 0, 2);

            // Data chunk
            stream.Write(System.Text.Encoding.ASCII.GetBytes("data"), 0, 4);
            stream.Write(System.BitConverter.GetBytes(samples.Length * 2), 0, 4);

            // Convert samples to 16-bit PCM
            foreach (float sample in samples)
            {
                short intSample = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue);
                stream.Write(System.BitConverter.GetBytes(intSample), 0, 2);
            }

            return stream.ToArray();
        }
    }
}