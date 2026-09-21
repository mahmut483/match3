using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

// Kazanma, puan/hedef tamamlandığı anda tespit edilmeli; hangi yoldan geldiği
// (takas, dokunma, özel vuruş) fark etmemeli. Özel vuruşlar hamle harcamadığı
// için ProcessTurn çağırmaz; kazanma yalnızca orada kontrol edilirse kaçar.
public class GameManagerWinTests
{
    private GameObject root;
    private LevelData level;

    [TearDown]
    public void TearDown()
    {
        GameManager.Instance = null;
        LevelLoader.selectedLevel = null;

        if (root != null) Object.DestroyImmediate(root);
        if (level != null) Object.DestroyImmediate(level);
    }

    [Test]
    public void ReachingPointGoalWithoutSpendingMove_EndsGame()
    {
        GameManager manager = CreateManager(goal: 10);

        manager.AddPoints(10);

        Assert.That(manager.isGameEnded, Is.True);
    }

    [Test]
    public void CompletingCollectGoalWithoutSpendingMove_EndsGame()
    {
        GameManager manager = CreateManager(goal: 0, new PotionGoal { potionType = PotionType.Red, amount = 1 });

        manager.RegisterClearedPotion(PotionType.Red);

        Assert.That(manager.isGameEnded, Is.True);
    }

    [Test]
    public void UnfinishedGoals_DoNotEndGame()
    {
        GameManager manager = CreateManager(goal: 10, new PotionGoal { potionType = PotionType.Red, amount = 1 });

        manager.AddPoints(10);

        Assert.That(manager.isGameEnded, Is.False);
    }

    private GameManager CreateManager(int goal, params PotionGoal[] goals)
    {
        level = ScriptableObject.CreateInstance<LevelData>();
        level.goal = goal;
        level.moves = 5;
        level.potionGoals = new List<PotionGoal>(goals);
        LevelLoader.selectedLevel = level;

        root = new GameObject("GameManagerTest");
        GameManager manager = root.AddComponent<GameManager>();

        SerializedObject serialized = new(manager);
        serialized.FindProperty("backgroundPanel").objectReferenceValue = Child("Background");
        serialized.FindProperty("victoryPanel").objectReferenceValue = Child("Victory");
        serialized.FindProperty("losePanel").objectReferenceValue = Child("Lose");
        serialized.FindProperty("outOfMovesPanel").objectReferenceValue = Child("OutOfMoves");
        serialized.FindProperty("confetti").objectReferenceValue = Child("Confetti");
        serialized.FindProperty("audioSource").objectReferenceValue = root.AddComponent<AudioSource>();
        serialized.FindProperty("winClip").objectReferenceValue = AudioClip.Create("win", 1, 1, 8000, false);
        serialized.FindProperty("lostClip").objectReferenceValue = AudioClip.Create("lost", 1, 1, 8000, false);
        serialized.FindProperty("pointsTXT").objectReferenceValue = Text("Points");
        serialized.FindProperty("movesTXT").objectReferenceValue = Text("Moves");
        serialized.FindProperty("goalTXT").objectReferenceValue = Text("Goal");
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // EditMode'da Unity yaşam döngüsü koşmaz; Awake düz C# çağrısıyla tetiklenir
        // (SendMessage edit modunda ShouldRunBehaviour assert'i üretiyor).
        // Panellerde buton yok; Awake bunu hata olarak bildiriyor.
        LogAssert.Expect(LogType.Error, "GameManager: Game over ekranındaki butonlar bulunamadı.");
        typeof(GameManager)
            .GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(manager, null);

        return manager;
    }

    private GameObject Child(string name)
    {
        GameObject child = new(name);
        child.transform.SetParent(root.transform);
        return child;
    }

    private TMP_Text Text(string name)
    {
        return Child(name).AddComponent<TextMeshPro>();
    }
}
