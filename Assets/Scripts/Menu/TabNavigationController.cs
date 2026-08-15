using UnityEngine;
using UnityEngine.UI;

public class TabNavigationController : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        public Button button;
        public RectTransform icon;
        public CanvasGroup title;
        public Image background;
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

    [Header("Tab Colors")]
    [SerializeField] private Color normalColor = new Color(0.55f, 0.05f, 0.18f, 1f);

    [SerializeField] private Color selectedColor = new Color(0.85f, 0.12f, 0.32f, 1f);

    private Vector2[] normalIconPositions;

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

        normalIconPositions = new Vector2[tabs.Length];

        // TABLARI HAZIRLA
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;

            if (tabs[i].icon != null)
            {
                normalIconPositions[i] = tabs[i].icon.anchoredPosition;
            }

            if (tabs[i].button != null)
            {
                tabs[i].button.onClick.AddListener(() => GoToPage(index));
            }
        }

        // Scroll hareketini dinle
        scrollRect.onValueChanged.AddListener(OnScroll);

        // Başlangıç görünümü
        UpdateTabs(
            scrollRect.horizontalNormalizedPosition
        );
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

    // TAB'A BASILDIĞINDA
    private void GoToPage(int pageIndex)
    {
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

        // Tek tab varsa direkt seçili
        if (tabs.Length == 1)
        {
            SetTabSelection(0, 1f);
            return;
        }

        // Scroll 0-1 değerini sayfa pozisyonuna çevir
        //
        // 3 tab örneği:
        //
        // 0   = Play
        // 0.5 = Clan
        // 1   = Profile

        float pagePosition = normalizedPosition * (tabs.Length - 1);

        for (int i = 0; i < tabs.Length; i++)
        {
            // Tab ile mevcut scroll pozisyonu arasındaki mesafe
            float distance = Mathf.Abs(pagePosition - i);

            // 1 = tamamen seçili
            // 0 = seçili değil
            float selection = Mathf.Clamp01(1f - distance);

            SetTabSelection(i, selection);
        }
    }

    private void SetTabSelection(
        int index,
        float selection)
    {
        Tab tab = tabs[index];

        // -------------------------
        // ICON SCALE
        // -------------------------

        if (tab.icon != null)
        {
            float scale = Mathf.Lerp(normalScale, selectedScale, selection);

            tab.icon.localScale = Vector3.one * scale;


            // -------------------------
            // ICON Y POSITION
            // -------------------------

            Vector2 position = normalIconPositions[index];

            position.y += selectedYOffset * selection;

            tab.icon.anchoredPosition = position;
        }


        // -------------------------
        // TITLE ALPHA
        // -------------------------

        if (tab.title != null)
        {
            tab.title.alpha = Mathf.Lerp( 0f, 1f, selection);
        }


        // -------------------------
        // TAB BACKGROUND COLOR
        // -------------------------

        if (tab.background != null)
        {
            tab.background.color = Color.Lerp(normalColor, selectedColor, selection);
        }
    }
}