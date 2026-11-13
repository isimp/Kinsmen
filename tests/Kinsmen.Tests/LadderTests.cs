using System.Collections.Generic;
using Xunit;

namespace Kinsmen.Tests
{
    /// <summary>
    /// How far one skill of a character counts on a server. Walking up everyone's levels, the
    /// first step up wider than the gap splits off the outliers; each of them counts in full up to
    /// the best level below the gap plus the gap, and each level beyond that by the rate. Everyone
    /// below the gap counts in full. Gap 15 and rate a quarter throughout, the defaults.
    /// </summary>
    public class LadderTests
    {
        private const float Gap = 15f;
        private const float Rate = 0.25f;

        private static List<Standing> Others(params (string Name, float Level)[] others)
        {
            var list = new List<Standing>();
            for (var i = 0; i < others.Length; i++) list.Add(new Standing(i + 1, others[i].Name, others[i].Level));
            return list;
        }

        private static float Counted(float level, List<Standing> others) =>
            Ladder.Lower(level, Ladder.CeilingFor(others, level, Gap, 0f), Rate);

        [Fact]
        public void AFriendFarAheadCountsFifteenAboveTheBestOfTheRestAndAQuarterBeyond()
        {
            Assert.Equal(66.25f, Counted(100f, Others(("Ragna", 40f), ("Bjorn", 25f))));
        }

        [Fact]
        public void TrainingNeverLowersTheCountedLevel()
        {
            // One more level of training may add little, but never takes anything away: no cliff at the gap.
            var groups = new[]
            {
                Others(("Bjorn", 25f)),
                Others(("Ragna", 40f), ("Bjorn", 25f)),
                Others(("Bjorn", 20f), ("Ragna", 40f), ("Mid", 70f)),
                Others(("Ragna", 0f), ("Bjorn", 0f)),
                Others(("Ragna", 40.9f), ("Twin", 100f)),
            };
            foreach (var group in groups)
            {
                var before = 0f;
                for (var tenth = 0; tenth <= 1000; tenth++)
                {
                    var counted = Counted(tenth / 10f, group);
                    Assert.True(counted >= before, $"at {tenth / 10f} counted {counted}, below {before} a tenth earlier");
                    before = counted;
                }
            }
        }

        [Fact]
        public void TheHoldStartsAQuarterOfALevelPastTheGapAndGrowsFromThere()
        {
            var bjorn = Others(("Bjorn", 25f));
            Assert.Equal(40f, Counted(40f, bjorn));
            Assert.Equal(40.25f, Counted(41f, bjorn));
            Assert.Equal(41.5f, Counted(46f, bjorn));
        }

        [Fact]
        public void TheGroupsOwnSpreadIsNeverHeld()
        {
            Assert.Equal(40f, Counted(40f, Others(("Bjorn", 20f), ("Sven", 30f))));
            Assert.Equal(40f, Counted(40f, Others(("Bjorn", 25f))));
        }

        [Fact]
        public void TheBestOfTheGroupStaysFreeWithAMaxedCharacterAround()
        {
            Assert.Equal(40f, Counted(40f, Others(("Bjorn", 25f), ("Hero", 100f))));
            Assert.Equal(25f, Counted(25f, Others(("Ragna", 40f), ("Hero", 100f))));
        }

        [Fact]
        public void AGapOfExactlyTheSetSizeHoldsNobodyAndOneLevelMoreBarelyDoes()
        {
            Assert.Equal(55f, Counted(55f, Others(("Ragna", 40f))));
            Assert.Equal(55.25f, Counted(56f, Others(("Ragna", 40f))));
        }

        [Fact]
        public void TwoMaxedCharactersAreBothHeld()
        {
            Assert.Equal(66.25f, Counted(100f, Others(("Twin", 100f), ("Ragna", 40f))));
            Assert.Equal(66.25f, Counted(100f, Others(("Twin", 95f), ("Ragna", 40f))));
            Assert.Equal(65f, Counted(95f, Others(("Hero", 100f), ("Ragna", 40f))));
        }

        [Fact]
        public void OnlyTheFirstWideGapSplitsAndEveryoneAboveItIsHeldAlike()
        {
            // 30, 40 | 70, 100: the steps are 10, 30 and 30, and the first wider than 15 is 40 to 70.
            Assert.Equal(66.25f, Counted(100f, Others(("Bjorn", 30f), ("Ragna", 40f), ("Mid", 70f))));
            Assert.Equal(58.75f, Counted(70f, Others(("Bjorn", 30f), ("Ragna", 40f), ("Hero", 100f))));
        }

