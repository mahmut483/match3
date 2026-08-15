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

    private void Start()
    {
        pageCount = scrollRect.content.childCount;

        currentPage = 0;
        targetPosition = 0f;

        scrollRect.horizontalNormalizedPosition = 0.5f;
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
        isSnapping = true;
    }
}