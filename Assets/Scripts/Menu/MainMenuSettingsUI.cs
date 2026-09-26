using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

using Match3.Shared;

namespace Match3.Menu
{
    public class MainMenuSettingsUI : MonoBehaviour
    {
        // Sprite'ı değişen görsel butonun Target Graphic'idir.
        [Serializable]
        private class AudioToggleView
        {
            public Button button;
            public Sprite onSprite;
            public Sprite offSprite;
        }

        [Header("Panel")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button settingsOpenButton;
        [SerializeField] private Button settingsCloseButton;
        [SerializeField] private Button quitButton;

        [Header("Ses")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioToggleView music;
        [SerializeField] private AudioToggleView sfx;

        private void Awake()
        {
            if (settingsOpenButton != null) settingsOpenButton.onClick.AddListener(OpenSettings);
            if (settingsCloseButton != null) settingsCloseButton.onClick.AddListener(CloseSettings);
            if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
            if (music.button != null) music.button.onClick.AddListener(ToggleMusic);
            if (sfx.button != null) sfx.button.onClick.AddListener(ToggleSfx);

            ApplyAndRefresh(GameAudioChannel.Music, music);
            ApplyAndRefresh(GameAudioChannel.Sfx, sfx);

            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        private void OpenSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(true);

            ApplyAndRefresh(GameAudioChannel.Music, music);
            ApplyAndRefresh(GameAudioChannel.Sfx, sfx);
        }

        private void CloseSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        private void ToggleMusic()
        {
            GameAudioSettings.Toggle(GameAudioChannel.Music);
            ApplyAndRefresh(GameAudioChannel.Music, music);
        }

        private void ToggleSfx()
        {
            GameAudioSettings.Toggle(GameAudioChannel.Sfx);
            ApplyAndRefresh(GameAudioChannel.Sfx, sfx);
        }

        private void ApplyAndRefresh(GameAudioChannel channel, AudioToggleView view)
        {
            bool isEnabled = GameAudioSettings.IsEnabled(channel);

            if (mixer != null)
            {
                mixer.SetFloat(
                    GameAudioSettings.GetMixerParameter(channel),
                    GameAudioSettings.GetVolumeDb(channel));
            }

            if (channel == GameAudioChannel.Music)
            {
                ApplyMusicPlayback(isEnabled);
            }

            Image image = view.button != null ? view.button.targetGraphic as Image : null;

            if (image == null) return;

            if (view.onSprite != null && view.offSprite != null)
            {
                image.sprite = isEnabled ? view.onSprite : view.offSprite;
                image.color = Color.white;
                return;
            }

            image.color = isEnabled
                ? Color.white
                : new Color(1f, 1f, 1f, 0.45f);
        }

        private void ApplyMusicPlayback(bool isEnabled)
        {
            if (musicSource == null) return;

            musicSource.mute = !isEnabled;

            if (isEnabled)
            {
                if (!musicSource.isPlaying) musicSource.Play();
            }
            else if (musicSource.isPlaying)
            {
                musicSource.Stop();
            }
        }

        private void QuitGame()
        {
    #if UNITY_EDITOR
            Debug.Log("Quit requested. Application.Quit only closes a built player.");
    #else
            Application.Quit();
    #endif
        }
    }
}
