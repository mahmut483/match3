using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Clan bilgi paneli: clan künyesi ve üye listesi.
public class ClanInfoPanelUI : MonoBehaviour
{
    [Header("Künye")]
    [SerializeField] private Image clanImage;
    [SerializeField] private TMP_Text clanNameText;
    [SerializeField] private TMP_Text clanDescriptionText;
    [SerializeField] private AvatarCatalog emblemCatalog;

    [Header("Değerler")]
    [SerializeField] private TMP_Text teamPointsValue;
    [SerializeField] private TMP_Text requiredLevelValue;
    [SerializeField] private TMP_Text teamTypeValue;
    [SerializeField] private TMP_Text membersValue;

    [Tooltip("joinType sayısının karşılığı: 0, 1, 2 sırasıyla.")]
    [SerializeField] private string[] joinTypeLabels = { "Açık", "Onaylı", "Kapalı" };

    [Header("Üye listesi")]
    [SerializeField] private ClanMemberRowUI memberRowPrefab;
    [SerializeField] private Transform memberParent;   // ClanMemberScrollView > Viewport > Content
    [SerializeField] private int memberLimit = 50;

    [Header("Butonlar")]
    [SerializeField] private Button editButton;
    [SerializeField] private GameObject editPanel;
    [SerializeField] private Button leaveButton;
    [SerializeField] private Button closeButton;

    private readonly List<ClanMemberRowUI> rows = new();

    private void Awake()
    {
        if (editButton != null) editButton.onClick.AddListener(OpenEdit);
        if (leaveButton != null) leaveButton.onClick.AddListener(Leave);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        if (editButton != null) editButton.onClick.RemoveListener(OpenEdit);
        if (leaveButton != null) leaveButton.onClick.RemoveListener(Leave);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    private void OnEnable()
    {
        ClanService.ClanChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        ClanService.ClanChanged -= Refresh;
    }

    private void Refresh()
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
        if (clanNameText != null) clanNameText.text = clan.name;
        if (clanDescriptionText != null) clanDescriptionText.text = clan.description;

        if (clanImage != null && emblemCatalog != null)
        {
            Sprite sprite = emblemCatalog.Get(clan.emblemIndex);

            if (sprite != null) clanImage.sprite = sprite;
        }

        if (teamPointsValue != null) teamPointsValue.text = clan.totalScore.ToString();
        if (requiredLevelValue != null) requiredLevelValue.text = clan.minLevel.ToString();
        if (membersValue != null) membersValue.text = clan.memberCount + "/" + clan.maxMembers;

        if (teamTypeValue != null)
        {
            bool valid = joinTypeLabels != null &&
                         clan.joinType >= 0 &&
                         clan.joinType < joinTypeLabels.Length;

            teamTypeValue.text = valid ? joinTypeLabels[clan.joinType] : clan.joinType.ToString();
        }

        // Düzenleme yalnızca liderde açık.
        if (editButton != null)
        {
            bool isLeader = FirebaseBootstrap.Instance != null &&
                            clan.leaderUid == FirebaseBootstrap.Instance.Uid;

            editButton.gameObject.SetActive(isLeader);
        }

        LoadMembers(clan);
    }

    private void LoadMembers(ClanData clan)
    {
        if (memberRowPrefab == null || memberParent == null) return;

        ClanService.LoadMembers(clan.id, memberLimit, members =>
        {
            if (!isActiveAndEnabled) return;

            ClearRows();

            foreach (UserData member in members)
            {
                ClanMemberRowUI row = Instantiate(memberRowPrefab, memberParent);
                row.Setup(member, member.uid == clan.leaderUid);
                rows.Add(row);
            }
        });
    }

    private void ClearRows()
    {
        foreach (ClanMemberRowUI row in rows)
        {
            if (row != null) Destroy(row.gameObject);
        }

        rows.Clear();
    }

    private void OpenEdit()
    {
        if (editPanel != null) editPanel.SetActive(true);
    }

    private void Leave()
    {
        leaveButton.interactable = false;

        ClanService.LeaveClan((success, message) =>
        {
            leaveButton.interactable = true;

            if (!success)
            {
                Debug.LogWarning("Clandan ayrılınamadı: " + message);
                return;
            }

            Close();
        });
    }

    private void Close()
    {
        gameObject.SetActive(false);
    }
}
