using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum StrikeKind
{
    Hammer,
    Cannon,
    Bomb
}

// Tab bar'daki özel vuruşlar. Seçim durumu, kalan haklar, buton görünümleri ve
// her vuruşun HANGİ hücreleri kapsadığı burada.
//
// Hücreleri temizleme işi bilerek burada değil: PotionBoard'a vuruş türü,
// başlangıç hücresi ve gerekirse hücre listesi veriliyor. Cannon satırı anında
// silmez; PotionBoard onu topun geçtiği sırada temizler. Tahtanın iç yapısına
// (Node dizisi, zincir sayacı) bu sınıf hiç dokunmaz.
public class SpecialStrikes : MonoBehaviour
{
    [System.Serializable]
    public class StrikeSlot
    {
        public StrikeKind kind;
        public Button button;

        [Tooltip("Kalan hakkı gösteren metin.")]
        public TMP_Text countText;

        [Tooltip("Vuruş seçiliyken açılan çerçeve. Boş bırakılabilir.")]
        public GameObject selectedVisual;

        [Tooltip("Butonun kendi Canvas'ı. Seçiliyken bilgi panelinin üstüne çıkarmak için.")]
        public Canvas canvas;

        [Header("Bilgi panelinde gösterilecekler")]
        [Tooltip("Panelde bu vuruşun ikonu.")]
        public Sprite panelIcon;

        public string title;

        [TextArea] public string description;

        [HideInInspector] public int remaining;
    }

    [SerializeField] private PotionBoard board;
    [SerializeField] private StrikeSlot[] slots;

    [Tooltip("Hammer'ın artısında her kolun uzunluğu. 1 = merkez ve dört komşu.")]
    [SerializeField, Min(1)] private int hammerReach = 1;

    [Tooltip("Vuruş seçilince açılan bilgi paneli. Seçim kalkınca kapanır.")]
    [SerializeField] private GameObject specialShotPanel;

    // Paneldeki üç alan her vuruşta AYNI nesneler; içerikleri seçime göre
    // değişiyor. Vuruş başına ayrı panel kurmak yerine tek panel dolduruluyor.
    [SerializeField] private Image panelIcon;
    [SerializeField] private TMP_Text panelTitle;
    [SerializeField] private TMP_Text panelDescription;

    [Tooltip("Seçili butonun sorting layer'ı. Bilgi panelinin ÜSTÜNDE kalması için.")]
    [SerializeField] private string selectedSortingLayer = "Character";

    [Tooltip("Seçili olmayan butonların sorting layer'ı. Panelin ALTINDA kalırlar.")]
    [SerializeField] private string defaultSortingLayer = "Background";

    // Seçili vuruş. null ise normal oyun: takas ve dokunarak patlatma çalışır.
    private StrikeKind? armed;
    private bool cannonFiring;

    public bool IsArmed => armed.HasValue;

    private void Start()
    {
        // ActiveLevel GameManager.Awake'te atanıyor; tüm Awake'ler Start'lardan
        // önce koştuğu için burada okumak güvenli.
        LevelData level = GameManager.Instance != null ? GameManager.Instance.ActiveLevel : null;

        foreach (StrikeSlot slot in slots)
        {
            slot.remaining = RemainingFor(level, slot.kind);

            // Döngü değişkeni kapanışa KOPYALANMALI, yoksa üç buton da son
            // sıradaki vuruşu seçer.
            StrikeKind kind = slot.kind;

            if (slot.button != null) slot.button.onClick.AddListener(() => Toggle(kind));
        }

        Refresh();

        if (board != null) board.CannonStrikeFinished += CompleteCannonStrike;
    }

    private void OnDestroy()
    {
        if (board != null) board.CannonStrikeFinished -= CompleteCannonStrike;
    }

    private static int RemainingFor(LevelData level, StrikeKind kind)
    {
        if (level == null) return 0;

        switch (kind)
        {
            case StrikeKind.Hammer: return level.hammerCount;
            case StrikeKind.Cannon: return level.cannonCount;
            default: return level.bombCount;
        }
    }

    // Aynı vuruşa tekrar basmak seçimi bırakır, başkasına basmak ona geçer.
    private void Toggle(StrikeKind kind)
    {
        StrikeSlot slot = SlotFor(kind);

        if (slot == null || slot.remaining <= 0) return;

        // Hedef satır beklenirken Cannon'a tekrar basmak gerçek bir toggle'dır:
        // board ilk konumuna gelir, maskeler açılır ve panel kapanır. Atış
        // başladıysa iptal etmek yerine aynı paneli vuruş bitene dek koruruz.
        if (kind == StrikeKind.Cannon && armed == StrikeKind.Cannon)
        {
            if (board != null && board.TryCancelCannonAim())
            {
                armed = null;
                Refresh();
            }

            return;
        }

        if (kind == StrikeKind.Cannon && armed != StrikeKind.Cannon)
        {
            if (board == null || !board.TryBeginCannonAim()) return;
        }

        // Cannon hedefleme/ateş akışı açıkken paneli kapatmak yerine korunur.
        if (armed == StrikeKind.Cannon && board != null && board.IsCannonPresentationActive) return;

        armed = armed == kind ? (StrikeKind?)null : kind;

        Refresh();
    }

