using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Clan listesindeki tek satır.
public class ClanRowUI : MonoBehaviour
{
    [SerializeField] private Image emblem;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text capacityText;
    [SerializeField] private TMP_Text minLevelText;
    [SerializeField] private Button actionButton;

    [SerializeField] private AvatarCatalog emblemCatalog;

    private ClanData clan;
    private Action<ClanData> onAction;

    public void Setup(ClanData data, Action<ClanData> action)
    {
        clan = data;
        onAction = action;

        if (nameText != null) nameText.text = data.name;
        if (capacityText != null) capacityText.text = data.memberCount + "/" + data.maxMembers;
        if (minLevelText != null) minLevelText.text = data.minLevel > 0 ? "Lv " + data.minLevel : "";

        if (emblem != null && emblemCatalog != null)
        {
            Sprite sprite = emblemCatalog.Get(data.emblemIndex);

            if (sprite != null) emblem.sprite = sprite;
        }

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(() => onAction?.Invoke(clan));
        }
    }
}
