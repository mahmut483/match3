using UnityEngine;
using UnityEngine.InputSystem;
using Match3.Gameplay.Potions;
using Match3.Gameplay.Session;

namespace Match3.Gameplay.Board
{
    // Pointer'dan taş seçimi: basılıyken komşu taşa sürüklemek takas, bırakmak
    // dokunma. Kabul kuralları (kilit, vuruş, oyun sonu) PotionBoard'un kapılarından
    // okunur; tahta mantığı burada yok.
    public sealed class BoardInput : MonoBehaviour
    {
        [SerializeField] private PotionBoard board;

        private Potion firstSelectedPotion;
        private Potion secondSelectedPotion;
        private bool waitForPointerRelease;

        private void Update()
        {
            if (Pointer.current == null) return;

            if (GameManager.Instance.IsGameEnded)
            {
                // Oyun Cannon hedef beklerken bittiyse kaymış board eve döner.
                if (board.IsAwaitingCannonTarget) board.TryCancelCannonAim();
                return;
            }

            if (!board.AcceptsInput)
            {
                ClearPointerSelection();
                return;
            }

            if (waitForPointerRelease && !Pointer.current.press.isPressed)
            {
                waitForPointerRelease = false;
            }

            if (Pointer.current.press.isPressed && !waitForPointerRelease)
            {
                Ray ray = Camera.main.ScreenPointToRay(Pointer.current.position.ReadValue());
                RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

                if (hit.collider != null && hit.collider.TryGetComponent(out Potion potion))
                {
                    if (firstSelectedPotion == null)
                    {
                        firstSelectedPotion = potion;
                        potion.SetSelectedVisual(true);
                    }

                    if (potion != firstSelectedPotion)
                    {
                        secondSelectedPotion = potion;
                    }

                    if (!board.IsAwaitingCannonTarget && firstSelectedPotion != null && secondSelectedPotion != null &&
                        board.TrySwap(firstSelectedPotion, secondSelectedPotion))
                    {
                        FinishPointerSwap();
                    }
                }
            }
            else
            {
                // Parmak kalktı. İkinci bir taşa hiç değilmediyse bu bir dokunmadır;
                // takas olduysa FinishPointerSwap referansları zaten temizlemiştir.
                Potion tapped = secondSelectedPotion == null ? firstSelectedPotion : null;

                if (firstSelectedPotion != null) firstSelectedPotion.SetSelectedVisual(false);

                firstSelectedPotion = null;
                secondSelectedPotion = null;

                board.TryTap(tapped);
            }
        }

        // Kilitliyken seçim düşer; parmak hâlâ basılıysa kalkana kadar yeni seçim yok.
        private void ClearPointerSelection()
        {
            if (firstSelectedPotion != null) firstSelectedPotion.SetSelectedVisual(false);

            firstSelectedPotion = null;
            secondSelectedPotion = null;
            waitForPointerRelease = Pointer.current.press.isPressed;
        }

        // Takas başladı: aynı basışla ikinci bir takas olmasın.
        private void FinishPointerSwap()
        {
            firstSelectedPotion = null;
            secondSelectedPotion = null;
            waitForPointerRelease = true;
        }
    }
}
