// Sahneler arası "hangi level oynanacak" bilgisini taşır.
// Menü veya NextLevel butonu bunu set eder, GameManager.Start okur.
// Statik olduğu için sahne değişiminde kaybolmaz.
public static class LevelLoader
{
    public static LevelData selectedLevel;
}
