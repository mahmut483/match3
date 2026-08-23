using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Clan üye listesindeki tek satır.
public class ClanMemberRowUI : MonoBehaviour
{
    [SerializeField] private Image avatarImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text roleText;      // opsiyonel: Lider / Üye
    [SerializeField] private AvatarCatalog avatarCatalog;

    public void Setup(UserData member, bool isLeader)
    {
        if (nameText != null) nameText.text = member.displayName;
        if (levelText != null) levelText.text = member.highestCompletedLevel.ToString();
        if (roleText != null) roleText.text = isLeader ? "Lider" : "Üye";

        if (avatarImage == null || avatarCatalog == null) return;

        Sprite sprite = avatarCatalog.Get(member.avatarIndex);

        if (sprite != null) avatarImage.sprite = sprite;
    }
}
