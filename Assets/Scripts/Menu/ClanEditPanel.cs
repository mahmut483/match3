using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Clan düzenleme paneli. Mevcut clan ayarlarını yükler, lider kaydedebilir.
public class ClanEditPanel : MonoBehaviour
{
    [Header("Alanlar")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField descriptionInput;
    [SerializeField] private Button saveButton;

    [Header("Seçiciler")]
    [SerializeField] private OptionSelector joinTypeSelector;
    [SerializeField] private OptionSelector minLevelSelector;
    [SerializeField] private OptionSelector minCupSelector;

    [Header("Amblem")]
    [SerializeField] private Button[] emblemButtons;   // sıra = emblemIndex
    [SerializeField] private AvatarCatalog emblemCatalog;
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float selectedScale = 1.2f;

    [Header("Kurallar")]
    [SerializeField] private int maxNameLength = 20;

    private int selectedEmblem;

    private void Awake()
    {
        if (saveButton != null) saveButton.onClick.AddListener(Save);
        if (nameInput != null) nameInput.characterLimit = maxNameLength;

        for (int i = 0; i < emblemButtons.Length; i++)
        {
            int index = i;

            if (emblemButtons[i] == null) continue;

            emblemButtons[i].onClick.AddListener(() => SelectEmblem(index));

            // Görselleri katalogdan al — sıra kaymasını engeller.
            if (emblemCatalog != null && emblemButtons[i].image != null)
            {
                Sprite sprite = emblemCatalog.Get(index);

                if (sprite != null) emblemButtons[i].image.sprite = sprite;
            }
        }
    }

    private void OnDestroy()
    {
        if (saveButton != null) saveButton.onClick.RemoveListener(Save);

        foreach (Button button in emblemButtons)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }
    }

    // Panel açıldığında mevcut clan ayarları yüklenir.
    private void OnEnable()
    {
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

        // Lider değilse kaydetme kapalı — kurallar zaten reddeder, kullanıcıyı boşuna uğraştırma.
        bool isLeader = FirebaseBootstrap.Instance != null &&
                        clan.leaderUid == FirebaseBootstrap.Instance.Uid;

        if (saveButton != null) saveButton.interactable = isLeader;
    }

    private void SelectEmblem(int index)
    {
        if (emblemButtons.Length == 0) return;

        selectedEmblem = Mathf.Clamp(index, 0, emblemButtons.Length - 1);

        for (int i = 0; i < emblemButtons.Length; i++)
        {
            if (emblemButtons[i] == null) continue;

            emblemButtons[i].transform.localScale =
                Vector3.one * (i == selectedEmblem ? selectedScale : normalScale);
        }
    }

    private void Save()
    {
        saveButton.interactable = false;

        ClanService.UpdateClan(
            nameInput != null ? nameInput.text : "",
            descriptionInput != null ? descriptionInput.text : "",
            selectedEmblem,
            joinTypeSelector != null ? joinTypeSelector.SelectedValue : 0,
            minLevelSelector != null ? minLevelSelector.SelectedValue : 0,
            minCupSelector != null ? minCupSelector.SelectedValue : 0,
            (success, message) =>
            {
                saveButton.interactable = true;

                if (!success)
                {
                    Debug.LogWarning("Clan güncellenemedi: " + message);
                    return;
                }

                gameObject.SetActive(false);
            });
    }
}
