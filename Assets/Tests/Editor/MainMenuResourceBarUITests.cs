using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

public class MainMenuResourceBarUITests
{
    [TestCase(10, "0/5 • 00:10")]
    [TestCase(1, "0/5 • 00:01")]
    [TestCase(0, "0/5 • 00:00")]
    public void LifeRefillTimer_FormatsCountdownForEmptyLives(
        int remainingSeconds,
        string expected)
    {
        Assert.That(
            InvokeLifeRefillTimer<string>(
                "FormatStatus",
                new[] { typeof(int), typeof(int), typeof(int) },
                0,
                5,
                remainingSeconds),
            Is.EqualTo(expected));
    }

    [Test]
    public void LifeRefillTimer_DoesNotDisplayCountdownWhileLivesRemain()
    {
        Assert.That(
            InvokeLifeRefillTimer<string>(
                "FormatStatus",
                new[] { typeof(int), typeof(int), typeof(int) },
                3,
                5,
                7),
            Is.EqualTo("3/5"));
    }

    [TestCase(0.0, 10)]
    [TestCase(3.2, 7)]
    [TestCase(9.1, 1)]
    [TestCase(10.0, 0)]
    [TestCase(25.0, 0)]
    public void LifeRefillTimer_CalculatesRemainingSecondsFromUtcElapsedTime(
        double elapsedSeconds,
        int expected)
    {
        DateTime startedAtUtc = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        DateTime nowUtc = startedAtUtc.AddSeconds(elapsedSeconds);

        Assert.That(
            InvokeLifeRefillTimer<int>(
                "GetRemainingSeconds",
                new[] { typeof(DateTime), typeof(DateTime), typeof(int) },
                startedAtUtc,
                nowUtc,
                10),
            Is.EqualTo(expected));
    }

    [Test]
    public void RuntimeInitializer_BindsNamedTexts_WhenSceneComponentIsMissing()
    {
        GameObject heartObject = new GameObject(
            "heartTMP",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        GameObject coinObject = new GameObject(
            "CoinTMP",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));

        try
        {
            TMP_Text heartText = heartObject.GetComponent<TMP_Text>();
            TMP_Text coinText = coinObject.GetComponent<TMP_Text>();
            heartText.text = "New Text";
            coinText.text = "New Text";

            MethodInfo[] runtimeInitializers = typeof(MainMenuResourceBarUI)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Where(method => method.GetCustomAttributes(
                    typeof(RuntimeInitializeOnLoadMethodAttribute), false).Length > 0)
                .ToArray();

            foreach (MethodInfo initializer in runtimeInitializers)
            {
                initializer.Invoke(null, null);
            }

            Assert.That(heartText.text, Is.EqualTo("0/5"));
            Assert.That(coinText.text, Is.EqualTo("0"));
        }
        finally
        {
            Object.DestroyImmediate(heartObject);
            Object.DestroyImmediate(coinObject);

            foreach (MainMenuResourceBarUI resourceBar in
                     Object.FindObjectsByType<MainMenuResourceBarUI>(
                         FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(resourceBar.gameObject);
            }
        }
    }

    [TestCase(0, 5, "0/5")]
    [TestCase(3, 5, "3/5")]
    [TestCase(7, 5, "5/5")]
    [TestCase(-1, 5, "0/5")]
    public void FormatLives_ClampsAndDisplaysCurrentAndMaximum(
        int lives,
        int maximum,
        string expected)
    {
        Assert.That(MainMenuResourceBarUI.FormatLives(lives, maximum), Is.EqualTo(expected));
    }

    [TestCase(0, "0")]
    [TestCase(125, "125")]
    [TestCase(-10, "0")]
    public void FormatCoins_ClampsNegativeValues(int coins, string expected)
    {
        Assert.That(MainMenuResourceBarUI.FormatCoins(coins), Is.EqualTo(expected));
    }

    private static T InvokeLifeRefillTimer<T>(
        string methodName,
        Type[] parameterTypes,
        params object[] arguments)
    {
        Type timerType = typeof(MainMenuResourceBarUI).Assembly.GetType("LifeRefillTimer");
        Assert.That(timerType, Is.Not.Null, "LifeRefillTimer henüz uygulanmadı.");

        MethodInfo method = timerType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            null,
            parameterTypes,
            null);
        Assert.That(method, Is.Not.Null, methodName + " henüz uygulanmadı.");

        return (T)method.Invoke(null, arguments);
    }
}
