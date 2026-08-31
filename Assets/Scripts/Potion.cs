
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

    // Taşın kendi görseli. Bombaya dönüşünce gizlenir — bombanın saydam
    // kenarlarından alttaki renk sızmasın diye.
    [SerializeField] private SpriteRenderer potionVisual;

    // Bombadayken kullanılacak seçim çerçevesi. Atanmazsa bomba seçiliyken
    // hiç çerçeve gösterilmez (taşın çerçevesi bombayı örtmediği için).
    [SerializeField] private GameObject bombSelectedVisual;

    private void Awake()
    {
        originalPotionType = potionType;

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
        StartCoroutine(MoveCoroutine(_targetPos, swapSpeed));
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

        // Bomba açıkken taşın görseli kapalı, kapalıyken geri açılır.
        if (potionVisual != null) potionVisual.enabled = !setActive;

        // Durum değişirken açık kalmış seçim çerçevesi kalmasın.
        if (selectedVisual != null) selectedVisual.SetActive(false);
        if (bombSelectedVisual != null) bombSelectedVisual.SetActive(false);
    }

    public void MoveToDown(Vector2 _targetPos, float startDelay = 0f)
    {
        StartCoroutine(MoveCoroutine(_targetPos, downSpeed, startDelay));
    }

    private IEnumerator MoveCoroutine(Vector2 _targetPos, float duration, float startDelay = 0f)
    {
        isMoving = true;

        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

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
        isMoving = false;
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
