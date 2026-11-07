using System.Collections.Generic;
using Xunit;

namespace Kinsmen.Tests
{
    /// <summary>
    /// How far one skill of a character counts on a server. Walking up everyone's levels, the
    /// first gap wider than the set gap splits off the outliers; each of them counts in full up to
    /// the best level below the gap plus the margin, and each level above that by the rate.
    /// Everyone below the gap counts in full. Gap 20, margin 5 and rate a quarter throughout, the
    /// defaults.
    /// </summary>
    public class LadderTests
    {
        private const float Gap = 20f;
        private const float Margin = 5f;
        private const float Rate = 0.25f;

        private static List<Standing> Others(params (string Name, float Level)[] others)
        {
            var list = new List<Standing>();
            for (var i = 0; i < others.Length; i++) list.Add(new Standing(i + 1, others[i].Name, others[i].Level));
            return list;
        }

        private static float Counted(float level, List<Standing> others) =>
            Ladder.Lower(level, Ladder.CeilingFor(others, level, Gap, Margin), Rate);

        [Fact]
        public void AFriendFarAheadCountsFiveAboveTheBestOfTheRestAndAQuarterBeyond()
        {
            Assert.Equal(58.75f, Counted(100f, Others(("Ragna", 40f), ("Bjorn", 25f))));
        }

        [Fact]
        public void TheGroupsOwnSpreadIsNeverHeld()
        {
            Assert.Equal(40f, Counted(40f, Others(("Bjorn", 20f), ("Sven", 30f))));
        }

        [Fact]
        public void TheBestOfTheGroupStaysFreeWithAMaxedCharacterAround()
        {
            Assert.Equal(40f, Counted(40f, Others(("Bjorn", 25f), ("Hero", 100f))));
            Assert.Equal(25f, Counted(25f, Others(("Ragna", 40f), ("Hero", 100f))));
        }

        [Fact]
        public void AGapOfExactlyTheSetSizeHoldsNobodyAndOneLevelMoreDoes()
        {
            Assert.Equal(60f, Counted(60f, Others(("Ragna", 40f))));
            Assert.Equal(49f, Counted(61f, Others(("Ragna", 40f))));
        }

        [Fact]
        public void TwoMaxedCharactersAreBothHeld()
        {
            Assert.Equal(58.75f, Counted(100f, Others(("Twin", 100f), ("Ragna", 40f))));
            Assert.Equal(58.75f, Counted(100f, Others(("Twin", 95f), ("Ragna", 40f))));
            Assert.Equal(57.5f, Counted(95f, Others(("Hero", 100f), ("Ragna", 40f))));
        }

        [Fact]
        public void OnlyTheFirstWideGapSplitsAndEveryoneAboveItIsHeldAlike()
        {
            // 20, 40 | 70, 100: the gaps are 20, 30 and 30, and the first wider than 20 is 40 to 70.
            var others = Others(("Bjorn", 20f), ("Ragna", 40f), ("Mid", 70f));
            Assert.Equal(58.75f, Counted(100f, others));
            Assert.Equal(51.25f, Counted(70f, Others(("Bjorn", 20f), ("Ragna", 40f), ("Hero", 100f))));
        }

        [Fact]
        public void ACharacterWithinTheGapOfTheGroupRaisesTheCeiling()
        {
            // 40, 60 | 100: 40 to 60 is no wider than 20, so the gap is 60 to 100 and the ceiling 65.
            Assert.Equal(73.75f, Counted(100f, Others(("Ragna", 40f), ("Mid", 60f))));
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
        public void ASkillNobodyElseTrainedHoldsAtTheMargin()
        {
            Assert.Equal(28.75f, Counted(100f, Others(("Ragna", 0f), ("Bjorn", 0f))));
        }

        [Fact]
        public void GapsAreMeasuredInWholeLevels()
        {
            // A death takes a share of a level: 40.9 is level 40, and 61.2 is 21 whole levels above it.
            Assert.Equal(60.5f, Counted(60.5f, Others(("Ragna", 40.9f))));
            Assert.Equal(49.725f, Counted(61.2f, Others(("Ragna", 40.9f))), 3);
        }

        [Fact]
        public void TheCeilingNamesTheBestBelowTheGap()
        {
            var ceiling = Ladder.CeilingFor(Others(("Bjorn", 25f), ("Ragna", 40f)), 100f, Gap, Margin);
            Assert.NotNull(ceiling);
            Assert.Equal(45f, ceiling.Limit);
            Assert.Equal("Ragna", ceiling.Holder);
            Assert.Equal(40f, ceiling.HolderLevel);
        }

        [Fact]
        public void NoGapMeansNoCeiling()
        {
            Assert.Null(Ladder.CeilingFor(Others(("Ragna", 40f)), 40f, Gap, Margin));
            Assert.Null(Ladder.CeilingFor(Others(("Ragna", 40f)), 55f, Gap, Margin));
            Assert.Null(Ladder.CeilingFor(Others(), 70f, Gap, Margin));
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
                new Standing(1, "Bjorn", 20f),
                new Standing(2, "Ragna", 40f),
                new Standing(3, "Mid", 70f),
                new Standing(4, "Hero", 100f),
            };
            var counted = Ladder.CountAll(all, Gap, Margin, Rate);
            Assert.Equal(20f, counted[1]);
            Assert.Equal(40f, counted[2]);
            Assert.Equal(51.25f, counted[3]);
            Assert.Equal(58.75f, counted[4]);
        }
    }
}
