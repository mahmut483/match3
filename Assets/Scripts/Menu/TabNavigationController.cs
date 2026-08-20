using UnityEngine;
using UnityEngine.UI;

public class TabNavigationController : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        [Tooltip("Tab objesi (ShopTab, RankTab...). Icon/Title alt objeleri otomatik bulunur.")]
        public RectTransform root;

        [Tooltip("Bu tab hangi sayfayı açar? Sayfa objesini sürükleyin. " +
                 "Sayfası yoksa boş bırakın — tab pasif görünür.")]
        public RectTransform page;

        // Aşağıdakiler Start'ta root'un içinden bulunur.
        [System.NonSerialized] public Button button;
        [System.NonSerialized] public Image background;
        [System.NonSerialized] public RectTransform icon;
        [System.NonSerialized] public CanvasGroup title;
        [System.NonSerialized] public CanvasGroup highlight; // opsiyonel "Highlight" alt objesi
        [System.NonSerialized] public LayoutElement layout;
    }

    [Header("References")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private PageSnap pageSnap;

    [Header("Tabs")]
    [SerializeField] private Tab[] tabs;

    [Header("Selected Effect")]
    [SerializeField] private float normalScale = 1.75f;
    [SerializeField] private float selectedScale = 2f;
    [SerializeField] private float selectedYOffset = 25f;

    [Tooltip("Seçili tab ne kadar genişlesin? 0.6 = diğerlerinin 1.6 katı. " +
             "Bar'daki HorizontalLayoutGroup'ta Control Child Size > Width işaretli olmalı.")]
    [SerializeField] private float selectedExtraWidth = 0.6f;

    [Header("Tab Colors")]
    [SerializeField] private Color normalColor = new Color(0.55f, 0.05f, 0.18f, 1f);

    [SerializeField] private Color selectedColor = new Color(0.85f, 0.12f, 0.32f, 1f);

    private Vector2[] normalIconPositions;

    // Sayfa sayısı — tab seçimi buna göre hesaplanır.
    private int pageCount;

    private void Start()
    {
        if (scrollRect == null)
        {
            Debug.LogError("ScrollRect bağlı değil!");
            return;
        }

        if (pageSnap == null)
        {
            Debug.LogError("PageSnap bağlı değil!");
            return;
        }

        if (tabs == null || tabs.Length == 0)
        {
            Debug.LogError("Tab listesi boş!");
            return;
        }

        Canvas.ForceUpdateCanvases();

        pageCount = scrollRect.content.childCount;

        normalIconPositions = new Vector2[tabs.Length];

        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            Tab tab = tabs[i];

            if (tab.root == null)
            {
                Debug.LogError($"Tab {i}: Root atanmamış!");
                continue;
            }

            // Alt objeleri ve bileşenleri kendisi bulur — elle sürüklemeye gerek yok.
            tab.button = tab.root.GetComponent<Button>();
            tab.background = tab.root.GetComponent<Image>();
            tab.icon = tab.root.Find("Icon") as RectTransform;

            Transform titleTransform = tab.root.Find("Title");
            tab.title = titleTransform != null ? titleTransform.GetComponent<CanvasGroup>() : null;

            // Seçiliyken beliren açık renkli panel — varsa kullanılır, yoksa sorun değil.
            Transform highlightTransform = tab.root.Find("Highlight");
            tab.highlight = highlightTransform != null ? highlightTransform.GetComponent<CanvasGroup>() : null;

            // Genişlik animasyonu için LayoutElement gerekli; yoksa eklenir.
            tab.layout = tab.root.GetComponent<LayoutElement>();

            if (tab.layout == null)
            {
                tab.layout = tab.root.gameObject.AddComponent<LayoutElement>();
            }

            if (tab.icon != null)
            {
                normalIconPositions[i] = tab.icon.anchoredPosition;
            }

            if (tab.title == null)
            {
                Debug.LogWarning($"{tab.root.name}: 'Title' alt objesi ya da CanvasGroup'u yok.");
            }

            if (tab.page != null && tab.page.parent != scrollRect.content)
            {
                Debug.LogWarning($"{tab.root.name}: {tab.page.name} Content'in altında değil.");
            }

            if (tab.button != null)
            {
                tab.button.onClick.AddListener(() => GoToTabPage(index));
            }
        }

        // Scroll hareketini dinle
        scrollRect.onValueChanged.AddListener(OnScroll);

        // Başlangıç görünümü
        UpdateTabs(scrollRect.horizontalNormalizedPosition);
    }

    private void OnDestroy()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(OnScroll);
        }

        if (tabs != null)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i].button != null) tabs[i].button.onClick.RemoveAllListeners();
            }
        }
    }

    // Tab'ın açtığı sayfanın Content içindeki sırası. Sayfa atanmamışsa -1.
    private static int PageIndexOf(Tab tab)
    {
        return tab.page != null ? tab.page.GetSiblingIndex() : -1;
    }

    // TAB'A BASILDIĞINDA — dizideki sırası değil, tab'ın kendi sayfası kullanılır.
    private void GoToTabPage(int tabIndex)
    {
        int pageIndex = PageIndexOf(tabs[tabIndex]);

        if (pageIndex < 0 || pageIndex >= pageCount) return;

        pageSnap.GoToPage(pageIndex);
    }

    // SAYFA KAYDIRILDIĞINDA
    private void OnScroll(Vector2 scrollPosition)
    {
        UpdateTabs(scrollPosition.x);
    }

    private void UpdateTabs(float normalizedPosition)
    {
        if (tabs == null || tabs.Length == 0) return;

        normalizedPosition = Mathf.Clamp01(normalizedPosition);

        if (pageCount <= 1)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                SetTabSelection(i, PageIndexOf(tabs[i]) == 0 ? 1f : 0f);
            }

            return;
        }

        // Scroll'un 0-1 değerini sayfa numarasına çevir.
        // Bölen SAYFA sayısıdır — PageSnap de aynı hesabı kullanır.
        //
        // 3 sayfa örneği: 0 = ilk sayfa, 0.5 = ikinci, 1 = üçüncü
        float pagePosition = normalizedPosition * (pageCount - 1);

        for (int i = 0; i < tabs.Length; i++)
        {
            int pageIndex = PageIndexOf(tabs[i]);

            // Sayfası olmayan tab hiçbir zaman seçili görünmez.
            if (pageIndex < 0 || pageIndex >= pageCount)
            {
                SetTabSelection(i, 0f);
                continue;
            }

            // Tab'ın sayfası ile mevcut scroll pozisyonu arasındaki mesafe.
            // 1 = tamamen seçili, 0 = seçili değil
            float selection = Mathf.Clamp01(1f - Mathf.Abs(pagePosition - pageIndex));

            SetTabSelection(i, selection);
        }
    }

    private void SetTabSelection(int index, float selection)
    {
        Tab tab = tabs[index];

        if (tab.icon != null)
        {
            tab.icon.localScale = Vector3.one * Mathf.Lerp(normalScale, selectedScale, selection);

            Vector2 position = normalIconPositions[index];
            position.y += selectedYOffset * selection;
            tab.icon.anchoredPosition = position;
        }

        if (tab.title != null)
        {
            tab.title.alpha = selection;
        }

        if (tab.highlight != null)
        {
            tab.highlight.alpha = selection;
        }

        if (tab.layout != null)
        {
            // Layout group boş alanı flexibleWidth oranında paylaştırır:
            // seçili tab daha büyük pay alır, toplam genişlik hep bar'a tam oturur.
            tab.layout.flexibleWidth = 1f + selectedExtraWidth * selection;
        }

        if (tab.background != null)
        {
            tab.background.color = Color.Lerp(normalColor, selectedColor, selection);
        }
    }
}
