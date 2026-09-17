using NUnit.Framework;
using UnityEngine;

public class LevelCatalogTests
{
    private LevelCatalog catalog;
    private LevelData levelOne;
    private LevelData levelTwo;

    [SetUp]
    public void SetUp()
    {
        catalog = ScriptableObject.CreateInstance<LevelCatalog>();
        levelOne = CreateLevel(1);
        levelTwo = CreateLevel(2);
        catalog.levels.Add(levelOne);
        catalog.levels.Add(levelTwo);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(levelOne);
        Object.DestroyImmediate(levelTwo);
        Object.DestroyImmediate(catalog);
    }

    [TestCase(0, 1)]
    [TestCase(1, 2)]
    [TestCase(2, 1)]
    [TestCase(99, 1)]
    public void GetPlayableLevel_SelectsNextAvailableLevelAndWrapsToFirst(
        int highestCompletedLevel,
        int expectedLevelNumber)
    {
        LevelData selected = catalog.GetPlayableLevel(highestCompletedLevel);

        Assert.That(selected.level, Is.EqualTo(expectedLevelNumber));
    }

    private static LevelData CreateLevel(int number)
    {
        LevelData level = ScriptableObject.CreateInstance<LevelData>();
        level.level = number;
        return level;
    }
}
