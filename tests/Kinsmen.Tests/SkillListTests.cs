using System.Collections.Generic;
using Xunit;

namespace Kinsmen.Tests
{
    /// <summary>Which skills are held, as the server's setting names them.</summary>
    public class SkillListTests
    {
        private static int Id(Skills.SkillType skill) => (int)skill;

        [Fact]
        public void TheDefaultHoldsEverySkillThatChangesAFightAndLeavesTheRestFree()
        {
            var held = SkillList.Parse(SkillList.Default, new List<string>());

            foreach (var skill in new[]
            {
                Skills.SkillType.Swords, Skills.SkillType.Knives, Skills.SkillType.Clubs, Skills.SkillType.Polearms,
                Skills.SkillType.Spears, Skills.SkillType.Axes, Skills.SkillType.Bows, Skills.SkillType.Crossbows,
                Skills.SkillType.Unarmed, Skills.SkillType.Pickaxes, Skills.SkillType.ElementalMagic,
                Skills.SkillType.BloodMagic, Skills.SkillType.Blocking, Skills.SkillType.Run, Skills.SkillType.Jump,
                Skills.SkillType.Dodge, Skills.SkillType.Sneak, Skills.SkillType.Swim, Skills.SkillType.Ride,
                Skills.SkillType.Crafting, Skills.SkillType.Cooking,
            })
            {
                Assert.True(held.Contains(Id(skill)), skill + " should be held");
            }

            foreach (var skill in new[] { Skills.SkillType.WoodCutting, Skills.SkillType.Fishing, Skills.SkillType.Farming })
            {
                Assert.False(held.Contains(Id(skill)), skill + " should be free");
            }
        }

        [Fact]
        public void NamesAreReadWhateverTheirCaseAndSpacing()
        {
            var held = SkillList.Parse(" swords ,BOWS,,Blood Magic", new List<string>());
            Assert.Equal(new HashSet<int> { Id(Skills.SkillType.Swords), Id(Skills.SkillType.Bows), Id(Skills.SkillType.BloodMagic) }, held);
        }

        [Fact]
        public void BothSettingsReadASkillNameWithOrWithoutItsSpaces()
        {
            var magic = (int)Skills.SkillType.ElementalMagic;
            Assert.Equal(new HashSet<int> { magic }, SkillList.Parse("ElementalMagic", new List<string>()));
            Assert.Equal(new HashSet<int> { magic }, SkillList.Parse("Elemental Magic", new List<string>()));
            Assert.Equal(40f, SkillList.ParseCaps("ElementalMagic=40", new List<string>())[magic]);
            Assert.Equal(40f, SkillList.ParseCaps("Elemental Magic=40", new List<string>())[magic]);
        }

        [Fact]
        public void TheNamesTheSkillsWindowShowsAreNotTheGamesOwnAndAreTold()
        {
            // The window says Fists and Riding; the game's own names are Unarmed and Ride.
            var unknown = new List<string>();
            Assert.Empty(SkillList.Parse("Fists, Riding", unknown));
            Assert.Equal(new List<string> { "Fists", "Riding" }, unknown);
        }

        [Fact]
        public void AnUnknownNameIsToldAndTheOthersStillCount()
        {
            var unknown = new List<string>();
            var held = SkillList.Parse("Swords, Sorcery", unknown);
            Assert.Equal(new HashSet<int> { Id(Skills.SkillType.Swords) }, held);
            Assert.Equal(new List<string> { "Sorcery" }, unknown);
        }

        [Fact]
        public void ANumberStandsForASkillAnotherModAdds()
        {
            var held = SkillList.Parse("Swords, 1834512", new List<string>());
            Assert.Contains(1834512, held);
        }

        [Fact]
        public void TheEveryAndNoneEntriesOfTheGameAreNeverHeld()
        {
            var unknown = new List<string>();
            var held = SkillList.Parse("All, None, 0", unknown);
            Assert.Empty(held);
            Assert.Equal(3, unknown.Count);
        }
    }
}