        [Fact]
        public void ACharacterWithinTheGapOfTheGroupRaisesTheCeiling()
        {
            // 40, 55 | 100: 40 to 55 is no wider than 15, so the gap is 55 to 100 and the ceiling 70.
            Assert.Equal(77.5f, Counted(100f, Others(("Ragna", 40f), ("Mid", 55f))));
        }

        [Fact]
        public void TheLowestCharacterIsNeverHeld()
        {
            Assert.Equal(10f, Counted(10f, Others(("Ragna", 60f), ("Bjorn", 80f))));
        }

        [Fact]
        public void ACharacterAloneOnTheServerIsNeverHeld()
        {
            Assert.Equal(100f, Counted(100f, Others()));
        }

        [Fact]
        public void ASkillNobodyElseTrainedHoldsAtTheGap()
        {
            Assert.Equal(36.25f, Counted(100f, Others(("Ragna", 0f), ("Bjorn", 0f))));
        }

        [Fact]
        public void GapsCountTheShareOfALevelADeathLeaves()
        {
            // 55.5 is 14.6 above 40.9, inside the gap; 56.2 is 15.3 above it, just past.
            Assert.Equal(55.5f, Counted(55.5f, Others(("Ragna", 40.9f))));
            Assert.Equal(55.975f, Counted(56.2f, Others(("Ragna", 40.9f))), 3);
        }

        [Fact]
        public void TheCeilingNamesTheBestBelowTheGap()
        {
            var ceiling = Ladder.CeilingFor(Others(("Bjorn", 25f), ("Ragna", 40f)), 100f, Gap, 0f);
            Assert.NotNull(ceiling);
            Assert.Equal(55f, ceiling.Limit);
            Assert.Equal("Ragna", ceiling.Holder);
            Assert.Equal(40f, ceiling.HolderLevel);
        }

        [Fact]
        public void NoGapMeansNoCeiling()
        {
            Assert.Null(Ladder.CeilingFor(Others(("Ragna", 40f)), 40f, Gap, 0f));
            Assert.Null(Ladder.CeilingFor(Others(("Ragna", 40f)), 55f, Gap, 0f));
            Assert.Null(Ladder.CeilingFor(Others(), 70f, Gap, 0f));
        }

        [Fact]
        public void CountingNeverRaisesALevelAndNeverDropsBelowTheCeiling()
        {
            foreach (var rate in new[] { 0f, 0.1f, 0.25f, 0.5f, 1f })
            {
                for (var level = 0; level <= 130; level++)
                {
                    for (var limit = 0; limit <= 100; limit += 5)
                    {
                        var counted = Ladder.Lower(level, limit, rate);
                        Assert.True(counted <= level, $"level {level} limit {limit} rate {rate} counted {counted}");
                        Assert.True(counted >= System.Math.Min(level, limit), $"level {level} limit {limit} rate {rate} counted {counted}");
                    }
                }
            }
        }

        [Fact]
        public void ALevelAtOrBelowItsCeilingPassesUnchanged()
        {
            Assert.Equal(0f, Ladder.Lower(0f, 45f, Rate));
            Assert.Equal(30f, Ladder.Lower(30f, 45f, Rate));
            Assert.Equal(45f, Ladder.Lower(45f, 45f, Rate));
            Assert.Equal(70f, Ladder.Lower(70f, (Ceiling)null, Rate));
        }

        [Fact]
        public void ARateOutsideZeroToOneIsHeldToIt()
        {
            Assert.Equal(100f, Ladder.Lower(100f, 45f, 3f));
            Assert.Equal(45f, Ladder.Lower(100f, 45f, -1f));
        }

        [Fact]
        public void CountingEveryoneGivesEachTheirOwnStanding()
        {
            var all = new List<Standing>
            {
                new Standing(1, "Bjorn", 30f),
                new Standing(2, "Ragna", 40f),
                new Standing(3, "Mid", 70f),
                new Standing(4, "Hero", 100f),
            };
            var counted = Ladder.CountAll(all, Gap, Rate, 0f);
            Assert.Equal(30f, counted[1]);
            Assert.Equal(40f, counted[2]);
            Assert.Equal(58.75f, counted[3]);
            Assert.Equal(66.25f, counted[4]);
        }
    }
}
