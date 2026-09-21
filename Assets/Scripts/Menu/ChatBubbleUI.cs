using System;
using TMPro;
using UnityEngine;

using Match3.Backend;

namespace Match3.Menu
{
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

        // "just now", "5m", "3h", "2d" gibi kısa gösterim.
        private static string FormatAge(DateTime utc)
        {
            TimeSpan age = DateTime.UtcNow - utc;

            if (age.TotalMinutes < 1) return "just now";
            if (age.TotalHours < 1) return (int)age.TotalMinutes + "m";
            if (age.TotalDays < 1) return (int)age.TotalHours + "h";

            return (int)age.TotalDays + "d";
        }
    }
}
