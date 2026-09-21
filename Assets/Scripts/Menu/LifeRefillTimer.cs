using System;
using UnityEngine;

namespace Match3.Menu
{
    // Can tamamen bittiğinde gösterilen kısa yenilenme sayacının saf hesapları.
    public static class LifeRefillTimer
    {
        public const int DurationSeconds = 10;

        public static int GetRemainingSeconds(
            DateTime startedAtUtc,
            DateTime nowUtc,
            int durationSeconds)
        {
            int safeDuration = Mathf.Max(0, durationSeconds);
            double elapsedSeconds = Math.Max(0, (nowUtc - startedAtUtc).TotalSeconds);
            return Mathf.Clamp(
                Mathf.CeilToInt((float)(safeDuration - elapsedSeconds)),
                0,
                safeDuration);
        }

        public static string FormatStatus(int lives, int maximum, int remainingSeconds)
        {
            int safeMaximum = Mathf.Max(1, maximum);
            int safeLives = Mathf.Clamp(lives, 0, safeMaximum);

            if (safeLives > 0)
            {
                return $"{safeLives}/{safeMaximum}";
            }

            int safeRemaining = Mathf.Max(0, remainingSeconds);
            return $"0/{safeMaximum} • {safeRemaining / 60:00}:{safeRemaining % 60:00}";
        }
    }
}
