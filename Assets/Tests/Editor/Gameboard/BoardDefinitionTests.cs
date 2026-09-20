using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class BoardDefinitionTests
{
    [TestCase(0, 0, true)]
    [TestCase(5, 7, true)]
    [TestCase(-1, 0, false)]
    [TestCase(6, 7, false)]
    [TestCase(5, 8, false)]
    public void IsPlayable_OnlyAcceptsCellsInsideSixByEightVisibleArea(
        int x,
        int y,
        bool expected)
    {
        MethodInfo method = RequireBoardDefinitionMethod(
            "IsPlayable",
            typeof(Vector2Int));

        bool result = (bool)method.Invoke(null, new object[] { new Vector2Int(x, y) });

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void GetLayoutValidationError_AcceptsSixByFifteenLayout()
    {
        ArrayLayout layout = CreateLayout(15, 6);

        Assert.That(GetLayoutValidationError(layout), Is.Null);
    }

    [Test]
    public void GetLayoutValidationError_RejectsWrongRowCount()
    {
        ArrayLayout layout = CreateLayout(14, 6);

        Assert.That(
            GetLayoutValidationError(layout),
            Is.EqualTo("Layout must contain exactly 15 rows; found 14."));
    }

    [Test]
    public void GetLayoutValidationError_RejectsWrongColumnCount()
    {
        ArrayLayout layout = CreateLayout(15, 6);
        layout.rows[3].row = new bool[5];

        Assert.That(
            GetLayoutValidationError(layout),
            Is.EqualTo("Layout row 3 must contain exactly 6 columns; found 5."));
    }

    [Test]
    public void GetLayoutValidationError_RejectsBlockedSpawnCell()
    {
        ArrayLayout layout = CreateLayout(15, 6);
        layout.rows[8].row[2] = true;

        Assert.That(
            GetLayoutValidationError(layout),
            Is.EqualTo("Spawn cell (2, 8) must remain usable."));
    }

    private static string GetLayoutValidationError(ArrayLayout layout)
    {
        MethodInfo method = RequireBoardDefinitionMethod(
            "GetLayoutValidationError",
            typeof(ArrayLayout));

        return (string)method.Invoke(null, new object[] { layout });
    }

    private static MethodInfo RequireBoardDefinitionMethod(
        string methodName,
        params Type[] parameterTypes)
    {
        Type definitionType = typeof(PotionBoard).Assembly.GetType("BoardDefinition");

        Assert.That(
            definitionType,
            Is.Not.Null,
            "BoardDefinition has not been implemented yet.");

        MethodInfo method = definitionType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            null,
            parameterTypes,
            null);

        Assert.That(
            method,
            Is.Not.Null,
            $"BoardDefinition.{methodName} has not been implemented yet.");

        return method;
    }

    private static ArrayLayout CreateLayout(int rowCount, int columnCount)
    {
        ArrayLayout layout = new()
        {
            rows = new ArrayLayout.rowData[rowCount]
        };

        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            layout.rows[rowIndex].row = new bool[columnCount];
        }

        return layout;
    }
}
