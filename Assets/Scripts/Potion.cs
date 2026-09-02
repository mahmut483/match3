
using System.Collections;
using UnityEngine;

public class Potion : MonoBehaviour
{

    // Değerler
    public PotionType potionType;
    private PotionType originalPotionType;

    public int xIndex;
    public int yIndex;

    public bool isMatched;
    public bool isMoving;

    public Vector2 currentPos;
    public Vector2 targetPos;

    private float swapSpeed = .12f;
    private float downSpeed = .24f;

    [SerializeField] private GameObject selectedVisual;
    [SerializeField] private GameObject bomb;

    // Bombanın yere düşen gölgesi. bomb'un child'ı DEĞİL (onun animasyonlu
    // transformunu miras almasın diye), o yüzden aç/kapa işi elle yapılır.
    // Yalnızca süper bomba patlarken görünür; onu PotionBoard açar, burası
    // her durum değişiminde kapatarak temiz bir başlangıç garantiler.
    [SerializeField] private GameObject bombShadow;

    // Taşın kendi görseli. Bombaya dönüşünce gizlenir — bombanın saydam
    // kenarlarından alttaki renk sızmasın diye.
    [SerializeField] private SpriteRenderer potionVisual;

    // Bombadayken kullanılacak seçim çerçevesi. Atanmazsa bomba seçiliyken
    // hiç çerçeve gösterilmez (taşın çerçevesi bombayı örtmediği için).
    [SerializeField] private GameObject bombSelectedVisual;

    [Header("Düşüş esnemesi")]
    [Tooltip("Düşerken Y çarpanı. X ters oranda incelir, hacim korunmuş görünür.")]
    [SerializeField, Min(1f)] private float fallStretch = 1.12f;

    [Tooltip("Yere değince X çarpanı. Y ters oranda basılır.")]
    [SerializeField, Min(1f)] private float landSquash = 1.15f;

    [Tooltip("Squash'tan normal ölçeğe dönüş süresi.")]
    [SerializeField, Min(0f)] private float landRecover = 0.12f;

    [Tooltip("Eşleşen taşın kırılmadan önce sıfıra küçülme süresi.")]
    [SerializeField, Min(0f)] private float matchShrinkDuration = 0.09f;

    // Prefabdaki ölçek. Esneme hep bunun üzerine uygulanır ki
    // havuzdan çıkan taş bir öncekinin ölçeğini taşımasın.
    private Vector3 baseScale;

    // Çalışan hareket coroutine'i. Yeni hedef verilmeden önce durdurulur.
    private Coroutine moveRoutine;

    private void Awake()
    {
        originalPotionType = potionType;
        baseScale = transform.localScale;

        if (potionVisual == null) potionVisual = GetComponent<SpriteRenderer>();
    }

    public void setSelectedVisual(bool isPressing)
    {
        bool isBomb = potionType == PotionType.Bomb;

        // Bombanın kendi çerçevesi varsa onu, yoksa taşınkini kullan.
        if (isBomb)
        {
            if (selectedVisual != null) selectedVisual.SetActive(false);

            if (bombSelectedVisual != null) bombSelectedVisual.SetActive(isPressing);

            return;
        }

        if (bombSelectedVisual != null) bombSelectedVisual.SetActive(false);

        if (selectedVisual != null) selectedVisual.SetActive(isPressing);
    }


    public void SetIndicies(int _x, int _y)
    {
        xIndex = _x;
        yIndex = _y;
    }

    //MoveToTarget
    public void MoveToTarget(Vector2 _targetPos)
    {
        StartMove(_targetPos, swapSpeed, 0f);
    }

