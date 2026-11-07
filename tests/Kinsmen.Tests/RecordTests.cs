using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Xunit;

namespace Kinsmen.Tests
{
    /// <summary>
    /// The server's record of every character that played there: their real level in each listed
    /// skill and when they were last seen. It is written to a file on the server and sent to every
    /// game as text.
    /// </summary>
    public class RecordTests
    {
        private const int Swords = 1;
        private const int Bows = 8;
        private const long Day = 24 * 60 * 60;
        private const long Now = 1_800_000_000;

        private static Dictionary<int, float> Levels(params (int Skill, float Level)[] levels) =>
            levels.ToDictionary(l => l.Skill, l => l.Level);

        [Fact]
        public void ARecordWrittenAndReadBackHoldsEveryCharacter()
        {
            var record = new Record();
            record.Note(11, "Ragna", Levels((Swords, 40f), (Bows, 22.5f)), Now);
            record.Note(12, "Hero", Levels((Swords, 100f)), Now - Day);

            var back = Record.Read(record.Write(), out var problems);

            Assert.Empty(problems);
            Assert.Equal(2, back.Entries.Count);
            var ragna = back.Find(11);
            Assert.Equal("Ragna", ragna.Name);
            Assert.Equal(Now, ragna.LastSeen);
            Assert.Equal(40f, ragna.Levels[Swords]);
            Assert.Equal(22.5f, ragna.Levels[Bows]);
            Assert.Equal(100f, back.Find(12).Levels[Swords]);
        }

        [Fact]
        public void LevelsReadBackTheSameOnAPcInAnotherLanguage()
        {
            var before = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                var record = new Record();
                record.Note(11, "Ragna", Levels((Swords, 38.1f)), Now);
                var text = record.Write();
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                Assert.Equal(38.1f, Record.Read(text, out _).Find(11).Levels[Swords]);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = before;
            }
        }

        [Fact]
        public void ACharacterNotSeenForTheSetDaysDropsOutAndOneSeenSinceStays()
        {
            var record = new Record();
            record.Note(11, "Ragna", Levels((Swords, 40f)), Now - 29 * Day);
            record.Note(12, "Gone", Levels((Swords, 80f)), Now - 31 * Day);

            Assert.Equal(1, record.DropUnseen(Now, 30));

            Assert.NotNull(record.Find(11));
            Assert.Null(record.Find(12));
        }

        [Fact]
        public void ZeroDaysKeepsEveryCharacter()
        {
            var record = new Record();
            record.Note(12, "Gone", Levels((Swords, 80f)), Now - 900 * Day);
            Assert.Equal(0, record.DropUnseen(Now, 0));
            Assert.NotNull(record.Find(12));
        }

        [Fact]
        public void ForgettingACharacterByNameRemovesThatOneOnly()
        {
            var record = new Record();
            record.Note(11, "Ragna", Levels((Swords, 40f)), Now);
            record.Note(12, "Alt", Levels((Swords, 70f)), Now);

            Assert.Equal(1, record.Forget("alt"));
            Assert.Null(record.Find(12));
            Assert.NotNull(record.Find(11));
            Assert.Equal(0, record.Forget("Nobody"));
        }

        [Fact]
        public void NotingACharacterAgainReplacesItsNameLevelsAndLastSeen()
        {
            var record = new Record();
            record.Note(11, "Ragna", Levels((Swords, 40f), (Bows, 10f)), Now - Day);
            record.Note(11, "Ragna the Bold", Levels((Swords, 41f)), Now);

            var ragna = record.Find(11);
            Assert.Equal("Ragna the Bold", ragna.Name);
            Assert.Equal(Now, ragna.LastSeen);
            Assert.Equal(41f, ragna.Levels[Swords]);
            Assert.False(ragna.Levels.ContainsKey(Bows));
            Assert.Single(record.Entries);
        }

        [Fact]
        public void ANameWithTabsOrLineBreaksStillReadsBack()
        {
            var record = new Record();
            record.Note(11, "Rag\tna\nX", Levels((Swords, 40f)), Now);
            var back = Record.Read(record.Write(), out var problems);
            Assert.Empty(problems);
            Assert.Equal("Rag na X", back.Find(11).Name);
        }

        [Fact]
        public void AnUnreadableLineIsSkippedAndToldWhileTheRestIsKept()
        {
            var record = new Record();
            record.Note(11, "Ragna", Levels((Swords, 40f)), Now);
            record.Note(12, "Hero", Levels((Swords, 100f)), Now);
            var lines = record.Write().Split('\n').ToList();
            var hero = lines.FindIndex(l => l.Contains("Hero"));
            lines[hero] = "12\tHero\tnot a time\t1=100";

            var back = Record.Read(string.Join("\n", lines), out var problems);

            Assert.Single(problems);
            Assert.NotNull(back.Find(11));
            Assert.Null(back.Find(12));
        }

        [Fact]
        public void TextOfAnotherKindReadsAsAnEmptyRecordAndIsTold()
        {
            var back = Record.Read("<html>not a record</html>", out var problems);
            Assert.Empty(back.Entries);
            Assert.Single(problems);
        }

        [Fact]
        public void NoTextReadsAsAnEmptyRecordWithoutComplaint()
        {
            Assert.Empty(Record.Read("", out var problems).Entries);
            Assert.Empty(problems);
            Assert.Empty(Record.Read(null, out problems).Entries);
            Assert.Empty(problems);
        }

        [Fact]
        public void TheOthersLeaveOutTheAskingCharacterAndCountAnUntrainedSkillAsZero()
        {
            var record = new Record();
            record.Note(11, "Ragna", Levels((Swords, 40f)), Now);
            record.Note(12, "Hero", Levels((Swords, 100f), (Bows, 90f)), Now);

            var others = record.Others(Bows, self: 12);

            var ragna = Assert.Single(others);
            Assert.Equal("Ragna", ragna.Name);
            Assert.Equal(0f, ragna.Level);
        }
    }
}
