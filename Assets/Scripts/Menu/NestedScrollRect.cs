using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Dikey liste (clan listesi, rank listesi) içindeyken yatay parmak hareketini
// üstteki sayfa kaydırmasına devreder. Böylece liste üzerinde de sayfa geçişi yapılabilir.
public class NestedScrollRect : ScrollRect
{
    private GameObject parentTarget;
    private bool routeToParent;

    protected override void Awake()
    {
        base.Awake();

        if (transform.parent != null)
        {
            ScrollRect parentScroll = transform.parent.GetComponentInParent<ScrollRect>();

            if (parentScroll != null) parentTarget = parentScroll.gameObject;
        }
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        // Basma noktasından bu yana hangi eksende daha çok gidildi?
        Vector2 drag = eventData.position - eventData.pressPosition;

        routeToParent = Mathf.Abs(drag.x) > Mathf.Abs(drag.y);

        if (routeToParent && parentTarget != null)
        {
            // ScrollRect'e değil, üst objedeki TÜM dinleyicilere gönder.
            // PageSnap de bu olayları dinlediği için kademeli oturtma çalışmaya devam eder.
            ExecuteEvents.Execute(parentTarget, eventData, ExecuteEvents.beginDragHandler);
        }
        else
        {
            base.OnBeginDrag(eventData);
        }
    }

    public override void OnDrag(PointerEventData eventData)
    {
        if (routeToParent && parentTarget != null)
        {
            ExecuteEvents.Execute(parentTarget, eventData, ExecuteEvents.dragHandler);
        }
        else
        {
            base.OnDrag(eventData);
        }
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        if (routeToParent && parentTarget != null)
        {
            ExecuteEvents.Execute(parentTarget, eventData, ExecuteEvents.endDragHandler);
        }
        else
        {
            base.OnEndDrag(eventData);
        }

        routeToParent = false;
    }
}
