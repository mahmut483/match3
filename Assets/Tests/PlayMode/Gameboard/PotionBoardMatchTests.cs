#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// CheckBoard'un eşleşme tespiti: hangi şekil hangi tür grubu üretir.
// Refactor öncesi davranışı sabitler; taşlar yerinde durur, temizleme yapılmaz.
public class PotionBoardMatchTests : PotionBoardTestBase
{
    private List<MatchResult> ScanBoard()
    {
        Invoke("CheckBoard");
        return Field<List<MatchResult>>("currentMatchGroups");
    }

    private static IEnumerable<Vector2Int> CellsOf(MatchResult group)
    {
        return group.connectedPotions.Select(p => new Vector2Int(p.xIndex, p.yIndex));
    }

    [UnityTest]
    public IEnumerator NoMatchPattern_ProducesNoGroups()
    {
        yield return SetUpBoard();

        Assert.That(ScanBoard(), Is.Empty);
    }

    [UnityTest]
    public IEnumerator ThreeInARow_IsHorizontalMatch()
    {
        yield return SetUpBoard();
        SetType(PotionType.Green, new(0, 0), new(1, 0), new(2, 0));

        List<MatchResult> groups = ScanBoard();

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].direction, Is.EqualTo(MatchDirection.Horizontal));
        Assert.That(groups[0].IsSuperMatch, Is.False);
        Assert.That(CellsOf(groups[0]), Is.EquivalentTo(new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) }));
    }

    [UnityTest]
    public IEnumerator FourInAColumn_IsLongVerticalMatch()
    {
        yield return SetUpBoard();
        SetType(PotionType.Green, new(5, 2), new(5, 3), new(5, 4), new(5, 5));

        List<MatchResult> groups = ScanBoard();

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].direction, Is.EqualTo(MatchDirection.LongVertical));
        Assert.That(groups[0].IsSuperMatch, Is.True);
        Assert.That(groups[0].connectedPotions, Has.Count.EqualTo(4));
        Assert.That(groups[0].protectedPotion, Is.Not.Null);
        Assert.That(groups[0].connectedPotions, Has.Member(groups[0].protectedPotion));
    }

    [UnityTest]
    public IEnumerator TShape_IsSuperMatchWithAllFiveCells()
    {
        yield return SetUpBoard();
        SetType(PotionType.Green, new(0, 3), new(1, 3), new(2, 3), new(1, 4), new(1, 5));

        List<MatchResult> groups = ScanBoard();

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].direction, Is.EqualTo(MatchDirection.Super));
        Assert.That(CellsOf(groups[0]), Is.EquivalentTo(new[]
        {
            new Vector2Int(0, 3), new Vector2Int(1, 3), new Vector2Int(2, 3), new Vector2Int(1, 4), new Vector2Int(1, 5),
        }));
    }

    [UnityTest]
    public IEnumerator LShapeFromVerticalLine_IsSuperMatch()
    {
        yield return SetUpBoard();
        // Yatay üçlü + sol ucundan yukarı iki (L): IsConnected önce yatay hattı
        // bulur, SuperMatch dikey kolu ekler.
        SetType(PotionType.Green, new(0, 4), new(0, 5), new(0, 6), new(1, 4), new(2, 4));

        List<MatchResult> groups = ScanBoard();

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].direction, Is.EqualTo(MatchDirection.Super));
        Assert.That(groups[0].connectedPotions, Has.Count.EqualTo(5));
    }

    [UnityTest]
    public IEnumerator TwoSeparateLines_AreTwoGroups()
    {
        yield return SetUpBoard();
        SetType(PotionType.Green, new(0, 0), new(1, 0), new(2, 0));
        SetType(PotionType.Green, new(3, 6), new(4, 6), new(5, 6));

        List<MatchResult> groups = ScanBoard();

        Assert.That(groups, Has.Count.EqualTo(2));
        Assert.That(groups.Select(g => g.direction), Is.All.EqualTo(MatchDirection.Horizontal));
    }

    [UnityTest]
    public IEnumerator SpecialPotions_DoNotMatchEachOther()
    {
        yield return SetUpBoard();
        SetType(PotionType.Bomb, new(0, 0), new(1, 0), new(2, 0));
        SetType(PotionType.Rocket, new(3, 1), new(3, 2), new(3, 3));

        Assert.That(ScanBoard(), Is.Empty);
    }

    [UnityTest]
    public IEnumerator SpawnRows_AreNotScanned()
    {
        yield return SetUpBoard();
        SetType(PotionType.Green, new(0, 8), new(1, 8), new(2, 8));

        Assert.That(ScanBoard(), Is.Empty);
    }
}
#endif
