using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Match3.Gameplay.Board;
using Match3.Gameplay.Potions;

public class MatchFinderTests
{
    private readonly List<GameObject> spawned = new();
    private BoardGrid grid;
    private MatchFinder finder;

    [SetUp]
    public void SetUp()
    {
        grid = new BoardGrid(new ArrayLayout());
        finder = new MatchFinder(grid);

        // Eşleşmesiz desen: (x + 2y) % 3 → Red/Blue/Yellow; Green desende yok.
        for (int x = 0; x < BoardDefinition.VisibleWidth; x++)
        {
            for (int y = 0; y < BoardDefinition.TotalHeight; y++)
            {
                grid.Place(NewPotion((PotionType)((x + 2 * y) % 3)), new Vector2Int(x, y));
            }
        }
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned) Object.DestroyImmediate(go);
        spawned.Clear();
    }

    private Potion NewPotion(PotionType type)
    {
        GameObject go = new("potion");
        spawned.Add(go);
        Potion potion = go.AddComponent<Potion>();
        potion.PotionType = type;
        return potion;
    }

    private void SetType(PotionType type, params Vector2Int[] cells)
    {
        foreach (Vector2Int cell in cells) grid[cell.x, cell.y].Potion.PotionType = type;
    }

    private static IEnumerable<Vector2Int> CellsOf(MatchResult group)
    {
        return group.ConnectedPotions.Select(p => new Vector2Int(p.XIndex, p.YIndex));
    }

    [Test]
    public void NoMatchPattern_ProducesNoGroups()
    {
        Assert.That(finder.FindAll(), Is.Empty);
    }

    [Test]
    public void ThreeInARow_IsHorizontalMatch()
    {
        SetType(PotionType.Green, new(0, 0), new(1, 0), new(2, 0));

        List<MatchResult> groups = finder.FindAll();

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].Direction, Is.EqualTo(MatchDirection.Horizontal));
        Assert.That(groups[0].IsSuperMatch, Is.False);
        Assert.That(CellsOf(groups[0]), Is.EquivalentTo(new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) }));
    }

    [Test]
    public void FourInAColumn_IsLongVerticalWithProtectedMember()
    {
        SetType(PotionType.Green, new(5, 2), new(5, 3), new(5, 4), new(5, 5));

        List<MatchResult> groups = finder.FindAll();

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].Direction, Is.EqualTo(MatchDirection.LongVertical));
        Assert.That(groups[0].ConnectedPotions, Has.Count.EqualTo(4));
        Assert.That(groups[0].ProtectedPotion, Is.Not.Null);
        Assert.That(groups[0].ConnectedPotions, Has.Member(groups[0].ProtectedPotion));
    }

    [Test]
    public void TShape_IsSuperMatchWithAllFiveCells()
    {
        SetType(PotionType.Green, new(0, 3), new(1, 3), new(2, 3), new(1, 4), new(1, 5));

        List<MatchResult> groups = finder.FindAll();

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].Direction, Is.EqualTo(MatchDirection.Super));
        Assert.That(groups[0].ConnectedPotions, Has.Count.EqualTo(5));
    }

    [Test]
    public void LShape_IsSuperMatch()
    {
        SetType(PotionType.Green, new(0, 4), new(0, 5), new(0, 6), new(1, 4), new(2, 4));

        List<MatchResult> groups = finder.FindAll();

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].Direction, Is.EqualTo(MatchDirection.Super));
        Assert.That(groups[0].ConnectedPotions, Has.Count.EqualTo(5));
    }

    [Test]
    public void TwoSeparateLines_AreTwoGroups()
    {
        SetType(PotionType.Green, new(0, 0), new(1, 0), new(2, 0), new(3, 6), new(4, 6), new(5, 6));

        Assert.That(finder.FindAll(), Has.Count.EqualTo(2));
    }

    [Test]
    public void SpecialPotions_DoNotMatchEachOther()
    {
        SetType(PotionType.Bomb, new(0, 0), new(1, 0), new(2, 0));
        SetType(PotionType.Rocket, new(3, 1), new(3, 2), new(3, 3));

        Assert.That(finder.FindAll(), Is.Empty);
    }

    [Test]
    public void SpawnRows_AreNotScanned()
    {
        SetType(PotionType.Green, new(0, 8), new(1, 8), new(2, 8));

        Assert.That(finder.FindAll(), Is.Empty);
    }

    [Test]
    public void FindAround_OnlyReportsGroupsOfTheTwoPotions_AndProtectsTheSwappedOne()
    {
        // (3,0) grubun parçası ve takas taşı; (0,5..7) ilgisiz bir dikey üçlü.
        SetType(PotionType.Green, new(0, 0), new(1, 0), new(2, 0), new(3, 0), new(0, 5), new(0, 6), new(0, 7));
        Potion swapped = grid[3, 0].Potion;
        Potion other = grid[3, 1].Potion;

        List<MatchResult> groups = finder.FindAround(swapped, other);

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].Direction, Is.EqualTo(MatchDirection.LongHorizontal));
        Assert.That(groups[0].ProtectedPotion, Is.SameAs(swapped));
    }
}
