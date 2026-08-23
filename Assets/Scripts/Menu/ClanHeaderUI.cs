using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Clan sayfasının üst şeridi: clan adı ve amblemi Firestore'dan gelir.
// Info butonu clan bilgi panelini açar.
public class ClanHeaderUI : MonoBehaviour
{
    [Header("Başlık")]
    [SerializeField] private TMP_Text clanNameText;
    [SerializeField] private Image clanIcon;
    [SerializeField] private AvatarCatalog emblemCatalog;

    [Header("Bilgi paneli")]
    [SerializeField] private Button infoButton;
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private Button closeInfoButton;

    private void Awake()
    {
        if (infoButton != null) infoButton.onClick.AddListener(OpenInfo);
        if (closeInfoButton != null) closeInfoButton.onClick.AddListener(CloseInfo);

        if (infoPanel != null) infoPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (infoButton != null) infoButton.onClick.RemoveListener(OpenInfo);
        if (closeInfoButton != null) closeInfoButton.onClick.RemoveListener(CloseInfo);
    }

    private void OnEnable()
    {
        // Clan bilgileri düzenlenince başlık kendiliğinden tazelenir.
        ClanService.ClanChanged += Apply;

        FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;

        // Kullanıcı verisi gelmeden clanId bilinmez; gelince tekrar denenir.
        if (bootstrap == null || !bootstrap.IsReady)
        {
            FirebaseBootstrap.UserReady += OnUserReady;
            return;
        }

        Load();
    }

    private void OnDisable()
    {
        FirebaseBootstrap.UserReady -= OnUserReady;
        ClanService.ClanChanged -= Apply;
    }

    private void OnUserReady(UserData user)
    {
        FirebaseBootstrap.UserReady -= OnUserReady;

        if (isActiveAndEnabled) Load();
    }

    private void Load()
    {
        ClanService.LoadCurrentClan(clan =>
        {
            if (clan != null) Fill(clan);
        });
    }

    // ClanChanged olayı parametresiz geldiği için önbellekteki clanı kullanır.
    private void Apply()
    {
        Fill(ClanService.CurrentClan);
    }

    private void Fill(ClanData clan)
    {
        if (clan == null) return;

        if (clanNameText != null) clanNameText.text = clan.name;

        if (clanIcon == null) return;

        if (emblemCatalog == null)
        {
            Debug.LogWarning("ClanHeaderUI: Emblem Catalog atanmamış, amblem gösterilemiyor.");
            return;
        }

        Sprite sprite = emblemCatalog.Get(clan.emblemIndex);

        if (sprite != null) clanIcon.sprite = sprite;
    }

    private void OpenInfo()
    {
        if (infoPanel != null) infoPanel.SetActive(true);
    }

    private void CloseInfo()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
    }
}
