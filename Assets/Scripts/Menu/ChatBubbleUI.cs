using System;
using TMPro;
using UnityEngine;

// Sohbetteki tek mesaj balonu.
public class ChatBubbleUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text timeText;

    public void Setup(ClanMessage message)
    {
        if (nameText != null) nameText.text = message.senderName;
        if (messageText != null) messageText.text = message.text;
        if (timeText != null) timeText.text = FormatAge(message.createdAt.ToDateTime());
    }

    // "az önce", "5dk", "3sa", "2g" gibi kısa gösterim.
    private static string FormatAge(DateTime utc)
    {
        TimeSpan age = DateTime.UtcNow - utc;

        if (age.TotalMinutes < 1) return "az önce";
        if (age.TotalHours < 1) return (int)age.TotalMinutes + "dk";
        if (age.TotalDays < 1) return (int)age.TotalHours + "sa";

        return (int)age.TotalDays + "g";
    }
}
