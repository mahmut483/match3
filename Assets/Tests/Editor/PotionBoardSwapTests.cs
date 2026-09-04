using System.Collections;
using System.Reflection;
using CartoonFX;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PotionBoardSwapTests
{
    private GameObject boardObject;
    private GameObject firstObject;
    private GameObject secondObject;
    private GameObject thirdObject;

    [Test]
    public void DestroyedRocketTrail_DoesNotAbortTrailCompletionCheck()
    {
        GameObject trailObject = new GameObject("Destroyed Trail");
        ParticleSystem destroyedTrail = trailObject.AddComponent<ParticleSystem>();
        ParticleSystem[] trails = { destroyedTrail };

        Object.DestroyImmediate(trailObject);

        MethodInfo anyAlive = typeof(PotionBoard).GetMethod(
            "AnyAlive",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(anyAlive, Is.Not.Null);

        object result = null;
        Assert.DoesNotThrow(() => result = anyAlive.Invoke(null, new object[] { trails }));
        Assert.That(result, Is.EqualTo(false));
    }

    [Test]
    public void PotionInitialization_PreventsChildEffectsFromDestroyingPooledObjects()
    {
        GameObject potionObject = new GameObject("Pooled Potion");
        GameObject effectObject = new GameObject("Rocket Trail");
        effectObject.transform.SetParent(potionObject.transform);
        effectObject.AddComponent<ParticleSystem>();

        CFXR_Effect effect = effectObject.AddComponent<CFXR_Effect>();
        effect.clearBehavior = CFXR_Effect.ClearBehavior.Destroy;

        Potion potion = potionObject.AddComponent<Potion>();
        MethodInfo awake = typeof(Potion).GetMethod(
            "Awake",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(awake, Is.Not.Null);
        awake.Invoke(potion, null);

        Assert.That(effect.clearBehavior, Is.EqualTo(CFXR_Effect.ClearBehavior.None));

        Object.DestroyImmediate(potionObject);
    }

    [Test]
    public void SecondSwapDuringResolution_DoesNotMutateBoardConcurrently()
    {
        boardObject = new GameObject("Board");
        PotionBoard board = boardObject.AddComponent<PotionBoard>();
        board.enabled = false;

        firstObject = CreatePotion("First", 0);
        secondObject = CreatePotion("Second", 1);
        thirdObject = CreatePotion("Third", 2);

        Potion first = firstObject.GetComponent<Potion>();
        Potion second = secondObject.GetComponent<Potion>();
        Potion third = thirdObject.GetComponent<Potion>();

        Node[,] nodes = new Node[3, 1];
        nodes[0, 0] = new Node(true, first);
        nodes[1, 0] = new Node(true, second);
        nodes[2, 0] = new Node(true, third);

        SetPrivateField(board, "width", 3);
        SetPrivateField(board, "height", 1);
        SetPrivateField(board, "spacingX", 1f);
        SetPrivateField(board, "spacingY", 0f);
        SetPrivateField(board, "cellSize", 1f);
        SetPrivateField(board, "potionBoard", nodes);

        MethodInfo swapPotion = typeof(PotionBoard).GetMethod(
            "SwapPotion",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(swapPotion, Is.Not.Null);

        swapPotion.Invoke(board, new object[] { first, second });
        swapPotion.Invoke(board, new object[] { first, third });

        Assert.That(nodes[0, 0].potion, Is.SameAs(second));
        Assert.That(nodes[1, 0].potion, Is.SameAs(first));
        Assert.That(nodes[2, 0].potion, Is.SameAs(third));
    }

    [Test]
    public void BufferedSwap_StartsAfterPreviousResolutionFinishes()
    {
        boardObject = new GameObject("Board");
        PotionBoard board = boardObject.AddComponent<PotionBoard>();

        firstObject = CreatePotion("First", 0, PotionType.Red, 0f);
        secondObject = CreatePotion("Second", 1, PotionType.Blue, 0f);
        thirdObject = CreatePotion("Third", 2, PotionType.Green, 0f);

        Potion first = firstObject.GetComponent<Potion>();
        Potion second = secondObject.GetComponent<Potion>();
        Potion third = thirdObject.GetComponent<Potion>();

        Node[,] nodes = new Node[3, 8];

        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                nodes[x, y] = new Node(false, null);
            }
        }

        nodes[0, 0] = new Node(true, first);
        nodes[1, 0] = new Node(true, second);
        nodes[2, 0] = new Node(true, third);

        SetPrivateField(board, "width", 3);
        SetPrivateField(board, "height", 8);
        SetPrivateField(board, "spacingX", 1f);
        SetPrivateField(board, "spacingY", 0f);
        SetPrivateField(board, "cellSize", 1f);
        SetPrivateField(board, "potionBoard", nodes);

        MethodInfo swapPotion = typeof(PotionBoard).GetMethod(
            "SwapPotion",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(swapPotion, Is.Not.Null);

        swapPotion.Invoke(board, new object[] { first, second });
        swapPotion.Invoke(board, new object[] { first, third });

        board.StopAllCoroutines();

        MethodInfo doSwap = typeof(PotionBoard).GetMethod(
            "DoSwap",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo completeSwapResolution = typeof(PotionBoard).GetMethod(
            "CompleteSwapResolution",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(doSwap, Is.Not.Null);
        Assert.That(completeSwapResolution, Is.Not.Null);

        // İlk geçersiz swap geri döndü; çözümleyici artık sıradaki komutu alabilir.
        doSwap.Invoke(board, new object[] { first, second });
        completeSwapResolution.Invoke(board, null);

        Assert.That(nodes[0, 0].potion, Is.SameAs(first));
        Assert.That(nodes[1, 0].potion, Is.SameAs(third));
        Assert.That(nodes[2, 0].potion, Is.SameAs(second));
    }

    [UnityTest]
    public IEnumerator SwapStartedBetweenCells_SettlesEachPotionAtItsOwnedCell()
    {
        boardObject = new GameObject("Board");
        PotionBoard board = boardObject.AddComponent<PotionBoard>();
        board.enabled = false;

        firstObject = new GameObject("First");
        Potion first = firstObject.AddComponent<Potion>();
        first.SetIndicies(0, 0);
        first.transform.position = new Vector3(-0.1f, 0f, 0f);
        SetPrivateField(first, "swapSpeed", 0f);

        secondObject = new GameObject("Second");
        Potion second = secondObject.AddComponent<Potion>();
        second.SetIndicies(1, 0);
        second.transform.position = new Vector3(0.5f, 0f, 0f);
        SetPrivateField(second, "swapSpeed", 0f);

        Node[,] nodes = new Node[2, 1];
        nodes[0, 0] = new Node(true, first);
        nodes[1, 0] = new Node(true, second);

        SetPrivateField(board, "width", 2);
        SetPrivateField(board, "height", 1);
        SetPrivateField(board, "spacingX", 0.5f);
        SetPrivateField(board, "spacingY", 0f);
        SetPrivateField(board, "cellSize", 1f);
        SetPrivateField(board, "potionBoard", nodes);

        MethodInfo doSwap = typeof(PotionBoard).GetMethod(
            "DoSwap",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(doSwap, Is.Not.Null);
        doSwap.Invoke(board, new object[] { first, second });

        yield return null;

        Assert.That(first.xIndex, Is.EqualTo(1));
        Assert.That(first.transform.position.x, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(second.xIndex, Is.EqualTo(0));
        Assert.That(second.transform.position.x, Is.EqualTo(-0.5f).Within(0.001f));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(boardObject);
        Object.DestroyImmediate(firstObject);
        Object.DestroyImmediate(secondObject);
        Object.DestroyImmediate(thirdObject);
    }

    private static GameObject CreatePotion(
        string name,
        int xIndex,
        PotionType potionType = PotionType.Red,
        float swapSpeed = 1f)
    {
        GameObject potionObject = new GameObject(name);
        Potion potion = potionObject.AddComponent<Potion>();
        potion.potionType = potionType;
        potion.SetIndicies(xIndex, 0);
        potion.transform.position = new Vector3(xIndex - 1f, 0f, 0f);
        SetPrivateField(potion, "swapSpeed", swapSpeed);

        return potionObject;
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
        field.SetValue(target, value);
    }
}
