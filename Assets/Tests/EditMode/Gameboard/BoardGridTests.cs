using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Match3.Gameplay.Board;
using Match3.Gameplay.Potions;

public class BoardGridTests
{
    private readonly List<GameObject> spawned = new();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned) Object.DestroyImmediate(go);
        spawned.Clear();
    }

    private Potion NewPotion(PotionType type = PotionType.Red)
    {
        GameObject go = new("potion");
        spawned.Add(go);
        Potion potion = go.AddComponent<Potion>();   // EditMode: Awake koşmaz
        potion.PotionType = type;
        return potion;
    }

    private static ArrayLayout OpenLayout() => new();   // varsayılan 15x6, hepsi açık

    private static ArrayLayout LayoutWithBlocked(int x, int y)
    {
        ArrayLayout layout = new();
        layout.rows[y].row[x] = true;
        return layout;
    }

    [Test]
    public void Constructor_BuildsBlockedAndOpenNodesFromLayout()
    {
        BoardGrid grid = new(LayoutWithBlocked(2, 3));

        Assert.That(grid[2, 3].IsUsable, Is.False);
        Assert.That(grid[0, 0].IsUsable, Is.True);
        Assert.That(grid.IsUsable(new Vector2Int(2, 3)), Is.False);
    }

    [Test]
    public void Place_SetsNodeAndPotionIndices()
    {
        BoardGrid grid = new(OpenLayout());
        Potion potion = NewPotion();

        grid.Place(potion, new Vector2Int(4, 6));

        Assert.That(grid[4, 6].Potion, Is.SameAs(potion));
        Assert.That(potion.XIndex, Is.EqualTo(4));
        Assert.That(potion.YIndex, Is.EqualTo(6));
        Assert.That(grid.PotionAt(new Vector2Int(4, 6)), Is.SameAs(potion));
    }

    [Test]
    public void PotionAt_ReturnsNullOutsideStorageAndOnBlockedOrEmptyCells()
    {
        BoardGrid grid = new(LayoutWithBlocked(1, 1));
        grid.Place(NewPotion(), new Vector2Int(0, 0));

        Assert.That(grid.PotionAt(new Vector2Int(-1, 0)), Is.Null);
        Assert.That(grid.PotionAt(new Vector2Int(0, 15)), Is.Null);
        Assert.That(grid.PotionAt(new Vector2Int(1, 1)), Is.Null);
        Assert.That(grid.PotionAt(new Vector2Int(5, 7)), Is.Null);
    }

    [Test]
    public void Swap_ExchangesCellsAndIndices()
    {
        BoardGrid grid = new(OpenLayout());
        Potion a = NewPotion(PotionType.Red);
        Potion b = NewPotion(PotionType.Blue);
        grid.Place(a, new Vector2Int(0, 0));
        grid.Place(b, new Vector2Int(1, 0));

        grid.Swap(a, b);

        Assert.That(grid[0, 0].Potion, Is.SameAs(b));
        Assert.That(grid[1, 0].Potion, Is.SameAs(a));
        Assert.That(a.XIndex, Is.EqualTo(1));
        Assert.That(b.XIndex, Is.EqualTo(0));
    }

    [Test]
    public void Clear_EmptiesCellWithoutTouchingPotion()
    {
        BoardGrid grid = new(OpenLayout());
        Potion potion = NewPotion();
        grid.Place(potion, new Vector2Int(3, 3));

        grid.Clear(new Vector2Int(3, 3));

        Assert.That(grid[3, 3].Potion, Is.Null);
        Assert.That(potion.XIndex, Is.EqualTo(3));
    }

    [Test]
    public void Potions_EnumeratesOnlyOccupiedCells()
    {
        BoardGrid grid = new(OpenLayout());
        grid.Place(NewPotion(), new Vector2Int(0, 0));
        grid.Place(NewPotion(), new Vector2Int(5, 14));

        Assert.That(grid.Potions.Count(), Is.EqualTo(2));
    }
}
