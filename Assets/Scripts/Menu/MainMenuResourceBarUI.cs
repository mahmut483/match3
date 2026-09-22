using System;
using System.Collections;
using TMPro;
using UnityEngine;

using Match3.Backend;

namespace Match3.Menu
{
    // MainMenu üst barındaki can ve coin değerlerini kullanıcı verisiyle eşler;
    // can eksikken yenilenme sayacını gösterir ve süre dolunca canı yeniler.
    public sealed class MainMenuResourceBarUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text heartText;
        [SerializeField] private TMP_Text coinText;

        private Coroutine refillCountdownRoutine;

        private void OnEnable()
        {
            FirebaseBootstrap.UserReady += HandleUserUpdated;
            RefreshCurrentUser();
        }

        private void OnDisable()
        {
            FirebaseBootstrap.UserReady -= HandleUserUpdated;
            StopRefillCountdown();
        }

        private void RefreshCurrentUser()
        {
            FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;
            HandleUserUpdated(bootstrap != null && bootstrap.IsReady ? bootstrap.User : null);
        }

        private void HandleUserUpdated(UserData user)
        {
            int lives = user != null ? user.lives : 0;
            int coins = user != null ? user.gold : 0;

            if (coinText != null) coinText.text = FormatCoins(coins);

            if (user == null || lives >= LifeRules.MaxLives)
            {
                StopRefillCountdown();
                if (heartText != null) heartText.text = FormatLives(lives, LifeRules.MaxLives);
                return;
            }

            if (refillCountdownRoutine == null)
            {
                refillCountdownRoutine = StartCoroutine(RefillCountdown());
            }
        }

        private IEnumerator RefillCountdown()
        {
            WaitForSecondsRealtime tick = new WaitForSecondsRealtime(0.2f);

            while (isActiveAndEnabled)
            {
                FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;
                UserData user = bootstrap != null && bootstrap.IsReady ? bootstrap.User : null;

                if (user == null || user.lives >= LifeRules.MaxLives)
                {
                    break;
                }

                int remainingSeconds = LifeRefillTimer.GetRemainingSeconds(
                    user.livesUpdatedAt.ToDateTime(),
                    DateTime.UtcNow,
                    LifeRules.RegenSeconds);

                if (heartText != null)
                {
                    heartText.text = LifeRefillTimer.FormatStatus(
                        user.lives,
                        LifeRules.MaxLives,
                        remainingSeconds);
                }

                // Süre doldu: can eklenir, UserReady tetiklenir ve gerekirse sayaç yeniden başlar.
                if (remainingSeconds <= 0)
                {
                    refillCountdownRoutine = null;
                    bootstrap.RegenerateLives();
                    yield break;
                }

                yield return tick;
            }

            refillCountdownRoutine = null;
        }

        private void StopRefillCountdown()
        {
            if (refillCountdownRoutine == null) return;

            StopCoroutine(refillCountdownRoutine);
            refillCountdownRoutine = null;
        }

        public static string FormatLives(int lives, int maximum)
        {
            int safeMaximum = Mathf.Max(1, maximum);
            return $"{Mathf.Clamp(lives, 0, safeMaximum)}/{safeMaximum}";
        }

        public static string FormatCoins(int coins)
        {
            return Mathf.Max(0, coins).ToString();
        }
    }
}
