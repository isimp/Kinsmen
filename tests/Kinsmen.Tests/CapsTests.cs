using System.Collections.Generic;
using Xunit;

namespace Kinsmen.Tests
{
    /// <summary>
    /// The caps as everyone can see them, and caps the server sets by hand. A skill's cap is where a
    /// character far ahead counts in full up to: the best level of the group plus the margin, the
    /// group being everyone below the first wide gap. A cap set by hand replaces it for that skill.
    /// </summary>
    public class CapsTests
    {
        private static readonly int Swords = (int)Skills.SkillType.Swords;
        private static readonly int Bows = (int)Skills.SkillType.Bows;
        private static readonly int Fishing = (int)Skills.SkillType.Fishing;
        private const long Self = 99;

        private static readonly LadderSettings Defaults = new LadderSettings(20f, 5f, 0.25f);

        private static Record Group()
        {
            var record = new Record();
            record.Note(1, "Bjorn", new Dictionary<int, float> { [Swords] = 25f, [Bows] = 10f }, 0);
            record.Note(2, "Ragna", new Dictionary<int, float> { [Swords] = 40f }, 0);
            record.Note(3, "Hero", new Dictionary<int, float> { [Swords] = 100f, [Bows] = 90f }, 0);
            return record;
        }

        [Fact]
        public void TheGroupsBestIsTheBestBelowTheFirstWideGap()
        {
            var group = Group().Others(Swords, Self);
            var best = Ladder.GroupBest(group, 20f);
            Assert.Equal("Ragna", best.Name);
            Assert.Equal(40f, best.Level);
        }

        [Fact]
        public void WithoutAWideGapTheGroupsBestIsTheBestOfAll()
        {
            var best = Ladder.GroupBest(new List<Standing> { new Standing(1, "A", 20f), new Standing(2, "B", 35f) }, 20f);
            Assert.Equal("B", best.Name);
            Assert.Null(Ladder.GroupBest(new List<Standing>(), 20f));
        }

        [Fact]
        public void EveryListedSkillShowsItsCapAndWhoSetsIt()
        {
            var table = new CountingTable(Group(), Self, new HashSet<int> { Swords, Bows }, Defaults);
            var swords = table.Cap(Swords, 30f);
            Assert.Equal(45f, swords.Limit);
            Assert.Equal("Ragna", swords.Holder);
            Assert.Equal(60f, swords.HeldAbove);
            // Bows: 10 and 90 are 80 apart, so the group is Bjorn alone.
            Assert.Equal(15f, table.Cap(Bows, 0f).Limit);
        }

        [Fact]
        public void TheCharactersOwnLiveLevelCountsInTheGroupItBelongsTo()
        {
            // Asked at 50 Swords, the character stands with Ragna's 40, so the group's best is its own 50.
            var cap = new CountingTable(Group(), Self, new HashSet<int> { Swords }, Defaults).Cap(Swords, 50f);
            Assert.Equal(55f, cap.Limit);
            Assert.Null(cap.Holder);
        }

        [Fact]
        public void ASkillOffTheListHasNoCap()
        {
            Assert.Null(new CountingTable(Group(), Self, new HashSet<int> { Swords }, Defaults).Cap(Fishing, 0f));
        }

        [Fact]
        public void ACapSetByHandHoldsEveryoneAboveItWhateverTheGroup()
        {
            var fixedCaps = new Dictionary<int, float> { [Swords] = 50f };
            var table = new CountingTable(Group(), Self, new HashSet<int> { Swords }, Defaults, fixedCaps);
            Assert.Equal(62.5f, table.Lower(Swords, 100f));
            Assert.Equal(50f, table.Lower(Swords, 50f));
            var why = table.Why(Swords, 100f);
            Assert.Equal(50f, why.Limit);
            Assert.Null(why.Holder);
            var cap = table.Cap(Swords, 0f);
            Assert.Equal(50f, cap.Limit);
            Assert.True(cap.SetByHand);
        }

        [Fact]
        public void ACapSetByHandHoldsEvenAloneAndOnASkillOffTheList()
        {
            var fixedCaps = new Dictionary<int, float> { [Fishing] = 30f };
            var table = new CountingTable(new Record(), Self, new HashSet<int>(), Defaults, fixedCaps);
            Assert.Equal(37.5f, table.Lower(Fishing, 60f));
        }

        [Fact]
        public void CapsSetByHandAreReadByNameWithTheirLevel()
        {
            var unknown = new List<string>();
            var caps = SkillList.ParseCaps("Swords=50, blood magic = 30, Sorcery=5, Bows=many, Axes", unknown);
            Assert.Equal(new Dictionary<int, float> { [Swords] = 50f, [(int)Skills.SkillType.BloodMagic] = 30f }, caps);
            Assert.Equal(new List<string> { "Sorcery=5", "Bows=many", "Axes" }, unknown);
        }

        [Fact]
        public void NoCapsSetByHandIsTheDefault()
        {
            Assert.Empty(SkillList.ParseCaps("", new List<string>()));
        }

        [Fact]
        public void TheCapListSaysEachSkillsCapPlainly()
        {
            Assert.Equal("Swords: above 60 counts in full up to 45 (Ragna's 40 + 5), each level beyond a quarter",
                Words.CapLine("Swords", new Cap(45f, "Ragna", 40f, 60f, false), 5f, 0.25f));
            Assert.Equal("Bows: above 25 counts in full up to 10 (yours + 5), each level beyond a quarter",
                Words.CapLine("Bows", new Cap(10f, null, 5f, 25f, false), 5f, 0.25f));
            Assert.Equal("Swords: held by the server at 50, each level beyond a quarter",
                Words.CapLine("Swords", new Cap(50f, null, 0f, 50f, true), 5f, 0.25f));
            Assert.Equal("Fishing: nobody has a level yet", Words.CapLine("Fishing", null, 5f, 0.25f));
        }

        [Fact]
        public void TheSkillsWindowLineNamesTheServerForACapSetByHand()
        {
            Assert.Equal(
                "Swords counts as 62 here.\nThe server holds Swords at 50: up to 50 counts in full, each level above that a quarter.",
                Words.Tooltip("Swords", 62, null, 0, 50, 0.25f));
        }
    }
}
