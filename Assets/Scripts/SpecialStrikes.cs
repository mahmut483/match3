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
// Hücreleri temizleme işi bilerek burada değil: PotionBoard.RunStrike'a bir
// Vector2Int listesi veriliyor, tahtanın iç yapısına (Node dizisi, zincir
// sayacı) bu sınıf hiç dokunmuyor. "Hangi hücreler" oyun tasarımı sorusu ve sık
// değişir; "nasıl temizlenir" tahta mekaniği ve sabit.
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

        List<Vector2Int> cells = CellsFor(armed.Value, new Vector2Int(potion.xIndex, potion.yIndex));

        if (cells.Count == 0) return false;

        slot.remaining--;

        // Bir seçim, bir vuruş. Seçim açık kalsaydı sonraki dokunuş farkında
        // olmadan ikinci hakkı harcardı.
        armed = null;

        Refresh();

        board.RunStrike(cells);

        return true;
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

            case StrikeKind.Cannon:
                for (int x = 0; x < board.Width; x++)
                {
                    if (x != origin.x) cells.Add(new Vector2Int(x, origin.y));
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
