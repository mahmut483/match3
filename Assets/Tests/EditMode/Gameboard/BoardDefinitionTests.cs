using NUnit.Framework;
using UnityEngine;
using Match3.Gameplay.Board;

public class BoardDefinitionTests
{
    [TestCase(0, 0, true)]
    [TestCase(5, 7, true)]
    [TestCase(-1, 0, false)]
    [TestCase(6, 7, false)]
    [TestCase(5, 8, false)]
    public void IsPlayable_OnlyAcceptsCellsInsideSixByEightVisibleArea(int x, int y, bool expected)
    {
        Assert.That(BoardDefinition.IsPlayable(new Vector2Int(x, y)), Is.EqualTo(expected));
    }

    [Test]
    public void GetLayoutValidationError_AcceptsSixByFifteenLayout()
    {
        Assert.That(BoardDefinition.GetLayoutValidationError(CreateLayout(15, 6)), Is.Null);
    }

    [Test]
    public void GetLayoutValidationError_RejectsWrongRowCount()
    {
        Assert.That(
            BoardDefinition.GetLayoutValidationError(CreateLayout(14, 6)),
            Is.EqualTo("Layout must contain exactly 15 rows; found 14."));
    }

    [Test]
    public void GetLayoutValidationError_RejectsWrongColumnCount()
    {
        ArrayLayout layout = CreateLayout(15, 6);
        layout.rows[3].row = new bool[5];

        Assert.That(
            BoardDefinition.GetLayoutValidationError(layout),
            Is.EqualTo("Layout row 3 must contain exactly 6 columns; found 5."));
    }

    [Test]
    public void GetLayoutValidationError_RejectsBlockedSpawnCell()
    {
        ArrayLayout layout = CreateLayout(15, 6);
        layout.rows[8].row[2] = true;

        Assert.That(
            BoardDefinition.GetLayoutValidationError(layout),
            Is.EqualTo("Spawn cell (2, 8) must remain usable."));
    }

    private static ArrayLayout CreateLayout(int rowCount, int columnCount)
    {
        ArrayLayout layout = new() { rows = new ArrayLayout.rowData[rowCount] };

        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            layout.rows[rowIndex].row = new bool[columnCount];
        }

        return layout;
    }
}
