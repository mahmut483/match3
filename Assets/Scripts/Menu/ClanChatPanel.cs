using System.Collections;
using System.Collections.Generic;
using Firebase.Firestore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Teams (InOfClan) sayfası: clan sohbeti ve can istekleri.
// Yazma için cihazın kendi klavyesi açılır; arayüzde input alanı bulunmaz.
public class ClanChatPanel : MonoBehaviour
{
    [Header("Akış")]
    [SerializeField] private Transform feedParent;          // Scroll View > Viewport > Content
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private int messageLimit = 50;

    [Header("Prefablar")]
    [SerializeField] private ChatBubbleUI chatBubblePrefab;
    [SerializeField] private LifeRequestUI ownRequestPrefab;    // ClanHeatRequest(Sender)
    [SerializeField] private LifeRequestUI otherRequestPrefab;  // ClanHeatRequest(Receiver)

    [Header("Yazma")]
    [SerializeField] private Button writeButton;
    [SerializeField] private int maxMessageLength = 120;
    [SerializeField] private string keyboardPlaceholder = "Mesajını yaz...";

    [Tooltip("Yalnızca editörde test için. Cihazda kullanılmaz, boş bırakılabilir.")]
    [SerializeField] private TMP_InputField editorInput;

    [Header("Can isteği")]
    [SerializeField] private Button requestButton;
    [SerializeField] private GameObject requestTimeoutPanel;
    [SerializeField] private TMP_Text requestTimerText;

    private readonly List<GameObject> spawned = new();
    private ListenerRegistration listener;
    private TouchScreenKeyboard keyboard;
    private Coroutine cooldownRoutine;

