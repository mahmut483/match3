using UnityEngine;

// Sprite Mask hiyerarşiye bakmaz; aralığındaki HER sprite'ı etkiler.
// Bu yüzden bir bombanın maskesi diğer bombaları da keserdi.
//
// Çözüm: her bombaya tahtadaki hücresine göre benzersiz bir sorting order verip
// maskenin aralığını yalnızca o order'ın çevresine daraltmak. Böylece
//   - maske sadece kendi bombasını etkiler (aralıklar çakışmaz)
//   - çizim sırası hücreye bağlı olduğu için tutarlıdır (alt satırlar önde)
[RequireComponent(typeof(SpriteMask))]
public class BombMaskBinder : MonoBehaviour
{
    [Tooltip("Maskelenecek bomba sprite'ı. Boş bırakılırsa parent'tan alınır.")]
    [SerializeField] private SpriteRenderer targetRenderer;

    [Tooltip("Bombanın ÖNÜNDE durması gereken parçalar (fitil gibi). " +
             "Boş bırakılırsa bomba sprite'ının çocuklarından bulunur.")]
    [SerializeField] private SpriteRenderer[] frontRenderers;

    // Hücreler arası order boşluğu; aradaki pay maskenin aralığı olur.
    private const int OrderStep = 10;
    private const int MaskMargin = 4;

    // Satır numarasını tersine çevirmek için; tahtanın satır sayısından büyük olmalı.
    private const int RowSpan = 20;

    // Patlama sırasında bomba her şeyin önünde durmalı.
    private const int FrontOrder = 20000;

    // Fitil bombanın hemen ÖNÜNDE ve maskenin aralığının İÇİNDE olmalı:
    // SuperBomb klibi SquareMask'i kaydırarak fitili yakıyor, aralığın dışına
    // çıkarsa maske ona dokunamaz. +1 hem öne alır hem aralıkta tutar.
    private const int FrontPartOffset = 1;

    private SpriteMask mask;
    private Potion potion;
    private int appliedOrder = int.MinValue;
    private bool lockedToFront;

    private void Awake()
    {
        mask = GetComponent<SpriteMask>();

        if (targetRenderer == null && transform.parent != null)
        {
            targetRenderer = transform.parent.GetComponent<SpriteRenderer>();
        }

        potion = GetComponentInParent<Potion>();

        // Bombanın order'ı hücreye göre değişiyor. Fitil gibi çocuk parçalar
        // sabit order'da kalırsa bombanın ARKASINA düşer; prefabda önde
        // görünmelerinin sebebi order'ların o an eşit olması.
        if ((frontRenderers == null || frontRenderers.Length == 0) && targetRenderer != null)
        {
            frontRenderers = System.Array.FindAll(
                targetRenderer.GetComponentsInChildren<SpriteRenderer>(true),
                renderer => renderer != targetRenderer && renderer.gameObject != gameObject);
        }
    }

    private void OnEnable()
    {
        Apply();
    }

    private void OnDisable()
    {
        // Havuza dönerken kilit kalkar, sonraki kullanımda hücreye göre hesaplanır.
        lockedToFront = false;
        appliedOrder = int.MinValue;
    }

    // Süper bomba patlarken çağrılır: bomba tüm taşların ve diğer bombaların önüne alınır.
    public void LockToFront()
    {
        lockedToFront = true;
        ApplyOrder(FrontOrder);
    }

    // Bomba düşerken/takas olurken hücresi değişir; order da onunla güncellenir.
    private void LateUpdate()
    {
        Apply();
    }

    private void Apply()
    {
        if (lockedToFront) return;
        if (targetRenderer == null || potion == null) return;

        // Hücre başına benzersiz order. Alt satırlar daha büyük order alır,
        // yani üsttekilerin önünde çizilir.
        int cell = (RowSpan - potion.yIndex) * RowSpan + potion.xIndex;
        ApplyOrder(cell * OrderStep);
    }

    private void ApplyOrder(int order)
    {
        if (order == appliedOrder) return;

        appliedOrder = order;

        targetRenderer.sortingOrder = order;

        if (frontRenderers != null)
        {
            foreach (SpriteRenderer front in frontRenderers)
            {
                if (front != null) front.sortingOrder = order + FrontPartOffset;
            }
        }

        mask.isCustomRangeActive = true;
        mask.frontSortingLayerID = targetRenderer.sortingLayerID;
        mask.backSortingLayerID = targetRenderer.sortingLayerID;

        // Aralık tek noktaya sıkışırsa maske çalışmıyor; iki yana pay bırakılır.
        mask.frontSortingOrder = order + MaskMargin;
        mask.backSortingOrder = order - MaskMargin;
    }
}
