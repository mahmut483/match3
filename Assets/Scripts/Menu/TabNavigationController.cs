using UnityEngine;
using UnityEngine.UI;

namespace Match3.Menu
{
    public class TabNavigationController : MonoBehaviour
    {
        [System.Serializable]
        public class Tab
        {
            [Tooltip("Tab objesi (RankTab, HomeTab...). Icon/Title alt objeleri otomatik bulunur.")]
            public RectTransform root;

            [Tooltip("Bu tab hangi sayfayı açar? Sayfa objesini sürükleyin. " +
                     "Sayfası yoksa boş bırakın — tab pasif görünür.")]
            public RectTransform page;

            // Aşağıdakiler Start'ta root'un içinden bulunur.
            [System.NonSerialized] public Button button;
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

        [Header("Sliding Selection")]
        [Tooltip("Sayfa kaydırılırken tabların arasında hareket eden açık renkli plaka.")]
        [SerializeField] private RectTransform selectionIndicator;

        [SerializeField] private float indicatorYOffset;

        [Header("Selected Effect")]
        [SerializeField] private float normalScale = 1.75f;
        [SerializeField] private float selectedScale = 2f;
        [SerializeField] private float selectedYOffset = 25f;

        [Tooltip("Seçili tab ne kadar genişlesin? 0.6 = diğerlerinin 1.6 katı. " +
                 "Bar'daki HorizontalLayoutGroup'ta Control Child Size > Width işaretli olmalı.")]
        [SerializeField] private float selectedExtraWidth = 0.6f;

        [Header("Tab Color")]
        [Tooltip("Tab zeminlerinin sabit rengi. Seçili zemini kayan plaka verir.")]
        [SerializeField] private Color normalColor = new Color(0.55f, 0.05f, 0.18f, 1f);

        private Vector2[] normalIconPositions;

        private RectTransform navigationRoot;

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

            navigationRoot = transform as RectTransform;

            if (selectionIndicator != null)
            {
                // Inspector'da verilen başlangıç ölçüsünü kilitle. Canvas veya tab
                // genişliği değişse bile plaka yalnızca hareket eder, ölçeklenmez.
                Vector2 fixedIndicatorSize = selectionIndicator.rect.size;
                selectionIndicator.anchorMin = new Vector2(0.5f, 0.5f);
                selectionIndicator.anchorMax = new Vector2(0.5f, 0.5f);
                selectionIndicator.pivot = new Vector2(0.5f, 0.5f);
                selectionIndicator.sizeDelta = fixedIndicatorSize;

                // Layout plakanın genişliğini yönetmemeli; yalnızca tablar yerleşime katılır.
                LayoutElement indicatorLayout = selectionIndicator.GetComponent<LayoutElement>();

                if (indicatorLayout == null)
                {
                    indicatorLayout = selectionIndicator.gameObject.AddComponent<LayoutElement>();
                }

                indicatorLayout.ignoreLayout = true;

                Image indicatorImage = selectionIndicator.GetComponent<Image>();
                if (indicatorImage != null) indicatorImage.raycastTarget = false;

                // Plaka tab zeminlerinin üstünde çizilir. İkon ve başlıklar aşağıda
                // ayrı canvas'a alınarak plakanın üstünde kalır.
                selectionIndicator.SetAsLastSibling();
            }

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
                tab.icon = tab.root.Find("Icon") as RectTransform;

                Image background = tab.root.GetComponent<Image>();
                if (background != null) background.color = normalColor;

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
                    KeepAboveIndicator(tab.icon);
                }

                if (titleTransform != null) KeepAboveIndicator(titleTransform as RectTransform);

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

            RebuildAndUpdateIndicator(pagePosition);
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
        }

        private void RebuildAndUpdateIndicator(float pagePosition)
        {
            if (selectionIndicator == null || navigationRoot == null) return;

            // FlexibleWidth değerleri bu karede değiştiği için merkezleri okumadan önce
            // yerleşimi yenile. Üç tab için maliyeti çok düşüktür ve kaymayı bire bir tutar.
            LayoutRebuilder.ForceRebuildLayoutImmediate(navigationRoot);

            int leftPage = Mathf.Clamp(Mathf.FloorToInt(pagePosition), 0, Mathf.Max(0, pageCount - 1));
            int rightPage = Mathf.Clamp(Mathf.CeilToInt(pagePosition), 0, Mathf.Max(0, pageCount - 1));
            float blend = Mathf.Clamp01(pagePosition - leftPage);

            RectTransform leftTab = FindTabForPage(leftPage);
            RectTransform rightTab = FindTabForPage(rightPage);

            if (leftTab == null) leftTab = rightTab;
            if (rightTab == null) rightTab = leftTab;

            if (leftTab == null)
            {
                selectionIndicator.gameObject.SetActive(false);
                return;
            }

            selectionIndicator.gameObject.SetActive(true);

            Vector3 indicatorWorldPosition = Vector3.Lerp(leftTab.position, rightTab.position, blend);
            selectionIndicator.position = indicatorWorldPosition;

            Vector2 indicatorPosition = selectionIndicator.anchoredPosition;
            indicatorPosition.y += indicatorYOffset;
            selectionIndicator.anchoredPosition = indicatorPosition;
        }

        private RectTransform FindTabForPage(int pageIndex)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                if (PageIndexOf(tabs[i]) == pageIndex) return tabs[i].root;
            }

            return null;
        }

        private static void KeepAboveIndicator(RectTransform target)
        {
            if (target == null) return;

            Canvas canvas = target.GetComponent<Canvas>();

            if (canvas == null)
            {
                canvas = target.gameObject.AddComponent<Canvas>();
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = 1;
        }
    }
}
