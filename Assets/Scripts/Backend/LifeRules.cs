using System;

namespace Match3.Backend
{
    // Can kuralları: üst sınır, yenilenme aralığı ve zamana göre kaç can kazanıldığı.
    // Saf hesap; Firestore'a yazan taraf FirebaseBootstrap.
    public static class LifeRules
    {
        public const int MaxLives = 5;
        public const int RegenSeconds = 60;

        // livesUpdatedAt'ten bu yana dolan her aralık bir can; üst sınırı aşmaz.
        public static int GainedLives(int lives, DateTime updatedAtUtc, DateTime nowUtc)
        {
            if (lives >= MaxLives) return 0;

            double elapsed = Math.Max(0, (nowUtc - updatedAtUtc).TotalSeconds);
            int gained = (int)(elapsed / RegenSeconds);

            return Math.Min(gained, MaxLives - lives);
        }

        // Kazanılan canlar işlendikten sonra sayacın devam edeceği an: dolduysa şimdi,
        // dolmadıysa yalnızca kullanılan aralıklar kadar ileri (artan süre kaybolmaz).
        public static DateTime NextUpdatedAt(int lives, DateTime updatedAtUtc, int gained, DateTime nowUtc)
        {
            return lives + gained >= MaxLives ? nowUtc : updatedAtUtc.AddSeconds(gained * RegenSeconds);
        }
    }
}
