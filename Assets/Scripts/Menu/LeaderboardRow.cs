using TMPro;
using UnityEngine;

// Sıralama listesindeki tek satır. Hem listedeki satırlar hem de
// alttaki "senin sıran" paneli bu bileşeni kullanır.
public class LeaderboardRow : MonoBehaviour
{
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;

    public void Setup(long rank, string playerName, int level)
    {
        if (rankText != null) rankText.text = rank + ".";
        if (nameText != null) nameText.text = playerName;
        if (levelText != null) levelText.text = level.ToString();
    }
}
