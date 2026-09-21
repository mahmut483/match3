#if UNITY_EDITOR
using Match3.Gameplay.Board;
using Match3.Gameplay.Strikes;
using Match3.Gameplay.Potions;
using Match3.Gameplay.Session;
using Match3.Levels;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

// Gerçek PotionBoard + GameManager ile oynanış testlerinin ortak kurulumu.
// Sahne yerine gerekli referanslar elle kurulur; BoardInput eklenmez. Oyun kodunun asmdef'i olmadığı için bu
// dosyalar Assembly-CSharp'a derlenir; "Enable playmode tests for all
// assemblies" açık olmalı.
public abstract class PotionBoardTestBase
{
    protected const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    protected GameObject root;
    protected PotionBoard board;
    protected TMP_Text movesText;
    protected BoardGrid grid;

    private LevelData level;

    [TearDown]
    public void TearDown()
    {
        GameManager.Instance = null;
        LevelLoader.selectedLevel = null;
        if (root != null) Object.Destroy(root);
        if (level != null) Object.Destroy(level);
    }

    // Tahtayı kurar, Start'ın (InitializeBoard) çalışmasını bekler ve tüm
    // taşlara deterministik, eşleşmesiz bir desen yazar: (x + 2y) % 3 üzerinden
    // Red/Blue/Yellow; ardışık üç hücre yatayda da dikeyde de hep farklı.
    // Green desende hiç yok: testler şekilleri Green ile diker, komşu desen
    // hücreleri şekli uzatamaz.
    protected IEnumerator SetUpBoard()
    {
        level = ScriptableObject.CreateInstance<LevelData>();
        level.goal = 100000;
        level.moves = 5;
        level.potionGoals = new List<PotionGoal>();
        LevelLoader.selectedLevel = level;

        root = new GameObject("PotionBoardTest");
        root.SetActive(false);

        GameObject managerObject = Child(root, "GameManager");
        GameManager manager = managerObject.AddComponent<GameManager>();
        SerializedObject serializedManager = new(manager);
        serializedManager.FindProperty("backgroundPanel").objectReferenceValue = Child(managerObject, "Background");
        serializedManager.FindProperty("victoryPanel").objectReferenceValue = Child(managerObject, "Victory");
        serializedManager.FindProperty("losePanel").objectReferenceValue = Child(managerObject, "Lose");
        serializedManager.FindProperty("outOfMovesPanel").objectReferenceValue = Child(managerObject, "OutOfMoves");
        serializedManager.FindProperty("confetti").objectReferenceValue = Child(managerObject, "Confetti");
        serializedManager.FindProperty("audioSource").objectReferenceValue = managerObject.AddComponent<AudioSource>();
        serializedManager.FindProperty("winClip").objectReferenceValue = Clip("win");
        serializedManager.FindProperty("lostClip").objectReferenceValue = Clip("lost");
        serializedManager.FindProperty("last3MoveClip").objectReferenceValue = Clip("last3");
        serializedManager.FindProperty("pointsTXT").objectReferenceValue = Text(managerObject, "Points");
        movesText = Text(managerObject, "Moves");
        serializedManager.FindProperty("movesTXT").objectReferenceValue = movesText;
        serializedManager.FindProperty("goalTXT").objectReferenceValue = Text(managerObject, "Goal");
        serializedManager.ApplyModifiedPropertiesWithoutUndo();

        GameObject boardObject = Child(root, "PotionBoard");

        // Yardımcı bileşenler PotionBoard'dan ÖNCE eklenir: Awake'te GetComponent bulsun.
        BoardEffects effects = boardObject.AddComponent<BoardEffects>();
        SerializedObject serializedEffects = new(effects);
        serializedEffects.FindProperty("matchSource").objectReferenceValue = boardObject.AddComponent<AudioSource>();
        serializedEffects.FindProperty("superMatchSource").objectReferenceValue = boardObject.AddComponent<AudioSource>();
        serializedEffects.FindProperty("explodingSource").objectReferenceValue = boardObject.AddComponent<AudioSource>();
        serializedEffects.FindProperty("matchClip").objectReferenceValue = Clip("match");
        serializedEffects.FindProperty("superMatchClip").objectReferenceValue = Clip("super");
        serializedEffects.FindProperty("explodingClip").objectReferenceValue = Clip("explode");
        serializedEffects.ApplyModifiedPropertiesWithoutUndo();

        BoardRefill refill = boardObject.AddComponent<BoardRefill>();
        SerializedObject serializedRefill = new(refill);
        SerializedProperty prefabs = serializedRefill.FindProperty("potionPrefabs");
        string[] prefabNames = { "Red", "Blue", "Yellow", "Green" };
        prefabs.arraySize = prefabNames.Length;
        for (int i = 0; i < prefabNames.Length; i++)
        {
            prefabs.GetArrayElementAtIndex(i).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{prefabNames[i]}.prefab");
        }
        serializedRefill.FindProperty("potionParent").objectReferenceValue = Child(boardObject, "Potions");
        serializedRefill.ApplyModifiedPropertiesWithoutUndo();

        boardObject.AddComponent<SpecialChain>();
        boardObject.AddComponent<StrikePresentation>();

        board = boardObject.AddComponent<PotionBoard>();

        // Game over butonları yok; Awake bunu hata olarak bildiriyor.
        LogAssert.Expect(LogType.Error, "GameManager: Game over ekranındaki butonlar bulunamadı.");
        root.SetActive(true);

        // Awake'ler bu karede, Start (InitializeBoard) bir sonraki karede.
        yield return null;

        grid = Field<BoardGrid>("grid");

        for (int x = 0; x < BoardDefinition.VisibleWidth; x++)
        {
            for (int y = 0; y < BoardDefinition.TotalHeight; y++)
            {
                grid[x, y].Potion.PotionType = (PotionType)((x + 2 * y) % 3);
            }
        }
    }

    protected void SetType(PotionType type, params Vector2Int[] cells)
    {
        foreach (Vector2Int cell in cells) grid[cell.x, cell.y].Potion.PotionType = type;
    }

    protected object Invoke(string method, params object[] args)
    {
        return typeof(PotionBoard).GetMethod(method, Private).Invoke(board, args);
    }

    protected T Field<T>(string name)
    {
        return (T)typeof(PotionBoard).GetField(name, Private).GetValue(board);
    }

    private static GameObject Child(GameObject parent, string name)
    {
        GameObject child = new(name);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static TMP_Text Text(GameObject parent, string name)
    {
        return Child(parent, name).AddComponent<TextMeshPro>();
    }

    private static AudioClip Clip(string name)
    {
        return AudioClip.Create(name, 1, 1, 8000, false);
    }
}
#endif
