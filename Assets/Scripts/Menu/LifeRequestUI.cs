using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Can isteği satırı. Hem isteği atan (ilerleme) hem de diğer oyuncular (bağış butonu) için kullanılır.
// Bağış butonu yalnızca alıcı prefabında bulunur; gönderen prefabında boş bırakılır.
public class LifeRequestUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text progressText;   // "0/5"
    [SerializeField] private Button donateButton;     // yalnız alıcı prefabında

    private ClanMessage request;
    private Action<ClanMessage> onDonate;

    public void Setup(ClanMessage message, Action<ClanMessage> donateAction, bool canDonate)
    {
        request = message;
        onDonate = donateAction;

        if (nameText != null) nameText.text = message.senderName;
        if (messageText != null) messageText.text = message.text;

        if (progressText != null)
        {
            progressText.text = message.DonorCount + "/" + ClanChatService.LivesPerRequest;
        }

        if (donateButton != null)
        {
            donateButton.onClick.RemoveAllListeners();
            donateButton.onClick.AddListener(Donate);

            // Dolmuşsa, kapanmışsa ya da zaten bağış yaptıysa buton kapalı.
            donateButton.interactable = canDonate;
        }
    }

    private void Donate()
    {
        if (donateButton != null) donateButton.interactable = false;

        onDonate?.Invoke(request);
    }
}
