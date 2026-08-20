using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(ScrollRect))]
public class PageSnap : MonoBehaviour,
    IBeginDragHandler,
    IEndDragHandler
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private float snapSpeed = 12f;

    // Menü açıldığında gösterilecek sayfa. Objeyi sürükleyin — sırası
    // otomatik bulunur, sayfa ekleyip çıkarınca bozulmaz.
    [SerializeField] private RectTransform startPage;

    private int pageCount;
    private int currentPage;

    private float targetPosition;

    private bool isDragging;
    private bool isSnapping;

    private void Awake()
    {
        if (scrollRect == null)
            scrollRect = GetComponent<ScrollRect>();
    }

    // ScrollView'un içerik boyutu ilk karede henüz hesaplanmamış olabiliyor;
    // konum ataması o an işe yaramaz ve sayfa 0'da kalır. Layout oturduktan
    // sonra bir kez daha uygulanır.
    private IEnumerator Start()
    {
        pageCount = scrollRect.content.childCount;

        int startIndex = 0;

        if (startPage != null)
        {
            if (startPage.parent != scrollRect.content)
            {
                Debug.LogWarning($"PageSnap: {startPage.name} Content'in altında değil.");
            }

            startIndex = startPage.GetSiblingIndex();
        }

        ApplyStartPage(startIndex);

        // Layout group'lar bir kare sonra kesinleşiyor; konumu tekrar uygula.
        yield return null;

        ApplyStartPage(startIndex);
    }

    // Kaydırma konumu ancak Content'in genişliği hesaplandıktan sonra anlam kazanır.
    // ForceUpdateCanvases tek başına LayoutGroup'ları yeniden kurmuyor.
    private void ApplyStartPage(int startIndex)
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

        GoToPage(startIndex, true);
    }

    private void Update()
    {
        if (!isSnapping || isDragging)
            return;

        scrollRect.horizontalNormalizedPosition =
            Mathf.Lerp(
                scrollRect.horizontalNormalizedPosition,
                targetPosition,
                snapSpeed * Time.unscaledDeltaTime
            );

        if (Mathf.Abs(
            scrollRect.horizontalNormalizedPosition -
            targetPosition) < 0.001f)
        {
            scrollRect.horizontalNormalizedPosition =
                targetPosition;

            isSnapping = false;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        isSnapping = false;

        scrollRect.StopMovement();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;

        float position =
            scrollRect.horizontalNormalizedPosition;

        int page = Mathf.RoundToInt(
            position * (pageCount - 1)
        );

        GoToPage(page);
    }

    public void GoToPage(int pageIndex)
    {
        GoToPage(pageIndex, false);
    }

    public void GoToPage(int pageIndex, bool instant)
    {
        pageIndex = Mathf.Clamp(
            pageIndex,
            0,
            pageCount - 1
        );

        currentPage = pageIndex;

        if (pageCount <= 1)
            targetPosition = 0f;
        else
            targetPosition =
                (float)pageIndex / (pageCount - 1);

        scrollRect.StopMovement();

        isDragging = false;

        if (instant)
        {
            scrollRect.horizontalNormalizedPosition = targetPosition;
            isSnapping = false;
        }
        else
        {
            isSnapping = true;
        }
    }
}