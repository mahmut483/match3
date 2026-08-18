using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonControl : MonoBehaviour
{
    public const string MainMenuScene = "MainMenu";
    public const string GameBoardScene = "GameBoard";

    [SerializeField] private LevelCatalog catalog;

    // Aynı bölümü baştan başlatır (seçili level değişmez).
    public void TryAgain()
    {
        SceneManager.LoadScene(GameBoardScene);
    }

    // Katalogdan sıradaki bölümü yükler; son bölüm bittiyse menüye döner.
    public void NextLevel()
    {
        LevelData next = catalog.GetNext(GameManager.Instance.ActiveLevel);

        if (next != null)
        {
            LevelLoader.selectedLevel = next;
            SceneManager.LoadScene(GameBoardScene);
        }
        else
        {
            SceneManager.LoadScene(MainMenuScene);
        }
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene(MainMenuScene);
    }
}
