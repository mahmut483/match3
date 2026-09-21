using System;
using System.Collections;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

using Match3.Backend;

namespace Match3.Menu
{
    // MainMenu üst barındaki can ve coin değerlerini kullanıcı verisiyle eşler.
    public sealed class MainMenuResourceBarUI : MonoBehaviour
    {
        private const string HeartTextObjectName = "heartTMP";
        private const string CoinTextObjectName = "CoinTMP";

        [SerializeField] private TMP_Text heartText;
        [SerializeField] private TMP_Text coinText;
        [SerializeField, Min(1)] private int maximumLives = 5;

        private Coroutine refillCountdownRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            MainMenuResourceBarUI[] existingBars =
                Object.FindObjectsByType<MainMenuResourceBarUI>(FindObjectsInactive.Include);

            if (existingBars.Length > 0)
            {
                existingBars[0].RefreshCurrentUser();
                return;
            }

            TMP_Text foundHeartText = null;
            TMP_Text foundCoinText = null;

            foreach (TMP_Text text in
                     Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            {
                if (text.gameObject.name == HeartTextObjectName)
                {
                    foundHeartText = text;
                }
                else if (text.gameObject.name == CoinTextObjectName)
                {
                    foundCoinText = text;
                }
            }

            if (foundHeartText == null || foundCoinText == null)
            {
                return;
            }

            GameObject host = foundHeartText.canvas != null
                ? foundHeartText.canvas.gameObject
                : foundHeartText.gameObject;
            MainMenuResourceBarUI resourceBar = host.AddComponent<MainMenuResourceBarUI>();
            resourceBar.heartText = foundHeartText;
            resourceBar.coinText = foundCoinText;
            resourceBar.RefreshCurrentUser();
        }

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

            if (user == null || lives > 0)
            {
                StopRefillCountdown();
                if (heartText != null) heartText.text = FormatLives(lives, maximumLives);
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

                if (user == null || user.lives > 0)
                {
                    break;
                }

                int remainingSeconds = LifeRefillTimer.GetRemainingSeconds(
                    user.livesUpdatedAt.ToDateTime(),
                    DateTime.UtcNow,
                    LifeRefillTimer.DurationSeconds);

                if (heartText != null)
                {
                    heartText.text = LifeRefillTimer.FormatStatus(
                        user.lives,
                        maximumLives,
                        remainingSeconds);
                }

                if (remainingSeconds <= 0)
                {
                    refillCountdownRoutine = null;
                    bootstrap.RefillLivesToFull(maximumLives);
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
