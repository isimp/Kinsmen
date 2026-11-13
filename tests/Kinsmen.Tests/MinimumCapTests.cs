using System.Collections.Generic;
using Xunit;

namespace Kinsmen.Tests
{
    /// <summary>
    /// A server's minimum cap: however low the rest of the group stands, a character far ahead
    /// counts in full up to at least this level, and by the rate beyond it. It only ever raises the
    /// group's cap, and a cap set by hand stays as the server set it. Gap 15, rate a quarter and a
    /// minimum of 30 throughout.
    /// </summary>
    public class MinimumCapTests
    {
        private const long Self = 12;
        private const float Gap = 15f;
        private const float Rate = 0.25f;
        private const float Minimum = 30f;
        private static readonly int Ride = (int)Skills.SkillType.Ride;
        private static readonly int Swords = (int)Skills.SkillType.Swords;

        private static readonly LadderSettings Settings = new LadderSettings(Gap, Rate, Minimum);

        private static Record Friend(float level)
        {
            var record = new Record();
            record.Note(1, "Kevin", new Dictionary<int, float> { [Ride] = level, [Swords] = 40f }, 0);
            return record;
        }

        private static CountingTable Table(Record record, IDictionary<int, float> fixedCaps = null) =>
            new CountingTable(record, Self, new HashSet<int> { Ride, Swords }, Settings, fixedCaps);

        [Fact]
        public void AFriendFarAheadOfALowGroupCountsInFullUpToTheMinimumAndAQuarterBeyond()
        {
            // Riding 35 next to a 1: the group's cap would be 16, the minimum makes it 30.
            Assert.Equal(31.25f, Table(Friend(1f)).Lower(Ride, 35f));
        }

        [Fact]
        public void AFriendFarAheadButBelowTheMinimumCountsInFull()
        {
            Assert.Equal(25f, Table(Friend(1f)).Lower(Ride, 25f));
            Assert.Equal(30f, Table(Friend(1f)).Lower(Ride, 30f));
            Assert.Null(Table(Friend(1f)).Why(Ride, 30f));
        }

        [Fact]
        public void WhereTheGroupsCapIsHigherTheMinimumChangesNothing()
        {
            // Swords: 40 + 15 = 55 is above 30.
            Assert.Equal(66.25f, Table(Friend(1f)).Lower(Swords, 100f));
            Assert.Equal("Kevin", Table(Friend(1f)).Why(Swords, 100f).Holder);
        }

        [Fact]
        public void ACapSetByHandStaysAsTheServerSetIt()
        {
            var table = Table(Friend(1f), new Dictionary<int, float> { [Ride] = 20f });
            Assert.Equal(23.75f, table.Lower(Ride, 35f));
        }

        [Fact]
        public void NobodyIsHeldWithoutAWideGapWhateverTheMinimum()
        {
            Assert.Equal(35f, Table(Friend(25f)).Lower(Ride, 35f));
        }

        [Fact]
        public void AMinimumOfZeroLeavesTheGroupsCap()
        {
            var table = new CountingTable(Friend(1f), Self, new HashSet<int> { Ride }, new LadderSettings(Gap, Rate, 0f));
            Assert.Equal(20.75f, table.Lower(Ride, 35f));
        }

        [Fact]
        public void TheReasonNamesTheServerWhereTheMinimumHolds()
        {
            var why = Table(Friend(1f)).Why(Ride, 35f);
            Assert.Null(why.Holder);
            Assert.Equal(30f, why.Limit);
            Assert.Equal(30f, Ladder.CeilingFor(Friend(1f).Others(Ride, Self), 35f, Gap, Minimum).Limit);
            Assert.Null(Ladder.CeilingFor(Friend(1f).Others(Ride, Self), 35f, Gap, Minimum).Holder);
        }

        [Fact]
        public void TheCapListSaysTheMinimumHoldsTheSkill()
        {
            var cap = Table(Friend(1f)).Cap(Ride, 35f);
            Assert.Equal(30f, cap.Limit);
            Assert.True(cap.ByMinimum);
            Assert.Equal("Riding: counts in full up to 30 (the server's minimum), each level beyond a quarter",
                Words.CapLine("Riding", cap, Gap, Rate));
            Assert.False(Table(Friend(1f)).Cap(Swords, 100f).ByMinimum);
        }

        [Fact]
        public void TrainingNeverLowersTheCountedLevelUnderAMinimum()
        {
            foreach (var friend in new[] { 0f, 1f, 10f, 14.9f, 15f, 20f, 40f })
            {
                var table = Table(Friend(friend));
                var before = 0f;
                for (var tenth = 0; tenth <= 1000; tenth++)
                {
                    var counted = table.Lower(Ride, tenth / 10f);
                    Assert.True(counted >= before, $"next to {friend}, at {tenth / 10f} counted {counted}, below {before} a tenth earlier");
                    before = counted;
                }
            }
        }

        [Fact]
        public void TheGameCountsWhatTheRuleGivesForEveryLevel()
        {
            var record = Friend(1f);
            var table = Table(record);
            for (var tenth = 0; tenth <= 1000; tenth++)
            {
                var level = tenth / 10f;
                var expected = Ladder.Lower(level, Ladder.CeilingFor(record.Others(Ride, Self), level, Gap, Minimum), Rate);
                Assert.Equal(expected, table.Lower(Ride, level));
            }
        }

        [Fact]
        public void CountingEveryoneRaisesTheLowGroupsCapToTheMinimum()
        {
            var all = new List<Standing> { new Standing(1, "Kevin", 1f), new Standing(2, "Friend", 35f) };
            var counted = Ladder.CountAll(all, Gap, Rate, Minimum);
            Assert.Equal(1f, counted[1]);
            Assert.Equal(31.25f, counted[2]);
        }
    }
}