    // Vuruş seçiliyken PotionBoard dokunulan taşı buraya yollar. true dönerse
    // dokunuş harcandı; false ise tahta kendi akışına devam eder.
    public bool TryUseOn(Potion potion)
    {
        if (!armed.HasValue || potion == null || board == null) return false;

        StrikeSlot slot = SlotFor(armed.Value);

        if (slot == null || slot.remaining <= 0) return false;

        StrikeKind kind = armed.Value;
        Vector2Int origin = new(potion.xIndex, potion.yIndex);
        List<Vector2Int> cells = kind == StrikeKind.Cannon ? null : CellsFor(kind, origin);

        if (cells != null && cells.Count == 0) return false;

        // Vuruşun gerçekten başlatılabildiği doğrulanmadan hak düşmez. Cannon
        // prefabı/anchor'ı eksikse seçim açık kalır ve dokunma özel taşı yanlışlıkla
        // patlatmaz; true burada "bu dokunuş özel vuruşa aitti" demektir.
        Transform strikeSource = kind == StrikeKind.Hammer && slot.button != null
            ? slot.button.transform
            : null;

        if (!board.TryRunStrike(kind, origin, cells, strikeSource)) return true;

        slot.remaining--;

        // Bir seçim, bir vuruş. Seçim açık kalsaydı sonraki dokunuş farkında
        // olmadan ikinci hakkı harcardı.
        if (kind == StrikeKind.Cannon)
        {
            // Bilgi paneli ve seçili buton Cannon animasyonu boyunca görünür.
            cannonFiring = true;
        }
        else
        {
            armed = null;
        }

        Refresh();

        return true;
    }

    private void CompleteCannonStrike()
    {
        if (!cannonFiring) return;

        cannonFiring = false;
        armed = null;
        Refresh();
    }

    // Tahta sınırının dışına taşan hücreler ayıklanmıyor: ClearCell zaten sınır
    // kontrolü yapıyor ve dışarıdakini sessizce atlıyor.
    private List<Vector2Int> CellsFor(StrikeKind kind, Vector2Int origin)
    {
        List<Vector2Int> cells = new() { origin };

        switch (kind)
        {
            case StrikeKind.Hammer:
                for (int step = 1; step <= hammerReach; step++)
                {
                    cells.Add(origin + Vector2Int.right * step);
                    cells.Add(origin + Vector2Int.left * step);
                    cells.Add(origin + Vector2Int.up * step);
                    cells.Add(origin + Vector2Int.down * step);
                }
                break;

            case StrikeKind.Bomb:
                for (int x = origin.x - 1; x <= origin.x + 1; x++)
                {
                    for (int y = origin.y - 1; y <= origin.y + 1; y++)
                    {
                        if (x != origin.x || y != origin.y) cells.Add(new Vector2Int(x, y));
                    }
                }
                break;
        }

        return cells;
    }

    private StrikeSlot SlotFor(StrikeKind kind)
    {
        foreach (StrikeSlot slot in slots)
        {
            if (slot.kind == kind) return slot;
        }

        return null;
    }

    // Sayılar, seçili çerçeve, hakkı biten butonun kapanması, bilgi paneli ve
    // butonların sıralaması. Seçim her değiştiğinde tek yerden güncelleniyor.
    private void Refresh()
    {
        // Panel yalnızca bir vuruş seçiliyken duruyor.
        if (specialShotPanel != null) specialShotPanel.SetActive(IsArmed);

        // İçerik seçili vuruştan geliyor. Panel kapalıyken dokunmuyoruz;
        // kapanırken metinleri silmenin görünür bir faydası yok.
        StrikeSlot shown = armed.HasValue ? SlotFor(armed.Value) : null;

        if (shown != null)
        {
            if (panelIcon != null && shown.panelIcon != null) panelIcon.sprite = shown.panelIcon;
            if (panelTitle != null) panelTitle.text = shown.title;
            if (panelDescription != null) panelDescription.text = shown.description;
        }

        foreach (StrikeSlot slot in slots)
        {
            if (slot.countText != null) slot.countText.text = slot.remaining.ToString();
            if (slot.selectedVisual != null) slot.selectedVisual.SetActive(armed == slot.kind);
            if (slot.button != null) slot.button.interactable = slot.remaining > 0;

            if (slot.canvas == null) continue;

            // Seçili buton panelin üstüne çıkar, diğerleri altında kalır.
            // overrideSorting kapalıysa canvas kendi katmanını yok sayar, o
            // yüzden burada garantiye alınıyor.
            slot.canvas.overrideSorting = true;
            slot.canvas.sortingLayerName = armed == slot.kind
                ? selectedSortingLayer
                : defaultSortingLayer;
        }
    }
}
