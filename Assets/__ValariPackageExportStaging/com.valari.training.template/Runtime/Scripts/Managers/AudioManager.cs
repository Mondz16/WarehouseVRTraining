using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace ObjectiveManagerandQuestEngine
{
    public class AudioManager : MonoBehaviour
    {
        private class AudioState
        {
            public AudioClip Clip;
            public bool IsPlayed;
        }

        public static AudioManager instance;
        public AudioSource sfxSource;
        public AudioSource loopingSfxSource;
        public AudioSource voiceOverSource;
        public AudioSource bgmAudioSource;
        public AudioSource ambianceAudioSource;
        public AudioClip audioClip_CoinUpdate;
        public AudioClip audioClip_ObjectiveCompleted;
        public AudioClip audioClipNext;
        public AudioClip audioClipTeleport;
        public AudioClip audioClipOfficeAmbiance;
        public AudioClip audioClipElevatorAmbiance;
        public AudioClip moldCastedSFX;
        public AudioClip fireStartSFX;
        public AudioClip spraySFX;
        public AudioClip tilt1SFX;
        public AudioClip tilt2SFX;
        public AudioClip topMoldMoveSFX;
        public List<AudioClip> bgmAudioClip;
        public List<AudioClip> generalFactClips;

        private List<AudioState> _generalFactStatusList = new List<AudioState>();

        private void Awake()
        {
            instance = this;
            foreach (AudioClip generalFactClip in generalFactClips)
            {
                AudioState audioState = new AudioState();
                audioState.Clip = generalFactClip;
                audioState.IsPlayed = false;
                _generalFactStatusList.Add(audioState);
            }
        }

        public void Play_Coin()
        {
            sfxSource.PlayOneShot(audioClip_CoinUpdate);
        }

        public void Play_ObjectiveCompleted()
        {
            sfxSource.PlayOneShot(audioClip_ObjectiveCompleted);
        }

        public void PlayNextSFX()
        {
            sfxSource.PlayOneShot(audioClipNext);
        }

        public void PlayFireStartSFX(bool play)
        {
            loopingSfxSource.clip = fireStartSFX;
            if (play)
                loopingSfxSource.Play();
            else
                loopingSfxSource.Stop();
        }

        public void PlayTiltSFX(bool stand)
        {
            sfxSource.PlayOneShot(stand ? tilt1SFX : tilt2SFX);
        }

        public void PlaySpraySFX(bool play)
        {
            loopingSfxSource.clip = spraySFX;
            if (play)
                loopingSfxSource.Play();
            else
                loopingSfxSource.Stop();
        }

        public void PlayMoldCastedSFX()
        {
            sfxSource.PlayOneShot(moldCastedSFX);
        }

        public void PlayMachineTopMoldClose()
        {
            sfxSource.PlayOneShot(topMoldMoveSFX);
        }

        public void PlayTeleportSFX()
        {
            sfxSource.PlayOneShot(audioClipTeleport);
        }

        public void PlayVoiceOver(AudioClip clip)
        {
            if (voiceOverSource.isPlaying)
                voiceOverSource.Stop();

            voiceOverSource.PlayOneShot(clip);
            Debug.Log($"#{GetType().Name}# Play Voice Over");
        }

        [Button]
        public void ResetStates()
        {
            foreach (AudioState audioState in _generalFactStatusList)
                audioState.IsPlayed = false;
        }

        [Button]
        public void PlayRandomFacts()
        {
            AudioState selectedClip = null;

            do
            {
                int randomIndex = Random.Range(0, _generalFactStatusList.Count);
                selectedClip = _generalFactStatusList[randomIndex];
            } while (selectedClip is { IsPlayed: true });

            sfxSource.PlayOneShot(selectedClip.Clip);
            selectedClip.IsPlayed = true;
            Debug.Log("Play Random General Facts");
        }

        public void PlayRandomBGM()
        {
            int randomIndex = Random.Range(0, bgmAudioClip.Count);
            Debug.Log($"#{GetType().Name}# Random Index: {randomIndex}");
            bgmAudioSource.clip = bgmAudioClip[randomIndex];
            bgmAudioSource.Play();
        }

        public void PlayOfficeAmbianceBGM()
        {
            if (ambianceAudioSource.isPlaying)
                ambianceAudioSource.Stop();

            ambianceAudioSource.clip = audioClipOfficeAmbiance;
            ambianceAudioSource.Play();
        }

        public void PlayElevatorAmbianceBGM()
        {
            if (ambianceAudioSource.isPlaying)
                ambianceAudioSource.Stop();

            ambianceAudioSource.clip = audioClipElevatorAmbiance;
            ambianceAudioSource.Play();
        }

        public void TurnOnVoiceOverVolume(bool isOn)
        {
            voiceOverSource.volume = isOn ? 1 : 0;
        }
    }
}

