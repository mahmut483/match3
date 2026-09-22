using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Match3.Backend;
using Match3.Gameplay.Potions;
using Match3.Levels;
using Match3.Shared;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Match3.Gameplay.Session
{
    // Sahne adaptörü: level çözümü, GameSession'ı kurar, HUD/panel/ses/karakteri
    // session'a göre günceller, sahne geçişleri. Kural burada değil, GameSession'da.
    public class GameManager : MonoBehaviour
    {
        // Sahnedeki hazır hedef göstergesi: her potion tipi için bir obje
        // (ikonu ve TMP'si içinde hazır duruyor).
        [Serializable]
        private class GoalDisplay
        {
            public PotionType potionType;
            public GameObject root; // Red / Green / Blue ... objesi
        }

        public static GameManager Instance;

        [SerializeField] private GameObject backgroundPanel;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject losePanel;

        // Menüden gelinmediyse (editörde GameBoard direkt açıldıysa) oynanacak test leveli.
        [SerializeField] private LevelData levelData;

        // Şu an gerçekten oynanan level (LevelLoader'dan ya da levelData'dan çözülür).
        public LevelData ActiveLevel { get; private set; }

        // Bölüm kuralları (puan, hamle, hedefler, sonuç). Sahne yalnızca bunu yansıtır.
        private GameSession session;

        public bool IsGameEnded => session != null && session.IsEnded;

        private bool isPlayedlast3MovesClip;
        private bool outcomePresented;

        [SerializeField] private TMP_Text pointsTXT;
        [SerializeField] private TMP_Text movesTXT;
        [SerializeField] private TMP_Text goalTXT;

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
        [SerializeField] private int winGoldReward = 50;

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

        // Session'ı kurar ve HUD'u ilk değerlerle yazar.
        private void Initialize(LevelData level)
        {
            session = new GameSession(level);
            isPlayedlast3MovesClip = false;
            outcomePresented = false;

            SetupGoalSlots();
            RefreshHud();
        }

        // Level'da hedef olan tipleri açar, diğerlerini kapatır.
        private void SetupGoalSlots()
        {
            goalCountTexts.Clear();

            foreach (GoalDisplay display in goalDisplays)
            {
                display.root.SetActive(false);
            }

            foreach (PotionGoal potionGoal in session.Goals)
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

        // HUD yalnızca değer değişince yazılır: Initialize, AddPoints, ProcessTurn
        // ve RegisterClearedPotion çağırır.
        private void RefreshHud()
        {
            pointsTXT.text = session.Points.ToString() + " /";
            movesTXT.text = session.Moves.ToString();
            goalTXT.text = session.Goal.ToString();

            for (int i = 0; i < goalCountTexts.Count; i++)
            {
                if (goalCountTexts[i] != null)
                {
                    goalCountTexts[i].text = session.Goals[i].amount.ToString();
                }
            }
        }

        // PotionBoard temizlenen her taşı bildirir; tipi eşleşen hedefler düşer.
        public void RegisterClearedPotion(PotionType type)
        {
            session.RegisterCleared(type);
            RefreshHud();
            PresentOutcome();
        }

        // Her eşleşme/patlama anında puan; kazanma hedef dolduğu anda ilan edilir.
        public void AddPoints(int amount)
        {
            session.AddPoints(amount);
            RefreshHud();
            PresentOutcome();
        }

        // Hamle harcandı (takas/dokunma bitti). Oyun bittiyse hamle düşmez; kaybetme
        // burada, kazanma AddPoints/RegisterClearedPotion içinde ortaya çıkar.
        public void ProcessTurn()
        {
            if (session.IsEnded) return;

            session.EndTurn();
            RefreshHud();

            if (session.Moves <= 3 && session.Moves != 0 && !isPlayedlast3MovesClip)
            {
                audioSource.PlayOneShot(last3MoveClip, last3MoveVolume);
                isPlayedlast3MovesClip = true;
            }

            PresentOutcome();
        }

        // Sonuç sunumu bir kez: kazanmada karakter + konfeti + panel + ses, kaybetmede
        // karakter + panel + ses. IsGameEnded aynı karede tahta ve vuruş girişini kilitler.
        private void PresentOutcome()
        {
            if (outcomePresented || !session.IsEnded) return;

            outcomePresented = true;
            backgroundPanel.SetActive(true);

            if (session.Outcome == SessionOutcome.Won)
            {
                if (charAnim != null) charAnim.PlayWin();
                confetti.SetActive(true);
                StartCoroutine(WaitForConfetti());
                audioSource.PlayOneShot(winClip, winVolume);

                // İlerleme kazanıldığı anda yazılır; panel çarpıyla kapansa da kaybolmaz.
                if (FirebaseBootstrap.Instance != null)
                {
                    FirebaseBootstrap.Instance.CompleteLevel(ActiveLevel.level, session.Points, winGoldReward);
                }
            }
            else
            {
                if (charAnim != null) charAnim.PlayLose();
                outOfMovesPanel.SetActive(true);
                audioSource.PlayOneShot(lostClip, loseVolume);

                // Kaybedilen her bölüm bir can götürür; can kalmadıysa Try Again kapanır
                // (editörde doğrudan GameBoard açılırsa bootstrap yoktur, her şey açık kalır).
                if (FirebaseBootstrap.Instance != null && FirebaseBootstrap.Instance.User != null)
                {
                    FirebaseBootstrap.Instance.SpendLife();
                    if (tryAgainButton != null) tryAgainButton.interactable = FirebaseBootstrap.Instance.User.lives > 0;
                }
            }
        }

        private IEnumerator WaitForConfetti()
        {
            yield return new WaitForSeconds(2);
            victoryPanel.SetActive(true);
        }
    }
}
