using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class MainMenuPlayButton : MonoBehaviour
{
    [SerializeField] private LevelCatalog catalog;

    private Button playButton;

    private void Awake()
    {
        playButton = GetComponent<Button>();
        playButton.onClick.AddListener(Play);
    }

    private void OnEnable()
    {
        FirebaseBootstrap.UserReady += OnUserReady;
        RefreshInteractable();
    }

    private void OnDisable()
    {
        FirebaseBootstrap.UserReady -= OnUserReady;
    }

    private void OnDestroy()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveListener(Play);
        }
    }

    private void OnUserReady(UserData user)
    {
        RefreshInteractable();
    }

    private void RefreshInteractable()
    {
        if (playButton == null)
        {
            return;
        }

        FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;
        playButton.interactable = catalog != null
            && bootstrap != null
            && bootstrap.IsReady
            && bootstrap.User != null;
    }

    private void Play()
    {
        FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;

        if (catalog == null || bootstrap == null || !bootstrap.IsReady || bootstrap.User == null)
        {
            Debug.LogWarning("Play requested before the user progress was ready.");
            RefreshInteractable();
            return;
        }

        LevelData selectedLevel = catalog.GetPlayableLevel(bootstrap.User.highestCompletedLevel);

        if (selectedLevel == null)
        {
            Debug.LogError("No playable level exists in the level catalog.");
            return;
        }

        LevelLoader.selectedLevel = selectedLevel;
        LevelLoader.catalog = catalog;
        SceneManager.LoadScene(ButtonControl.GameBoardScene);
    }
}
