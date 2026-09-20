using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameManager : MonoBehaviour
{
    // Sahnedeki hazır hedef göstergesi: her potion tipi için bir obje
    // (ikonu ve TMP'si içinde hazır duruyor).
    [Serializable]
    public class GoalDisplay
    {
        public PotionType potionType;
        public GameObject root; // Red / Green / Blue ... objesi
    }


    public static GameManager Instance; // static reference

    public GameObject backgroundPanel; // grey background
    public GameObject victoryPanel;
    public GameObject losePanel;

    // Menüden gelinmediyse (editörde GameBoard direkt açıldıysa) oynanacak test leveli.
    [SerializeField] private LevelData levelData;

    // Şu an gerçekten oynanan level (LevelLoader'dan ya da levelData'dan çözülür).
    public LevelData ActiveLevel { get; private set; }

    private int goal; // the amount of points you need to get to to win.
    private int moves; // the number of turns you can take
    private int points; // the current points you have earned.

    // Toplama hedefleri: level başlarken LevelData'dan KOPYALANIR.
    // Kalan adetler oyun sırasında bu listede azaltılır (asset'e dokunulmaz).
    private readonly List<PotionGoal> potionGoals = new();

    public bool isGameEnded;
    private bool isPlayedlast3MovesClip = false;

    public TMP_Text pointsTXT;
    public TMP_Text movesTXT;
    public TMP_Text goalTXT;

    [Header("Hedef göstergeleri")]
    // Sahnedeki tüm hedef objeleri. Level'da hedef olanlar açılır, diğerleri kapatılır.
    [SerializeField] private List<GoalDisplay> goalDisplays = new();

    // Açık hedeflerin TMP'leri — potionGoals ile aynı sırada.
    private readonly List<TMP_Text> goalCountTexts = new();

    [SerializeField] private GameObject outOfMovesPanel;
    [SerializeField] private CharacterAnimator charAnim;
    [SerializeField] private GameObject confetti;

    [Header("Kaybetme ekranı")]
    [SerializeField] private Button tryAgainButton;
    [SerializeField] private Button loseScreenCloseButton;

    [Header("Kazanma ekranı")]
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button victoryScreenCloseButton;
    [SerializeField] private LevelCatalog levelCatalog;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip last3MoveClip;
    [SerializeField] private AudioClip winClip;
    [SerializeField] private AudioClip lostClip;

    [Header("Ses seviyeleri")]
    [SerializeField, Range(0f, 1f)] private float last3MoveVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float winVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float loseVolume = 1f;

    // PotionBoard tahtayı Start'ta kuruyor ve ActiveLevel'a ihtiyaç duyuyor.
    // Tüm Awake'ler tüm Start'lardan önce çalıştığı için level çözümü burada yapılır.
    private void Awake()
    {
        Instance = this;

        // Menü/NextLevel bir level seçtiyse onu oyna; yoksa Inspector'daki test levelini.
        ActiveLevel = LevelLoader.selectedLevel != null ? LevelLoader.selectedLevel : levelData;

        if (ActiveLevel == null)
        {
            Debug.LogError("GameManager: Level Data atanmamış! Inspector'dan bir LevelData asset'i sürükleyin.");
            return;
        }

        Initialize(ActiveLevel);

        ResolveGameOverButtons();

        if (tryAgainButton != null) tryAgainButton.onClick.AddListener(RestartCurrentLevel);
        if (loseScreenCloseButton != null) loseScreenCloseButton.onClick.AddListener(ReturnToMainMenu);
        if (nextLevelButton != null) nextLevelButton.onClick.AddListener(AdvanceToNextLevel);
        if (victoryScreenCloseButton != null) victoryScreenCloseButton.onClick.AddListener(ReturnToMainMenu);

        if (tryAgainButton == null || loseScreenCloseButton == null ||
            nextLevelButton == null || victoryScreenCloseButton == null)
        {
            Debug.LogError("GameManager: Game over ekranındaki butonlar bulunamadı.", this);
        }
    }

    // Açık sahne kaydedilmemiş olsa bile panel çocuklarından doğru butonları bulur.
    private void ResolveGameOverButtons()
    {
        if (tryAgainButton == null)
        {
            tryAgainButton = FindButton(losePanel, "TryAgain");
        }

        if (loseScreenCloseButton == null)
        {
            loseScreenCloseButton = FindButton(losePanel, "CrossBTN");
        }

        if (nextLevelButton == null)
        {
            nextLevelButton = FindButton(victoryPanel, "NextLevel");
        }

        if (victoryScreenCloseButton == null)
        {
            victoryScreenCloseButton = FindButton(victoryPanel, "CrossBTN");
        }

        if (levelCatalog == null)
        {
            levelCatalog = LevelLoader.catalog;
        }

#if UNITY_EDITOR
        // Kaydedilmemiş açık sahne, diskteki yeni catalog referansını taşımayabilir.
        if (levelCatalog == null)
        {
            levelCatalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(
                "Assets/Scripts/Levels/LevelsData/LevelCatalog.asset");
        }
#endif
    }

    private static Button FindButton(GameObject panel, string buttonName)
    {
        if (panel == null) return null;

        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
        {
            if (button.name == buttonName) return button;
        }

        return null;
    }

    private void OnDestroy()
    {
        if (tryAgainButton != null) tryAgainButton.onClick.RemoveListener(RestartCurrentLevel);
        if (loseScreenCloseButton != null) loseScreenCloseButton.onClick.RemoveListener(ReturnToMainMenu);
        if (nextLevelButton != null) nextLevelButton.onClick.RemoveListener(AdvanceToNextLevel);
        if (victoryScreenCloseButton != null) victoryScreenCloseButton.onClick.RemoveListener(ReturnToMainMenu);
    }

    // LevelLoader.selectedLevel sahne değişiminde korunduğu için aynı bölüm baştan kurulur.
    private void RestartCurrentLevel()
    {
        SceneManager.LoadScene(ButtonControl.GameBoardScene);
    }

    private void ReturnToMainMenu()
    {
        SceneManager.LoadScene(ButtonControl.MainMenuScene);
    }

    private void AdvanceToNextLevel()
    {
        if (levelCatalog == null)
        {
            Debug.LogError("GameManager: Next Level için LevelCatalog atanmamış.");
            return;
        }

        LevelData nextLevel = levelCatalog.GetNext(ActiveLevel);

        if (nextLevel == null)
        {
            ReturnToMainMenu();
            return;
        }

        LevelLoader.selectedLevel = nextLevel;
        SceneManager.LoadScene(ButtonControl.GameBoardScene);
    }

    // Bölüm değerlerini LevelData asset'inden okur.
    // potionGoals eleman eleman KOPYALANIR — referans atansaydı oyun sırasında
    // düşen sayaçlar doğrudan asset'in içine yazılır ve kalıcı olurdu.
    public void Initialize(LevelData level)
    {
        moves = level.moves;
        goal = level.goal;
        points = 0;
        isGameEnded = false;
        isPlayedlast3MovesClip = false;

        potionGoals.Clear();

        foreach (PotionGoal sourceGoal in level.potionGoals)
        {
            potionGoals.Add(new PotionGoal
            {
                potionType = sourceGoal.potionType,
                amount = sourceGoal.amount
            });
        }

        SetupGoalSlots();
    }

    // Level'da hedef olan tipleri açar, diğerlerini kapatır.
    private void SetupGoalSlots()
    {
        goalCountTexts.Clear();

        foreach (GoalDisplay display in goalDisplays)
        {
            display.root.SetActive(false);
        }

        foreach (PotionGoal potionGoal in potionGoals)
        {
            GoalDisplay display = goalDisplays.Find(d => d.potionType == potionGoal.potionType);

            if (display == null)
            {
                // Bu tip için sahnede gösterge yok — sıralama bozulmasın diye yine de ekle.
                goalCountTexts.Add(null);
                continue;
            }

            display.root.SetActive(true);
            goalCountTexts.Add(display.root.GetComponentInChildren<TMP_Text>());
        }
    }

    // Update is called once per frame
    void Update()
    {
        pointsTXT.text = points.ToString() + " /";
        movesTXT.text = moves.ToString();
        goalTXT.text = goal.ToString();

        for (int i = 0; i < goalCountTexts.Count; i++)
        {
            if (goalCountTexts[i] != null)
            {
                goalCountTexts[i].text = potionGoals[i].amount.ToString();
            }
        }
    }

    // PotionBoard.ReturnPotionToPool temizlenen her taşı buraya bildirir.
    // Tipi eşleşen tüm hedeflerin kalan adedi düşülür.
    public void RegisterClearedPotion(PotionType type)
    {
        foreach (PotionGoal potionGoal in potionGoals)
        {
            if (potionGoal.potionType == type && potionGoal.amount > 0)
            {
                potionGoal.amount--;
            }
        }
    }


    private bool AreAllPotionGoalsComplete()
    {
        foreach (PotionGoal potionGoal in potionGoals)
        {
            if (potionGoal.amount > 0)
            {
                return false;
            }
        }

        return true;
    }

    // Cascade sırasında her eşleşme/patlama anında puan ekler.
    // Kazanma/kaybetme kararı tur sonunda ProcessTurn'de verilir.
    public void AddPoints(int amount)
    {
        points += amount;
    }

    public void ProcessTurn(int _pointsToGain, bool _subtractMoves)
    {
        // Cascade bittikten sonra biriken hamleler arka arkaya düşülüyor.
        // Oyun bir önceki hamlede bittiyse kalanlar işlenmemeli — yoksa
        // kazanma panelinin üstüne kaybetme paneli de açılabilir.
        if (isGameEnded) return;

        points += _pointsToGain;

        if (_subtractMoves)
        {
            moves--;
        }

        // Kazanmak için puan hedefi ve TÜM toplama hedefleri tamamlanmalı.
        if (points >= goal && AreAllPotionGoalsComplete())
        {
            //you've won the game
            isGameEnded = true;
            if (charAnim != null) charAnim.PlayWin();
            backgroundPanel.SetActive(true);
            confetti.SetActive(true);
            StartCoroutine(WaitForConfetti());
            audioSource.PlayOneShot(winClip, winVolume);
            return;
        }
        if (moves <= 3 && moves != 0 && !isPlayedlast3MovesClip)
        {
            audioSource.PlayOneShot(last3MoveClip, last3MoveVolume);
            isPlayedlast3MovesClip = true;
        }
        else if (moves == 0)
        {
            // lose the game
            if (charAnim != null) charAnim.PlayLose();
            isGameEnded = true;
            backgroundPanel.SetActive(true);
            outOfMovesPanel.SetActive(true);
            audioSource.PlayOneShot(lostClip, loseVolume);

            return;
        }
    }

    private IEnumerator WaitForConfetti()
    {
        yield return new WaitForSeconds(2);
        victoryPanel.SetActive(true);
    }
}
