using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Match3.Gameplay.Board;
using Match3.Shared;

namespace Match3.Gameplay.Session
{
    // GameBoard sahnesindeki panellerin tek kontrolcüsü. Şimdilik ayarlar paneli;
    // kazanma, kaybetme ve diğer paneller de buraya eklenecek.
    //
    // Butonlar Start'ta koddan bağlanır, Inspector'da onClick doldurmak gerekmez.
    // Bu bileşen her zaman AÇIK bir objede durmalı (UIGameOverPanel gibi): panelin
    // kendisine konursa panel kapalı başladığında Start hiç çalışmaz ve butonlar
    // bağlanmaz.
    public class GameBoardUI : MonoBehaviour
    {
        // Açılıp kapanan bir ses kanalının sahneye ait görsel referansları.
        [System.Serializable]
        private class AudioToggle
        {
            public Button button;

            [Tooltip("Sprite'ı değişecek görsel. BOŞ bırak: butonun kendi arka plan paneli kullanılır.")]
            public Image image;

            [Tooltip("Açık ve kapalı panel sprite'ları (yeşil / gri). İkisi de boşsa kapalıyken panel soluklaşır.")]
            public Sprite onSprite;
            public Sprite offSprite;

            [HideInInspector] public bool isOn = true;
        }

        [Header("Ayarlar paneli")]
        [SerializeField] private PotionBoard board;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button settingsOpenButton;
        [SerializeField] private Button settingsCloseButton;
        [SerializeField] private Button leaveButton;

        [Header("Ses")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioToggle music = new();
        [SerializeField] private AudioToggle sfx = new();

        private GameAudioSettings audioSettings;

        private void Start()
        {
            audioSettings = GameAudioSettings.Shared;

            if (settingsOpenButton != null) settingsOpenButton.onClick.AddListener(OpenSettings);
            if (settingsCloseButton != null) settingsCloseButton.onClick.AddListener(CloseSettings);
            if (leaveButton != null) leaveButton.onClick.AddListener(LeaveToMenu);

            if (music.button != null) music.button.onClick.AddListener(ToggleMusic);
            if (sfx.button != null) sfx.button.onClick.AddListener(ToggleSfx);

            music.isOn = audioSettings.IsEnabled(GameAudioChannel.Music);
            sfx.isOn = audioSettings.IsEnabled(GameAudioChannel.Sfx);

            Apply(music, GameAudioChannel.Music);
            Apply(sfx, GameAudioChannel.Sfx);

            // Panel oyun başında kapalı; sahnede açık unutulmuş olabilir.
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (settingsOpenButton != null) settingsOpenButton.onClick.RemoveListener(OpenSettings);
            if (settingsCloseButton != null) settingsCloseButton.onClick.RemoveListener(CloseSettings);
            if (leaveButton != null) leaveButton.onClick.RemoveListener(LeaveToMenu);
            if (music.button != null) music.button.onClick.RemoveListener(ToggleMusic);
            if (sfx.button != null) sfx.button.onClick.RemoveListener(ToggleSfx);
        }

        // Panel açıkken tahta girişi kilitlenir; SpecialStrikes de aynı bayrağa
        // bakıp vuruş seçimini kapatır. UI paneli tahtanın Physics2D
        // raycast'ini engellemiyor; kilit olmasa panelin arkasındaki taşlara
        // dokunulabilirdi. Time.timeScale kullanılmıyor: buton animasyonları ve
        // coroutine'ler donardı. Süren cascade arkada bitmeye devam eder.
        private void OpenSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(true);
            if (board != null) board.InputLocked = true;
        }

        private void CloseSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (board != null) board.InputLocked = false;
        }

        private void LeaveToMenu()
        {
            SceneManager.LoadScene(ButtonControl.MainMenuScene);
        }

        private void ToggleMusic()
        {
            Toggle(music, GameAudioChannel.Music);
        }

        private void ToggleSfx()
        {
            Toggle(sfx, GameAudioChannel.Sfx);
        }

        private void Toggle(AudioToggle view, GameAudioChannel channel)
        {
            view.isOn = audioSettings.Toggle(channel);
            Apply(view, channel);
        }

        private void Apply(AudioToggle view, GameAudioChannel channel)
        {
            if (mixer != null)
            {
                mixer.SetFloat(
                    GameAudioSettings.GetMixerParameter(channel),
                    audioSettings.GetVolumeDb(channel));
            }

            // Değişecek görsel butonun kendi paneli; ikon ona dokunulmadan üstte durur.
            Image image = view.image != null
                ? view.image
                : (view.button != null ? view.button.targetGraphic as Image : null);

            if (image == null) return;

            if (view.onSprite != null && view.offSprite != null)
            {
                image.sprite = view.isOn ? view.onSprite : view.offSprite;
                image.color = Color.white;
            }
            else
            {
                // Sprite verilmediyse kapalı durum soluk panel.
                image.color = view.isOn ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            }
        }
    }
}
