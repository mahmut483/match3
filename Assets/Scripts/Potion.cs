
using System.Collections;
using UnityEngine;

public class Potion : MonoBehaviour
{

    // Değerler
   public PotionType potionType;

   public int xIndex;
   public int yIndex;

   public bool isMatched;
   public bool isMoving;

   public Vector2 currentPos;
   public Vector2 targetPos;

   public float swapSpeed = .5f;
   private float downSpeed = 1f;

   [SerializeField] private GameObject selectedVisual; 

    

    public void setSelectedVisual(bool isPressing)
    {
        if (isPressing)
        {
            selectedVisual.SetActive(true);
        }
        else
        {
            selectedVisual.SetActive(false);
        }
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

            float easadT = 1f - Mathf.Pow(1f - t, 9f);
            
            transform.position = Vector2.Lerp(startPos, _targetPos, easadT);

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
    Purple,
    Green,
    White
}
