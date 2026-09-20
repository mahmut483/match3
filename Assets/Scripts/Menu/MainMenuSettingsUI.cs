using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class MainMenuSettingsUI : MonoBehaviour
{
    [Serializable]
    private class AudioToggleView
    {
        public Button button;

        [Tooltip("Boş bırakılırsa butonun Target Graphic görseli kullanılır.")]
        public Image image;

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

    private GameAudioSettings settings;

    private void Awake()
    {
        settings = GameAudioSettings.Shared;

        if (settingsOpenButton != null) settingsOpenButton.onClick.AddListener(OpenSettings);
        if (settingsCloseButton != null) settingsCloseButton.onClick.AddListener(CloseSettings);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        if (music.button != null) music.button.onClick.AddListener(ToggleMusic);
        if (sfx.button != null) sfx.button.onClick.AddListener(ToggleSfx);

        ApplyAndRefresh(GameAudioChannel.Music, music);
        ApplyAndRefresh(GameAudioChannel.Sfx, sfx);

        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (settingsOpenButton != null) settingsOpenButton.onClick.RemoveListener(OpenSettings);
        if (settingsCloseButton != null) settingsCloseButton.onClick.RemoveListener(CloseSettings);
        if (quitButton != null) quitButton.onClick.RemoveListener(QuitGame);
        if (music.button != null) music.button.onClick.RemoveListener(ToggleMusic);
        if (sfx.button != null) sfx.button.onClick.RemoveListener(ToggleSfx);
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
        settings.Toggle(GameAudioChannel.Music);
        ApplyAndRefresh(GameAudioChannel.Music, music);
    }

    private void ToggleSfx()
    {
        settings.Toggle(GameAudioChannel.Sfx);
        ApplyAndRefresh(GameAudioChannel.Sfx, sfx);
    }

    private void ApplyAndRefresh(GameAudioChannel channel, AudioToggleView view)
    {
        bool isEnabled = settings.IsEnabled(channel);

        if (mixer != null)
        {
            mixer.SetFloat(
                GameAudioSettings.GetMixerParameter(channel),
                settings.GetVolumeDb(channel));
        }

        if (channel == GameAudioChannel.Music)
        {
            ApplyMusicPlayback(isEnabled);
        }

        Image image = view.image != null
            ? view.image
            : (view.button != null ? view.button.targetGraphic as Image : null);

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
