using System.Collections.Generic;
using Xunit;

namespace Kinsmen.Tests
{
    /// <summary>
    /// Characters the server exempts: their skills always count in full, while their real levels
    /// still go into the record and still count for everyone else's limits.
    /// </summary>
    public class ExemptionTests
    {
        private static readonly int Swords = (int)Skills.SkillType.Swords;
        private static readonly LadderSettings Defaults = new LadderSettings(15f, 0.25f);

        private static Record Group()
        {
            var record = new Record();
            record.Note(11, "Ragna", new Dictionary<int, float> { [Swords] = 40f }, 0);
            record.Note(12, "Hero", new Dictionary<int, float> { [Swords] = 100f }, 0);
            record.Note(13, "Mid", new Dictionary<int, float> { [Swords] = 60f }, 0);
            return record;
        }

        [Fact]
        public void AnExemptCharacterCountsInFull()
        {
            var table = new CountingTable(Group(), 12, new HashSet<int> { Swords }, Defaults, exempt: true);
            Assert.Equal(100f, table.Lower(Swords, 100f));
            Assert.Null(table.Why(Swords, 100f));
            Assert.False(table.Holds);
        }

        [Fact]
        public void AnExemptCharacterStillSeesTheCapsAsTheyStand()
        {
            var exempt = new CountingTable(Group(), 12, new HashSet<int> { Swords }, Defaults, exempt: true);
            var held = new CountingTable(Group(), 12, new HashSet<int> { Swords }, Defaults);
            Assert.Equal(held.Cap(Swords, 100f).Limit, exempt.Cap(Swords, 100f).Limit);
        }

        [Fact]
        public void AnExemptCharacterStillCountsForEveryoneElse()
        {
            // Mid at 60 stands above the gap over Ragna's 40 whether or not Hero is exempt.
            var midTable = new CountingTable(Group(), 13, new HashSet<int> { Swords }, Defaults);
            Assert.Equal(55f + 5f * 0.25f, midTable.Lower(Swords, 60f));
            // Exempting changes only the exempt game's own counting: Hero's 100 stays in the record for everyone.
            Assert.Equal(55f, midTable.Why(Swords, 60f).Limit);
        }

        [Fact]
        public void TheExemptListNamesCharactersWhateverTheirCase()
        {
            var exempt = Exemptions.Parse(" hero , Klaus Der Klaus,,");
            Assert.True(exempt.Covers(12, "Hero"));
            Assert.True(exempt.Covers(99, "klaus der klaus"));
            Assert.False(exempt.Covers(11, "Ragna"));
        }

        [Fact]
        public void ANumberNamesACharacterByIdForTwoOfTheSameName()
        {
            var exempt = Exemptions.Parse("48116079");
            Assert.True(exempt.Covers(48116079, "Olebole"));
            Assert.False(exempt.Covers(48116080, "Olebole"));
        }

        [Fact]
        public void NobodyIsExemptByDefault()
        {
            Assert.False(Exemptions.Parse("").Covers(12, "Hero"));
            Assert.False(Exemptions.Parse(null).Covers(12, "Hero"));
        }

        [Fact]
        public void AnExemptPlayerIsToldSo()
        {
            Assert.Equal("Kinsmen: this server exempts you, so every skill of yours counts in full.", Words.Exempt);
        }
    }
}
