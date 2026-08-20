using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Butona basılınca üzerindeki yazıyı küçültür, bırakınca eski boyutuna döndürür.
// Butonun kendisine eklenir; yazı atanmazsa alt objelerden otomatik bulunur.
public class ButtonPressScale : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler
{
    [SerializeField] private RectTransform target;

    [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.9f;

    private Vector3 normalScale;

    private void Awake()
    {
        if (target == null)
        {
            TMP_Text label = GetComponentInChildren<TMP_Text>(true);

            if (label != null) target = label.rectTransform;
        }

        if (target != null) normalScale = target.localScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (target == null) return;

        target.localScale = normalScale * pressedScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (target == null) return;

        target.localScale = normalScale;
    }
}
