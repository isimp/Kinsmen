using System.Collections.Generic;
using Xunit;

namespace Kinsmen.Tests
{
    /// <summary>
    /// What a game works out from the record the server sent: how far each of its character's
    /// skills counts, from the character's live level and everyone else's recorded one.
    /// </summary>
    public class CountingTableTests
    {
        private const long Hero = 12;
        private static readonly int Swords = (int)Skills.SkillType.Swords;
        private static readonly int Fishing = (int)Skills.SkillType.Fishing;

        private static CountingTable Table(Record record) =>
            new CountingTable(record, Hero, new HashSet<int> { Swords }, new LadderSettings(20f, 5f, 0.25f));

        private static Record Group(float heroRecorded = 100f)
        {
            var record = new Record();
            record.Note(11, "Ragna", new Dictionary<int, float> { [Swords] = 40f, [Fishing] = 10f }, 0);
            record.Note(Hero, "Hero", new Dictionary<int, float> { [Swords] = heroRecorded, [Fishing] = 100f }, 0);
            return record;
        }

        [Fact]
        public void BeforeTheServerSendsARecordNothingIsLowered()
        {
            Assert.Equal(100f, CountingTable.None.Lower(Swords, 100f));
            Assert.Null(CountingTable.None.Why(Swords, 100f));
        }

        [Fact]
        public void AHeldSkillCountsByTheLadder()
        {
            Assert.Equal(58.75f, Table(Group()).Lower(Swords, 100f));
        }

        [Fact]
        public void ASkillOffTheListIsNeverLowered()
        {
            Assert.Equal(100f, Table(Group()).Lower(Fishing, 100f));
            Assert.Null(Table(Group()).Why(Fishing, 100f));
        }

        [Fact]
        public void TheCharactersOwnRecordedLevelIsLeftOutForItsLiveOne()
        {
            // Recorded at 60, which would stand between Ragna and the live 100 if it counted.
            Assert.Equal(58.75f, Table(Group(heroRecorded: 60f)).Lower(Swords, 100f));
        }

        [Fact]
        public void TheLiveLevelDecidesWhereTheCharacterStands()
        {
            var table = Table(Group());
            Assert.Equal(30f, table.Lower(Swords, 30f));
            Assert.Equal(60f, table.Lower(Swords, 60f));
            Assert.Equal(49f, table.Lower(Swords, 61f));
        }

        [Fact]
        public void TheReasonNamesTheHolder()
        {
            var why = Table(Group()).Why(Swords, 100f);
            Assert.Equal("Ragna", why.Holder);
            Assert.Equal(45f, why.Limit);
        }

        [Fact]
        public void NoReasonIsGivenWhereNothingIsLowered()
        {
            Assert.Null(Table(Group()).Why(Swords, 44f));
            Assert.Null(Table(Group()).Why(Swords, 60f));
        }

        [Fact]
        public void AGameAskingEveryLevelGetsWhatTheRuleGives()
        {
            var record = new Record();
            record.Note(1, "Bjorn", new Dictionary<int, float> { [Swords] = 20f }, 0);
            record.Note(2, "Ragna", new Dictionary<int, float> { [Swords] = 40f }, 0);
            record.Note(3, "Mid", new Dictionary<int, float> { [Swords] = 70f }, 0);
            var settings = new LadderSettings(20f, 5f, 0.25f);
            var table = new CountingTable(record, Hero, new HashSet<int> { Swords }, settings);
            for (var tenth = 0; tenth <= 1000; tenth++)
            {
                var level = tenth / 10f;
                var expected = Ladder.Lower(level, Ladder.CeilingFor(record.Others(Swords, Hero), level, 20f, 5f), 0.25f);
                Assert.Equal(expected, table.Lower(Swords, level));
            }
        }

        [Fact]
        public void AnEffectsZeroPassesUntouched()
        {
            // A caller asking only what effects add starts from 0.
            Assert.Equal(0f, Table(Group()).Lower(Swords, 0f));
        }
    }
}
