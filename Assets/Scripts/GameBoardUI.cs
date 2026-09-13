using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// GameBoard sahnesindeki panellerin tek kontrolcüsü. Şimdilik ayarlar paneli;
// kazanma, kaybetme ve diğer paneller de buraya eklenecek.
//
// Butonlar Start'ta koddan bağlanır, Inspector'da onClick doldurmak gerekmez.
// Bu bileşen her zaman AÇIK bir objede durmalı (UIGameOverPanel gibi): panelin
// kendisine konursa panel kapalı başladığında Start hiç çalışmaz ve butonlar
// bağlanmaz.
public class GameBoardUI : MonoBehaviour
{
    // Açılıp kapanan bir ses kanalı: buton, ikon, mixer parametresi, kayıt anahtarı.
    [System.Serializable]
    public class AudioToggle
    {
        public Button button;

        [Tooltip("Sprite'ı değişecek görsel. BOŞ bırak: butonun kendi arka plan paneli kullanılır.")]
        public Image image;

        [Tooltip("Açık ve kapalı panel sprite'ları (yeşil / gri). İkisi de boşsa kapalıyken panel soluklaşır.")]
        public Sprite onSprite;
        public Sprite offSprite;

        [Tooltip("Mixer'da expose edilmiş volume parametresinin adı.")]
        public string parameter;

        [Tooltip("PlayerPrefs anahtarı; tercih oturumlar arasında saklanır.")]
        public string prefsKey;

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
    [SerializeField] private AudioToggle music = new() { parameter = "MusicVolume", prefsKey = "MusicOn" };
    [SerializeField] private AudioToggle sfx = new() { parameter = "SfxVolume", prefsKey = "SfxOn" };

    // Mixer'da "kapalı" için sessizlik. 0 dB = tam ses.
    private const float MutedDb = -80f;

    private void Start()
    {
        if (settingsOpenButton != null) settingsOpenButton.onClick.AddListener(OpenSettings);
        if (settingsCloseButton != null) settingsCloseButton.onClick.AddListener(CloseSettings);
        if (leaveButton != null) leaveButton.onClick.AddListener(LeaveToMenu);

        if (music.button != null) music.button.onClick.AddListener(() => Toggle(music));
        if (sfx.button != null) sfx.button.onClick.AddListener(() => Toggle(sfx));

        // Kayıtlı tercihler: hiç kaydedilmediyse açık.
        music.isOn = PlayerPrefs.GetInt(music.prefsKey, 1) == 1;
        sfx.isOn = PlayerPrefs.GetInt(sfx.prefsKey, 1) == 1;

        Apply(music);
        Apply(sfx);

        // Panel oyun başında kapalı; sahnede açık unutulmuş olabilir.
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    // Panel açıkken tahta girişi kilitlenir. UI paneli tahtanın Physics2D
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

    private void Toggle(AudioToggle channel)
    {
        channel.isOn = !channel.isOn;

        PlayerPrefs.SetInt(channel.prefsKey, channel.isOn ? 1 : 0);
        PlayerPrefs.Save();

        Apply(channel);
    }

    // Mixer'ı ve ikonu kanalın durumuna getirir. Parametre mixer'da yoksa
    // SetFloat false döner ve sessizce geçilir; müzik grubu sonradan eklenince
    // kendiliğinden çalışmaya başlar.
    private void Apply(AudioToggle channel)
    {
        if (mixer != null && !string.IsNullOrEmpty(channel.parameter))
        {
            mixer.SetFloat(channel.parameter, channel.isOn ? 0f : MutedDb);
        }

        // Değişecek görsel butonun kendi paneli; ikon ona dokunulmadan üstte durur.
        Image image = channel.image != null
            ? channel.image
            : (channel.button != null ? channel.button.targetGraphic as Image : null);

        if (image == null) return;

        if (channel.onSprite != null && channel.offSprite != null)
        {
            image.sprite = channel.isOn ? channel.onSprite : channel.offSprite;
        }
        else
        {
            // Sprite verilmediyse kapalı durum soluk panel.
            image.color = channel.isOn ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        }
    }
}
