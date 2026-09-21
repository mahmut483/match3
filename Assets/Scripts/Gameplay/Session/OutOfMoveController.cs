using UnityEngine;

namespace Match3.Gameplay.Session
{
    public class OutOfMoveController : MonoBehaviour
    {

        [SerializeField] private GameObject losePanel;
        void OutOfMoveAnimation()
        {
            gameObject.SetActive(false);
            losePanel.SetActive(true);
        }
    }
}