    private void Awake()
    {
        if (writeButton != null) writeButton.onClick.AddListener(OpenKeyboard);
        if (requestButton != null) requestButton.onClick.AddListener(RequestLives);

        if (editorInput != null)
        {
            editorInput.characterLimit = maxMessageLength;
            editorInput.onSubmit.AddListener(SendFromEditorInput);
            editorInput.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        StartListening();
        RefreshRequestState();
    }

    private void OnDisable()
    {
        StopListening();
        StopCooldown();
    }

    private void OnDestroy()
    {
        StopListening();

        if (writeButton != null) writeButton.onClick.RemoveAllListeners();
        if (requestButton != null) requestButton.onClick.RemoveAllListeners();
        if (editorInput != null) editorInput.onSubmit.RemoveAllListeners();
    }

    #region Yazma

    // Cihazda sistem klavyesini açar. Editörde klavye desteklenmediği için
    // varsa test input'u kullanılır.
    private void OpenKeyboard()
    {
        if (TouchScreenKeyboard.isSupported)
        {
            keyboard = TouchScreenKeyboard.Open(
                "",
                TouchScreenKeyboardType.Default,
                autocorrection: false,
                multiline: false,
                secure: false,
                alert: false,
                textPlaceholder: keyboardPlaceholder,
                characterLimit: maxMessageLength
            );

            return;
        }

        if (editorInput != null)
        {
            editorInput.gameObject.SetActive(true);
            editorInput.text = "";
            editorInput.ActivateInputField();
            return;
        }

        Debug.LogWarning("Bu platformda klavye yok. Test için Editor Input alanını doldurun.");
    }

    // Klavyenin durumu her karede kontrol edilir; kullanıcı "Done" deyince mesaj gider.
    private void Update()
    {
        if (keyboard == null) return;

        if (keyboard.status == TouchScreenKeyboard.Status.Done)
        {
            string text = keyboard.text;
            keyboard = null;

            ClanChatService.SendChat(text);
            return;
        }

        // İptal edildi ya da odak kaybedildi — mesaj gönderilmez.
        if (keyboard.status != TouchScreenKeyboard.Status.Visible)
        {
            keyboard = null;
        }
    }

    private void SendFromEditorInput(string text)
    {
        if (editorInput != null)
        {
            editorInput.text = "";
            editorInput.gameObject.SetActive(false);
        }

        ClanChatService.SendChat(text);
    }

    #endregion

    #region Akış

    private void StartListening()
    {
        FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;

        if (bootstrap == null || !bootstrap.IsReady) return;

        string clanId = bootstrap.User.clanId;

        if (string.IsNullOrEmpty(clanId)) return;

        StopListening();

        // Canlı dinleme: başka biri mesaj yazınca ekran kendiliğinden güncellenir.
        listener = ClanChatService.Listen(clanId, messageLimit, Render);
    }

    private void StopListening()
    {
        if (listener == null) return;

        listener.Stop();
        listener = null;
    }

    private void Render(List<ClanMessage> messages)
    {
        Clear();

        string myUid = FirebaseBootstrap.Instance.Uid;

        foreach (ClanMessage message in messages)
        {
            if (message.Type == ClanMessageType.LifeRequest)
            {
                SpawnRequest(message, myUid);
            }
            else
            {
                ChatBubbleUI bubble = Instantiate(chatBubblePrefab, feedParent);
                bubble.Setup(message);
                spawned.Add(bubble.gameObject);
            }
        }

        ScrollToBottom();
        ClaimPendingLives(messages, myUid);
    }

    private void SpawnRequest(ClanMessage message, string myUid)
    {
        bool isMine = message.senderUid == myUid;

        LifeRequestUI prefab = isMine ? ownRequestPrefab : otherRequestPrefab;

        if (prefab == null)
        {
            Debug.LogWarning(isMine
                ? "Own Request Prefab atanmamış — kendi isteğin ekranda görünmez."
                : "Other Request Prefab atanmamış — başkalarının isteği ekranda görünmez.");
            return;
        }

        LifeRequestUI row = Instantiate(prefab, feedParent);

        // Kendi isteğine bağış yapılamaz; dolmuşsa veya kapanmışsa da buton kapalı.
        bool alreadyDonated = message.donorUids != null && message.donorUids.Contains(myUid);
        bool canDonate = !isMine
                         && !alreadyDonated
                         && !message.claimed
                         && message.DonorCount < ClanChatService.LivesPerRequest;

        row.Setup(message, Donate, canDonate);
        spawned.Add(row.gameObject);
    }

    // Kendi isteklerime gelen canları otomatik topla.
    private void ClaimPendingLives(List<ClanMessage> messages, string myUid)
    {
        foreach (ClanMessage message in messages)
        {
            if (message.Type != ClanMessageType.LifeRequest) continue;
            if (message.senderUid != myUid || message.claimed) continue;
            if (message.DonorCount == 0) continue;

            ClanChatService.ClaimLives(message, gained =>
            {
                if (gained > 0) Debug.Log($"{gained} can toplandı.");
            });
        }
    }

    private void Donate(ClanMessage request)
    {
        ClanChatService.DonateLife(request);
        // Dinleyici açık olduğu için liste sonuç gelince kendiliğinden tazelenir.
    }

    private void RequestLives()
    {
        // Sunucuya gitmeden önce butonu kilitle; çift dokunuş iki istek atmasın.
        if (requestButton != null) requestButton.interactable = false;

        ClanChatService.SendLifeRequest(success =>
        {
            if (requestButton != null) requestButton.interactable = true;

            if (!success)
            {
                Debug.LogWarning("Can isteği gönderilemedi. Clanda mısın, süre doldu mu?");
                return;
            }

            RefreshRequestState();
        });
    }

    #region Bekleme süresi

    // Kalan süre varsa butonu gizleyip sayacı başlatır, yoksa butonu gösterir.
    private void RefreshRequestState()
    {
        double remaining = ClanChatService.RemainingRequestCooldown();

        if (remaining <= 0)
        {
            ShowRequestButton();
            return;
        }

        StopCooldown();
        cooldownRoutine = StartCoroutine(CooldownLoop());
    }

    private void ShowRequestButton()
    {
        StopCooldown();

        if (requestButton != null) requestButton.gameObject.SetActive(true);
        if (requestTimeoutPanel != null) requestTimeoutPanel.SetActive(false);
    }

    // Saniyede bir çalışır — Update ile her kare string üretmekten çok daha ucuz.
    private IEnumerator CooldownLoop()
    {
        if (requestButton != null) requestButton.gameObject.SetActive(false);
        if (requestTimeoutPanel != null) requestTimeoutPanel.SetActive(true);

        WaitForSeconds tick = new WaitForSeconds(1f);
        int lastShown = -1;

        while (true)
        {
            int remaining = Mathf.CeilToInt((float)ClanChatService.RemainingRequestCooldown());

            if (remaining <= 0) break;

            // Yazıyı yalnızca gösterilen saniye değiştiğinde güncelle.
            if (remaining != lastShown)
            {
                lastShown = remaining;

                if (requestTimerText != null) requestTimerText.text = Format(remaining);
            }

            yield return tick;
        }

        cooldownRoutine = null;
        ShowRequestButton();
    }

    private void StopCooldown()
    {
        if (cooldownRoutine == null) return;

        StopCoroutine(cooldownRoutine);
        cooldownRoutine = null;
    }

    // 90 -> "01:30"
    private static string Format(int totalSeconds)
    {
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        return minutes.ToString("00") + ":" + seconds.ToString("00");
    }

    #endregion

    private void ScrollToBottom()
    {
        if (scrollRect == null) return;

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    private void Clear()
    {
        foreach (GameObject item in spawned)
        {
            if (item != null) Destroy(item);
        }

        spawned.Clear();
    }

    #endregion
}
