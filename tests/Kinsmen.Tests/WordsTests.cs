using System.Collections.Generic;
using Xunit;

namespace Kinsmen.Tests
{
    /// <summary>What the held character reads: the effect icon's text, the message on joining, the chat list and the skills window's line.</summary>
    public class WordsTests
    {
        [Fact]
        public void TheEffectIconSaysHowManySkillsAreHeld()
        {
            Assert.Equal("17 held", Words.HeldIcon(17));
            Assert.Equal("1 held", Words.HeldIcon(1));
        }

        [Fact]
        public void TheJoiningMessageCountsTheHeldSkills()
        {
            Assert.Equal("17 of your skills count lower here", Words.Joined(17));
            Assert.Equal("1 of your skills counts lower here", Words.Joined(1));
            Assert.Null(Words.Joined(0));
        }

        [Fact]
        public void TheChatListNamesEachHeldSkillWithBothLevels()
        {
            var list = new List<(string, int, int)> { ("Swords", 58, 100), ("Bows", 61, 100) };
            Assert.Equal("Kinsmen: Swords counts as 58 of 100 here, Bows as 61 of 100. Your skills window says why.", Words.ChatList(list));
        }

        [Fact]
        public void TheSkillsWindowLineNamesTheHolderAndTheRule()
        {
            Assert.Equal(
                "Swords counts as 58 here.\nThe best Swords of the group is 40, by Ragna: up to 45 counts in full, each level above that a quarter.",
                Words.Tooltip("Swords", 58, "Ragna", 40, 45, 0.25f));
        }

        [Fact]
        public void TheRateIsSaidInWordsWhereItHasThem()
        {
            Assert.Equal("a quarter", Words.Rate(0.25f));
            Assert.Equal("half", Words.Rate(0.5f));
            Assert.Equal("three quarters", Words.Rate(0.75f));
            Assert.Equal("a tenth", Words.Rate(0.1f));
            Assert.Equal("nothing", Words.Rate(0f));
            Assert.Equal("15%", Words.Rate(0.15f));
        }
    }
}
