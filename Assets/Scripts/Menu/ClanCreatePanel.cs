using TMPro;
using UnityEngine;
using UnityEngine.UI;

// CreatePage: yeni clan kurma formu.
public class ClanCreatePanel : MonoBehaviour
{
    [Header("Alanlar")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField descriptionInput;
    [SerializeField] private Button createButton;

    [Header("Seçiciler")]
    [SerializeField] private OptionSelector joinTypeSelector;   // herkese açık / onaylı / kapalı
    [SerializeField] private OptionSelector minLevelSelector;
    [SerializeField] private OptionSelector minCupSelector;

    [Header("Amblem")]
    [SerializeField] private Image emblemPreview;
    [SerializeField] private AvatarCatalog emblemCatalog;
    [SerializeField] private Button emblemButton;               // View — amblem değiştirir

    [Header("Kurallar")]
    [SerializeField] private int minNameLength = 3;
    [SerializeField] private int maxNameLength = 20;
    [SerializeField] private int maxMembers = 30;

    private int emblemIndex;

    private void Awake()
    {
        if (createButton != null) createButton.onClick.AddListener(Create);
        if (emblemButton != null) emblemButton.onClick.AddListener(NextEmblem);

        if (nameInput != null) nameInput.characterLimit = maxNameLength;

        ApplyEmblem();
    }

    private void OnDestroy()
    {
        if (createButton != null) createButton.onClick.RemoveListener(Create);
        if (emblemButton != null) emblemButton.onClick.RemoveListener(NextEmblem);
    }

    private void NextEmblem()
    {
        if (emblemCatalog == null || emblemCatalog.Count == 0) return;

        emblemIndex = (emblemIndex + 1) % emblemCatalog.Count;

        ApplyEmblem();
    }

    private void ApplyEmblem()
    {
        if (emblemPreview == null || emblemCatalog == null) return;

        Sprite sprite = emblemCatalog.Get(emblemIndex);

        if (sprite != null) emblemPreview.sprite = sprite;
    }

    private void Create()
    {
        string clanName = nameInput != null ? nameInput.text.Trim() : "";

        if (clanName.Length < minNameLength)
        {
            Debug.LogWarning($"Clan adı en az {minNameLength} karakter olmalı.");
            return;
        }

        ClanData clan = new ClanData
        {
            name = clanName,
            description = descriptionInput != null ? descriptionInput.text.Trim() : "",
            emblemIndex = emblemIndex,
            maxMembers = maxMembers,
            joinType = joinTypeSelector != null ? joinTypeSelector.SelectedValue : 0,
            minLevel = minLevelSelector != null ? minLevelSelector.SelectedValue : 0,
            minCup = minCupSelector != null ? minCupSelector.SelectedValue : 0
        };

        // Çift tıklamayı engelle; sonuç gelince tekrar açılır.
        createButton.interactable = false;

        ClanService.CreateClan(clan, (success, message) =>
        {
            createButton.interactable = true;

            if (!success)
            {
                Debug.LogWarning("Clan kurulamadı: " + message);
                return;
            }

            Debug.Log("Clan kuruldu: " + clan.name);

            if (nameInput != null) nameInput.text = "";
            if (descriptionInput != null) descriptionInput.text = "";
        });
    }
}
