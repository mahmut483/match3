using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Match3.Gameplay.Potions;
using Match3.Gameplay.Session;
using Match3.Levels;

public class GameSessionTests
{
    private LevelData level;

    [TearDown]
    public void TearDown()
    {
        if (level != null) Object.DestroyImmediate(level);
    }

    private GameSession NewSession(int goal, int moves, params PotionGoal[] goals)
    {
        level = ScriptableObject.CreateInstance<LevelData>();
        level.goal = goal;
        level.moves = moves;
        level.potionGoals = new List<PotionGoal>(goals);
        return new GameSession(level);
    }

    [Test]
    public void ReachingPointGoal_WinsImmediately()
    {
        GameSession session = NewSession(goal: 10, moves: 5);

        session.AddPoints(10);

        Assert.That(session.Outcome, Is.EqualTo(SessionOutcome.Won));
    }

    [Test]
    public void CompletingCollectGoal_WinsImmediately()
    {
        GameSession session = NewSession(goal: 0, moves: 5, new PotionGoal { potionType = PotionType.Red, amount = 1 });

        session.RegisterCleared(PotionType.Red);

        Assert.That(session.Outcome, Is.EqualTo(SessionOutcome.Won));
    }

    [Test]
    public void UnfinishedCollectGoal_DoesNotWin()
    {
        GameSession session = NewSession(goal: 10, moves: 5, new PotionGoal { potionType = PotionType.Red, amount = 1 });

        session.AddPoints(10);

        Assert.That(session.Outcome, Is.EqualTo(SessionOutcome.Playing));
    }

    [Test]
    public void LastMoveWithoutGoal_Loses()
    {
        GameSession session = NewSession(goal: 100, moves: 1);

        session.EndTurn();

        Assert.That(session.Moves, Is.EqualTo(0));
        Assert.That(session.Outcome, Is.EqualTo(SessionOutcome.Lost));
    }

    [Test]
    public void WinBeforeLastTurnEnds_StaysWon()
    {
        GameSession session = NewSession(goal: 10, moves: 1);

        session.AddPoints(10);
        session.EndTurn();

        Assert.That(session.Outcome, Is.EqualTo(SessionOutcome.Won));
        Assert.That(session.Moves, Is.EqualTo(1), "Oyun bittikten sonra hamle düşmez.");
    }

    [Test]
    public void Goals_AreCopied_LevelAssetIsNotMutated()
    {
        PotionGoal source = new() { potionType = PotionType.Blue, amount = 2 };
        GameSession session = NewSession(goal: 0, moves: 5, source);

        session.RegisterCleared(PotionType.Blue);

        Assert.That(session.Goals[0].amount, Is.EqualTo(1));
        Assert.That(source.amount, Is.EqualTo(2));
    }

    [Test]
    public void RegisterCleared_IgnoresTypesWithoutGoalAndGoalsAtZero()
    {
        GameSession session = NewSession(goal: 100, moves: 5, new PotionGoal { potionType = PotionType.Red, amount = 1 });

        session.RegisterCleared(PotionType.Green);
        session.RegisterCleared(PotionType.Red);
        session.RegisterCleared(PotionType.Red);

        Assert.That(session.Goals[0].amount, Is.EqualTo(0));
    }
}
