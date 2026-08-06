using UnityEngine;

public class OutOfMoveController : MonoBehaviour
{

    [SerializeField] private GameObject losePanel;
    void OutOfMoveAnimation()
    {
        gameObject.SetActive(false);
        losePanel.SetActive(true);
    }
}
