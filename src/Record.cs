using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Kinsmen
{
    /// <summary>One character in the record: who, when last seen, and their real level in each listed skill.</summary>
    internal sealed class RecordEntry
    {
        public RecordEntry(long id, string name, long lastSeen, Dictionary<int, float> levels)
        {
            Id = id;
            Name = name;
            LastSeen = lastSeen;
            Levels = levels;
        }

        /// <summary>The character's id (Player.GetPlayerID), the same in every world.</summary>
        public long Id { get; }

        public string Name { get; }

        /// <summary>Seconds since 1970, UTC.</summary>
        public long LastSeen { get; }

        /// <summary>Real level by skill; a skill not here counts as 0.</summary>
        public IReadOnlyDictionary<int, float> Levels { get; }
    }

    /// <summary>
    /// The server's record of every character that played there, kept as text: a header line,
    /// then one line a character with its id, name, last seen and levels, tab-separated, numbers
    /// written the same way on every PC. The server writes it to a file and sends it to every game.
    /// </summary>
    internal sealed class Record
    {
        private const string Header = "kinsmen record 1";

        private static readonly char[] Between = { ';' };

        private readonly Dictionary<long, RecordEntry> _entries = new Dictionary<long, RecordEntry>();

        public IReadOnlyCollection<RecordEntry> Entries => _entries.Values;

        /// <summary>The character with this id, or none.</summary>
        public RecordEntry Find(long id) => _entries.TryGetValue(id, out var entry) ? entry : null;

        /// <summary>Puts a character in, or replaces what was held of them.</summary>
        public void Note(long id, string name, IDictionary<int, float> levels, long now)
        {
            _entries[id] = new RecordEntry(id, Clean(name), now, new Dictionary<int, float>(levels));
        }

        /// <summary>Takes out every character not seen for more than the given days; 0 keeps everyone. Returns how many went.</summary>
        public int DropUnseen(long now, int days)
        {
            if (days <= 0) return 0;
            var oldest = now - (long)days * 24 * 60 * 60;
            var gone = _entries.Values.Where(e => e.LastSeen < oldest).Select(e => e.Id).ToList();
            foreach (var id in gone) _entries.Remove(id);
            return gone.Count;
        }

        /// <summary>Takes out every character of this name, whatever its case. Returns how many went.</summary>
        public int Forget(string name)
        {
            var wanted = Clean(name);
            var gone = _entries.Values.Where(e => string.Equals(e.Name, wanted, StringComparison.OrdinalIgnoreCase)).Select(e => e.Id).ToList();
            foreach (var id in gone) _entries.Remove(id);
            return gone.Count;
        }

        /// <summary>Everyone but the asking character, with their level in one skill (0 where not trained).</summary>
        public List<Standing> Others(int skill, long self) =>
            _entries.Values
                .Where(e => e.Id != self)
                .Select(e => new Standing(e.Id, e.Name, e.Levels.TryGetValue(skill, out var level) ? level : 0f))
                .ToList();

        public string Write()
        {
            var text = new StringBuilder(Header).Append('\n');
            foreach (var entry in _entries.Values.OrderBy(e => e.Id))
            {
                text.Append(entry.Id.ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(entry.Name).Append('\t')
                    .Append(entry.LastSeen.ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(string.Join(";", entry.Levels.OrderBy(l => l.Key).Select(l =>
                        l.Key.ToString(CultureInfo.InvariantCulture) + "=" + l.Value.ToString("R", CultureInfo.InvariantCulture))))
                    .Append('\n');
            }
            return text.ToString();
        }

        /// <summary>
        /// Reads a record back. A line that cannot be read is left out and told in the problems;
        /// text that is not a record at all reads as an empty one, told too. No text is an empty
        /// record, nothing to tell.
        /// </summary>
        public static Record Read(string text, out List<string> problems)
        {
            problems = new List<string>();
            var record = new Record();
            if (string.IsNullOrEmpty(text)) return record;
            var lines = text.Replace("\r", "").Split('\n');
            if (lines[0] != Header)
            {
                problems.Add("not a Kinsmen record: the first line reads \"" + Shorten(lines[0]) + "\"");
                return record;
            }
            for (var i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                var entry = ReadLine(lines[i]);
                if (entry == null) problems.Add("line " + (i + 1).ToString(CultureInfo.InvariantCulture) + " cannot be read: " + Shorten(lines[i]));
                else record._entries[entry.Id] = entry;
            }
            return record;
        }

        private static RecordEntry ReadLine(string line)
        {
            var parts = line.Split('\t');
            if (parts.Length != 4) return null;
            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) return null;
            if (!long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seen)) return null;
            var levels = new Dictionary<int, float>();
            foreach (var pair in parts[3].Split(Between, StringSplitOptions.RemoveEmptyEntries))
            {
                var halves = pair.Split('=');
                if (halves.Length != 2
                    || !int.TryParse(halves[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var skill)
                    || !float.TryParse(halves[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var level))
                    return null;
                levels[skill] = level;
            }
            return new RecordEntry(id, parts[1], seen, levels);
        }

        /// <summary>A name as the record keeps it: tabs and line breaks, which separate its fields and lines, become spaces.</summary>
        private static string Clean(string name) => (name ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();

        private static string Shorten(string line) => line.Length <= 60 ? line : line.Substring(0, 60) + "...";
    }
}
