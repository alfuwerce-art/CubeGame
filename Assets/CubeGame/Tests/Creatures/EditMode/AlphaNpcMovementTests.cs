// D2 REVIEW-FIX — EditMode checks of the recovered Alpha NPC schedule (SpawnLocation) and waypoint patrol (WalkPath).
using System;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaNpcMovementTests
    {
        const int Hour = 3600000;

        static AlphaScheduleEntry[] Day() => new[]
        {
            new AlphaScheduleEntry(new WorldPosition(10, 5, 10), 0),            // home
            new AlphaScheduleEntry(new WorldPosition(50, 5, 10), 8 * Hour),     // work
            new AlphaScheduleEntry(new WorldPosition(30, 5, 40), 18 * Hour),    // evening
            new AlphaScheduleEntry(new WorldPosition(10, 5, 10), 22 * Hour),    // home again
        };

        [Test]
        public void ScheduleTakesTheLastEntryWhoseTimeHasPassed()
        {
            var day = Day();
            Assert.AreEqual(0, AlphaSpawnLocationSchedule.ActiveIndex(day, 0));
            Assert.AreEqual(0, AlphaSpawnLocationSchedule.ActiveIndex(day, 8 * Hour - 1));
            Assert.AreEqual(1, AlphaSpawnLocationSchedule.ActiveIndex(day, 8 * Hour), "time <= world time, inclusive");
            Assert.AreEqual(2, AlphaSpawnLocationSchedule.ActiveIndex(day, 20 * Hour));
            Assert.AreEqual(3, AlphaSpawnLocationSchedule.ActiveIndex(day, 23 * Hour));
            // Alpha scans the whole list and keeps the LAST match, also when the list is not sorted.
            var unsorted = new[] { day[2], day[0], day[1] };
            Assert.AreEqual(2, AlphaSpawnLocationSchedule.ActiveIndex(unsorted, 20 * Hour));
            // Nothing qualifies -> index 0 (the loop's initial value).
            Assert.AreEqual(0, AlphaSpawnLocationSchedule.ActiveIndex(new[] { day[1], day[2] }, 1 * Hour));
        }

        [Test]
        public void NpcTravelsOnAScheduleChangeAndTheReachedEntryBecomesHome()
        {
            var schedule = new AlphaSpawnLocationSchedule(Day());
            Assert.IsFalse(schedule.Update(7 * Hour), "still the first entry");
            Assert.IsFalse(schedule.Travelling);
            Assert.IsTrue(schedule.Update(9 * Hour), "08:00 passed -> new destination");
            Assert.IsTrue(schedule.Travelling);
            Assert.AreEqual(1, schedule.TargetIndex);
            Assert.AreEqual(0, schedule.ArrivedIndex);
            Assert.IsFalse(schedule.Update(10 * Hour), "same entry -> no new order");
            WorldPosition home = schedule.MarkArrived();
            Assert.AreEqual(new WorldPosition(50, 5, 10), home, "the reached entry is the new home / leash point");
            Assert.IsFalse(schedule.Travelling);
            Assert.IsTrue(schedule.Update(1 * Hour), "after midnight the list starts again from the first entry");
            Assert.AreEqual(0, schedule.TargetIndex);
            Assert.IsFalse(new AlphaSpawnLocationSchedule(new AlphaScheduleEntry[0]).Update(5 * Hour));
        }

        [Test]
        public void WalkPathVisitsPointsInOrderWithTheAlphaPause()
        {
            var points = new[] { new WorldPosition(0, 0, 0), new WorldPosition(20, 0, 0), new WorldPosition(20, 0, 20) };
            var path = new AlphaWalkPath(points);
            int rolls = 0;
            Func<int> rand = () => { rolls++; return 5999; };   // 5999 % 4000 + 2000 = 3999

            Assert.IsTrue(path.Tick(10, 0, 0, 16, rand, out WorldPosition target), "far from point 0 -> walk to it");
            Assert.AreEqual(points[0], target);
            Assert.AreEqual(0, path.Index);

            Assert.IsFalse(path.Tick(1.0, 0, 0, 16, rand, out _), "inside radius 2 -> stop and start the pause");
            Assert.AreEqual(1, rolls);
            Assert.AreEqual(3999, path.PauseMs);
            Assert.AreEqual(1, path.Index, "the index advances when the pause starts");

            Assert.IsFalse(path.Tick(1.0, 0, 0, 3998, rand, out _), "still pausing");
            Assert.AreEqual(1, path.PauseMs);
            Assert.IsTrue(path.Tick(1.0, 0, 0, 1, rand, out target), "pause over -> next point");
            Assert.AreEqual(points[1], target);

            Assert.IsFalse(path.Tick(20, 0, 0.5, 16, rand, out _));
            Assert.AreEqual(2, path.Index);
            Assert.IsFalse(path.Tick(20, 0, 20, 4000, rand, out _), "arrived at the last point");
            Assert.AreEqual(0, path.Index, "the route wraps: (index + 1) % count");
        }

        [Test]
        public void PauseIsBetweenTwoAndSixSeconds()
        {
            Assert.AreEqual(4000, AlphaWalkPath.PauseSpanMs);
            Assert.AreEqual(2000, AlphaWalkPath.PauseBaseMs);
            Assert.AreEqual(21, AlphaWalkPath.GroundSearchSteps);
            foreach (int roll in new[] { 0, 3999, 4000, 32767 })
            {
                var path = new AlphaWalkPath(new[] { new WorldPosition(0, 0, 0) });
                path.Tick(0, 0, 0, 0, () => roll, out _);
                Assert.That(path.PauseMs, Is.InRange(2000, 5999));
                Assert.AreEqual(0, path.Index, "one point: the route stays on it");
            }

            Assert.IsFalse(new AlphaWalkPath(new WorldPosition[0]).Tick(0, 0, 0, 16, () => 0, out _));
        }

        [Test]
        public void DayWindowOfRandomInteractionIsSixToTwentyThree()
        {
            Assert.IsFalse(AlphaNpcDayWindow.IsDay(6 * Hour - 1));
            Assert.IsTrue(AlphaNpcDayWindow.IsDay(6 * Hour));
            Assert.IsTrue(AlphaNpcDayWindow.IsDay(23 * Hour - 1));
            Assert.IsFalse(AlphaNpcDayWindow.IsDay(23 * Hour));
        }

        [Test]
        public void TheRingPatrolIsAFallbackAndSaysSo()
        {
            StringAssert.StartsWith("CUBEGAME_FALLBACK", PatrolRoute.Provenance);
            StringAssert.DoesNotContain("UNKNOWN", PatrolRoute.Provenance, "Alpha patrol and schedule are recovered, not unknown");
            StringAssert.Contains("SpawnLocationBehavior", AlphaSpawnLocationSchedule.Status);
            StringAssert.Contains("WalkPathBehavior", AlphaWalkPath.Status);
        }
    }
}