    public void Bomb(bool setActive)
    {
        if (setActive)
        {
            potionType = PotionType.Bomb;
            bomb.SetActive(true);
        }
        else
        {
            potionType = originalPotionType;
            bomb.SetActive(false);
        }

        if (bombShadow != null) bombShadow.SetActive(false);

        // Bomba açıkken taşın görseli kapalı, kapalıyken geri açılır.
        if (potionVisual != null) potionVisual.enabled = !setActive;

        // Durum değişirken açık kalmış seçim çerçevesi kalmasın.
        if (selectedVisual != null) selectedVisual.SetActive(false);
        if (bombSelectedVisual != null) bombSelectedVisual.SetActive(false);
    }

    public void MoveToDown(Vector2 _targetPos, float startDelay = 0f)
    {
        StartMove(_targetPos, downSpeed, startDelay, true);
    }

    // Taş hareket hâlindeyken yeni bir hedef alabiliyor (cascade sürerken yapılan
    // takas gibi). İki MoveCoroutine aynı anda transform'a yazar ve hangisi önce
    // biterse isMoving'i temizler — bekleyen kod yanlış anda devam eder.
    // Bu yüzden yeni hareket başlamadan önce eskisi kesilir.
    private void StartMove(Vector2 _targetPos, float duration, float startDelay, bool stretch = false)
    {
        if (moveRoutine != null) StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(MoveCoroutine(_targetPos, duration, startDelay, stretch));
    }

    // Havuza dönerken obje kapanır ve coroutine'ler durur; elde kalan referans
    // temizlenmezse taş yeniden kullanıldığında geçersiz bir handle taşır.
    private void OnDisable()
    {
        moveRoutine = null;
        isMoving = false;

        // Esneme yarıda kalmış olabilir; havuzdan çıkarken temiz başlasın.
        transform.localScale = baseScale;
    }

    private IEnumerator MoveCoroutine(Vector2 _targetPos, float duration, float startDelay = 0f, bool stretch = false)
    {
        isMoving = true;

        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        // Düşerken uzar: Y büyür, X aynı oranda incelir.
        if (stretch) SetScale(1f / fallStretch, fallStretch);

        float elaspeed = 0f;
        Vector2 startPos = transform.position;

        while (elaspeed < duration)
        {
            elaspeed += Time.deltaTime;

            float t = Mathf.Clamp01(elaspeed / duration);

            transform.position = Vector2.Lerp(startPos, _targetPos, t);

            yield return null;
        }
        transform.position = _targetPos;

        // Yere değdi: X büyür, Y basılır; sonra normale yaylanır.
        if (stretch) yield return LandSquash();

        isMoving = false;
        moveRoutine = null;
    }

    // Squash anlık uygulanır, normale dönüş landRecover boyunca yumuşar.
    private IEnumerator LandSquash()
    {
        SetScale(landSquash, 1f / landSquash);

        float elapsed = 0f;
        Vector3 from = transform.localScale;

        while (elapsed < landRecover)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / landRecover);

            transform.localScale = Vector3.Lerp(from, baseScale, t);

            yield return null;
        }

        transform.localScale = baseScale;
    }

    // Eşleşen taş kırılmadan önce hızlıca sıfıra küçülür.
    // Havuza dönerken OnDisable ölçeği zaten baseScale'e sıfırlıyor.
    public IEnumerator ShrinkOut()
    {
        // Düşüş/squash coroutine'i de ölçeğe yazıyor; ikisi çakışmasın.
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
            isMoving = false;
        }

        float elapsed = 0f;
        Vector3 from = transform.localScale;

        while (elapsed < matchShrinkDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / matchShrinkDuration);

            transform.localScale = Vector3.Lerp(from, Vector3.zero, t);

            yield return null;
        }

        transform.localScale = Vector3.zero;
    }

    private void SetScale(float xFactor, float yFactor)
    {
        transform.localScale = new Vector3(baseScale.x * xFactor,
                                           baseScale.y * yFactor,
                                           baseScale.z);
    }
}

// PotionType enum
public enum PotionType
{
    Red,
    Blue,
    Yellow,
    Green,
    Bomb
}
