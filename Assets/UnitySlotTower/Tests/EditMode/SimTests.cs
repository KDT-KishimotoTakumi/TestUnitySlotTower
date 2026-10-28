using NUnit.Framework;
using UnitySlotTower.Core.Sim;

namespace UnitySlotTower.Tests
{
    public class SimTests
    {
        [Test]
        public void GameRandom_SameSeedSameSequence()
        {
            var a = new GameRandom(123);
            var b = new GameRandom(123);
            for (var i = 0; i < 100; i++) Assert.AreEqual(a.NextUInt64(), b.NextUInt64());
            Assert.AreNotEqual(new GameRandom(1).NextUInt64(), new GameRandom(2).NextUInt64());
        }

        [Test]
        public void GameRandom_RangesAndChance()
        {
            var r = new GameRandom(7);
            var hits = 0;
            const int trials = 100000;
            for (var i = 0; i < trials; i++)
            {
                Assert.That(r.NextDouble(), Is.GreaterThanOrEqualTo(0).And.LessThan(1));
                Assert.That(r.NextInt(21), Is.InRange(0, 20));
                if (r.Chance(0.25)) hits++;
            }
            Assert.AreEqual(0.25, hits / (double)trials, 0.01);
        }

        [Test]
        public void SimClock_AdvancesInFixedSteps()
        {
            var clock = new SimClock();
            var count = 0;
            for (var i = 0; i < 30; i++) clock.Advance(SimClock.StepSeconds, () => count++);
            Assert.AreEqual(30, count);
            Assert.AreEqual(30, clock.Step);
        }

        [Test]
        public void SimClock_PausedDoesNotAdvanceAndKeepsNoBacklog()
        {
            var clock = new SimClock { Paused = true };
            var count = 0;
            clock.Advance(5f, () => count++);
            Assert.AreEqual(0, count);
            clock.Paused = false;
            clock.Advance(SimClock.StepSeconds, () => count++);
            Assert.AreEqual(1, count);
        }

        [Test]
        public void SimClock_LimitsStepsPerAdvance()
        {
            var clock = new SimClock();
            var count = 0;
            clock.Advance(10f, () => count++);
            Assert.AreEqual(SimClock.MaxStepsPerAdvance, count);
        }
    }
}
