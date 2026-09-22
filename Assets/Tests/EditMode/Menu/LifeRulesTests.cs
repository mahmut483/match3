using System;
using NUnit.Framework;
using Match3.Backend;

public class LifeRulesTests
{
    private static readonly DateTime T0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public void FullLives_GainNothing()
    {
        Assert.AreEqual(0, LifeRules.GainedLives(LifeRules.MaxLives, T0, T0.AddHours(5)));
    }

    [Test]
    public void BeforeFirstInterval_GainNothing()
    {
        Assert.AreEqual(0, LifeRules.GainedLives(2, T0, T0.AddSeconds(LifeRules.RegenSeconds - 1)));
    }

    [Test]
    public void OneLifePerInterval_CappedAtMax()
    {
        Assert.AreEqual(1, LifeRules.GainedLives(2, T0, T0.AddSeconds(LifeRules.RegenSeconds)));
        Assert.AreEqual(2, LifeRules.GainedLives(2, T0, T0.AddSeconds(LifeRules.RegenSeconds * 2.5)));
        Assert.AreEqual(3, LifeRules.GainedLives(2, T0, T0.AddSeconds(LifeRules.RegenSeconds * 10)));
    }

    [Test]
    public void NextUpdatedAt_KeepsLeftoverTime_WhenStillBelowMax()
    {
        DateTime now = T0.AddSeconds(LifeRules.RegenSeconds * 1.5);
        DateTime next = LifeRules.NextUpdatedAt(1, T0, 1, now);
        Assert.AreEqual(T0.AddSeconds(LifeRules.RegenSeconds), next);
    }

    [Test]
    public void NextUpdatedAt_IsNow_WhenReachingMax()
    {
        DateTime now = T0.AddSeconds(LifeRules.RegenSeconds * 7);
        Assert.AreEqual(now, LifeRules.NextUpdatedAt(2, T0, 3, now));
    }
}
