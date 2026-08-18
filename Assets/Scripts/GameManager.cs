using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{

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
    public TMP_Text potionTypeGoalTXT;

    [SerializeField] private GameObject outOfMovesPanel;
    [SerializeField] private Animator charAnimCtrl;
    [SerializeField] private GameObject confetti;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip last3MoveClip;
    [SerializeField] private AudioClip winClip;
    [SerializeField] private AudioClip lostClip;

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
    }

    // Update is called once per frame
    void Update()
    {
        pointsTXT.text = points.ToString() + " /";
        movesTXT.text = moves.ToString();
        goalTXT.text = goal.ToString();
        potionTypeGoalTXT.text = BuildPotionGoalText();
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

    // HUD için "Red: 12  Bomb: 3" tarzı özet metin üretir.
    private string BuildPotionGoalText()
    {
        string text = "";

        foreach (PotionGoal potionGoal in potionGoals)
        {
            text += potionGoal.potionType + ": " + potionGoal.amount + "  ";
        }

        return text;
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
            charAnimCtrl.SetTrigger("Win");
            backgroundPanel.SetActive(true);
            confetti.SetActive(true);
            StartCoroutine(WaitForConfetti());
            audioSource.clip = winClip;
            audioSource.Play();
            return;
        }
        if (moves <= 3 && moves != 0 && !isPlayedlast3MovesClip)
        {
            charAnimCtrl.SetTrigger("LowMove");
            audioSource.clip = last3MoveClip;
            audioSource.Play();
            isPlayedlast3MovesClip = true;
        }
        else if (moves == 0)
        {
            // lose the game
            charAnimCtrl.SetTrigger("Lose");
            isGameEnded = true;
            backgroundPanel.SetActive(true);
            outOfMovesPanel.SetActive(true);
            audioSource.clip = lostClip;
            audioSource.Play();

            return;
        }
    }

    private IEnumerator WaitForConfetti()
    {
        yield return new WaitForSeconds(2);
        victoryPanel.SetActive(true);
    }
}
