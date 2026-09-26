using TMPro;
using UnityEngine;
using UnityEngine.UI;

using Match3.Backend;

namespace Match3.Menu
{
    // Clan düzenleme paneli. Mevcut clan ayarlarını yükler, lider kaydedebilir.
    //
    // Amblem seçimi ClanCreatePanel ile aynı düzende: yan yana dizilmiş butonlar
    // yerine tek önizleme ve katalogda ilerleyen bir "Change" butonu. Katalog
    // büyüdükçe panele buton eklemek gerekmiyor.
    public class ClanEditPanel : MonoBehaviour
    {
        [Header("Alanlar")]
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_InputField descriptionInput;
        [SerializeField] private Button saveButton;

        [Tooltip("Kaydetmeden çıkar. Form kilitliyken de çalışır: oyuncu hiçbir " +
                 "durumda panelde mahsur kalmamalı.")]
        [SerializeField] private Button closeButton;

        [Header("Seçiciler")]
        [SerializeField] private OptionSelector joinTypeSelector;
        [SerializeField] private OptionSelector minLevelSelector;
        [SerializeField] private OptionSelector minCupSelector;

        [Header("Amblem")]
        [SerializeField] private Image emblemPreview;

        [Tooltip("Amblemi katalogdaki bir sonrakine geçiren buton (Change).")]
        [SerializeField] private Button emblemButton;

        [SerializeField] private AvatarCatalog emblemCatalog;

        [Header("Kurallar")]
        [SerializeField] private int minNameLength = 3;
        [SerializeField] private int maxNameLength = 20;
        [SerializeField] private int maxDescriptionLength = 80;

        private int emblemIndex;

        private void Awake()
        {
            if (saveButton != null) saveButton.onClick.AddListener(Save);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (emblemButton != null) emblemButton.onClick.AddListener(NextEmblem);

            if (nameInput != null) nameInput.characterLimit = maxNameLength;
            if (descriptionInput != null) descriptionInput.characterLimit = maxDescriptionLength;
        }

        // Kaydetmeden çıkış. Formdaki değişiklikler bilerek geri alınmıyor: panel her
        // açılışta clanın güncel verisiyle baştan dolduruluyor, yani yarım kalan
        // düzenleme kendiliğinden kayboluyor.
        private void Close()
        {
            gameObject.SetActive(false);
        }

        // Panel açıldığında mevcut clan ayarları yüklenir.
        private void OnEnable()
        {
            // Veri gelene kadar kaydetme kapalı: boş formun üstüne basılıp clanın
            // adı silinmesin.
            SetEditable(false);

            if (ClanService.CurrentClan != null)
            {
                Fill(ClanService.CurrentClan);
                return;
            }

            ClanService.LoadCurrentClan(clan =>
            {
                if (clan != null && isActiveAndEnabled) Fill(clan);
            });
        }

        private void Fill(ClanData clan)
        {
            if (nameInput != null) nameInput.text = clan.name;
            if (descriptionInput != null) descriptionInput.text = clan.description;

            if (joinTypeSelector != null) joinTypeSelector.SetValue(clan.joinType);
            if (minLevelSelector != null) minLevelSelector.SetValue(clan.minLevel);
            if (minCupSelector != null) minCupSelector.SetValue(clan.minCup);

            SelectEmblem(clan.emblemIndex);

            // Lider değilse form kapalı — kurallar zaten reddeder, kullanıcıyı
            // boşuna uğraştırma.
            bool isLeader = FirebaseBootstrap.Instance != null &&
                            clan.leaderUid == FirebaseBootstrap.Instance.Uid;

            SetEditable(isLeader);
        }

        // Kaydetme sırasında ve lider olmayan üyede tüm form aynı anda kilitlenir.
        private void SetEditable(bool editable)
        {
            if (saveButton != null) saveButton.interactable = editable;
            if (emblemButton != null) emblemButton.interactable = editable;
            if (nameInput != null) nameInput.interactable = editable;
            if (descriptionInput != null) descriptionInput.interactable = editable;
        }

        private void NextEmblem()
        {
            if (emblemCatalog == null || emblemCatalog.Count == 0) return;

            SelectEmblem((emblemIndex + 1) % emblemCatalog.Count);
        }

        private void SelectEmblem(int index)
        {
            if (emblemCatalog == null || emblemCatalog.Count == 0)
            {
                emblemIndex = 0;
                return;
            }

            // Katalogdan avatar silinmiş olabilir; kayıtlı index aralığa çekilir.
            emblemIndex = Mathf.Clamp(index, 0, emblemCatalog.Count - 1);

            if (emblemPreview == null) return;

            Sprite sprite = emblemCatalog.Get(emblemIndex);

            if (sprite != null) emblemPreview.sprite = sprite;
        }

        private void Save()
        {
            string clanName = nameInput != null ? nameInput.text.Trim() : "";

            // Sunucuya gitmeden önce elenebilecek hata için istek atma.
            if (clanName.Length < minNameLength)
            {
                Debug.LogWarning($"Clan adı en az {minNameLength} karakter olmalı.");
                return;
            }

            SetEditable(false);

            ClanService.UpdateClan(
                clanName,
                descriptionInput != null ? descriptionInput.text.Trim() : "",
                emblemIndex,
                joinTypeSelector != null ? joinTypeSelector.SelectedValue : 0,
                minLevelSelector != null ? minLevelSelector.SelectedValue : 0,
                minCupSelector != null ? minCupSelector.SelectedValue : 0,
                (success, message) =>
                {
                    // Yazma sürerken oyuncu paneli kapatmış olabilir. Kapalı panele
                    // dokunursak, panel yeniden açıldığında Fill'in kurduğu kilit
                    // durumunu bozarız.
                    if (!isActiveAndEnabled)
                    {
                        if (!success) Debug.LogWarning("Clan güncellenemedi: " + message);
                        return;
                    }

                    if (!success)
                    {
                        // Form açık kalır: oyuncu ismi düzeltip tekrar denesin.
                        SetEditable(true);
                        Debug.LogWarning("Clan güncellenemedi: " + message);
                        return;
                    }

                    Close();
                });
        }
    }
}
