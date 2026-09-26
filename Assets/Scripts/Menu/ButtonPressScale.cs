using UnityEngine;
using UnityEngine.EventSystems;

namespace Match3.Menu
{
    // Butona basılınca hedefi küçültür, bırakınca eski boyutuna döndürür.
    // Butonun kendisine eklenir; hedef boşsa butonun kendisi küçülür.
    public class ButtonPressScale : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] private RectTransform target;

        [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.9f;

        private Vector3 normalScale;

        private void Awake()
        {
            if (target == null) target = (RectTransform)transform;

            normalScale = target.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            target.localScale = normalScale * pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            target.localScale = normalScale;
        }
    }
}
