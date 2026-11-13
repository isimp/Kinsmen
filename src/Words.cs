using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Kinsmen
{
    /// <summary>What the held character reads. Skill names come in already in the game's language.</summary>
    internal static class Words
    {
        /// <summary>The text under the effect icon.</summary>
        public static string HeldIcon(int count) => Count(count) + " held";

        /// <summary>The message in the middle of the screen on joining; none when nothing is held.</summary>
        public static string Joined(int count)
        {
            if (count <= 0) return null;
            return Count(count) + " of your skills " + (count == 1 ? "counts" : "count") + " lower here";
        }

        /// <summary>The chat line on joining: each held skill with what it counts as and its real level.</summary>
        public static string ChatList(IEnumerable<(string Skill, int Counted, int Real)> held)
        {
            var text = new StringBuilder("Kinsmen: ");
            var first = true;
            foreach (var (skill, counted, real) in held)
            {
                text.Append(first ? skill + " counts as " : ", " + skill + " as ")
                    .Append(Count(counted)).Append(" of ").Append(Count(real));
                if (first) text.Append(" here");
                first = false;
            }
            return text.Append(". Your skills window says why.").ToString();
        }

        /// <summary>The line the skills window adds to a held skill's tooltip; no holder means a cap the server set by hand.</summary>
        public static string Tooltip(string skill, int counted, string holder, int holderLevel, int limit, float rate) =>
            skill + " counts as " + Count(counted) + " here.\n" +
            (holder == null
                ? "The server holds " + skill + " at " + Count(limit)
                : "The best " + skill + " of the group is " + Count(holderLevel) + ", by " + holder) +
            ": up to " + Count(limit) + " counts in full, each level above that " + Rate(rate) + ".";

        /// <summary>What /kinsmen held says to a character the server exempts.</summary>
        public const string Exempt = "Kinsmen: this server exempts you, so every skill of yours counts in full.";

        /// <summary>What plain /kinsmen says: every word that can follow it, the self-test's where it is installed; a word it does not know is named first.</summary>
        public static string Help(bool selfTest, string unknown)
        {
            var text = new StringBuilder();
            if (!string.IsNullOrEmpty(unknown)) text.Append("Kinsmen does not know \"").Append(unknown).Append("\".\n");
            text.Append("Kinsmen commands:\n")
                .Append("/kinsmen held: which of your skills count lower here, and why\n")
                .Append("/kinsmen caps: every held skill's limit on this server\n")
                .Append("/kinsmen record: the characters the server remembers\n")
                .Append("/kinsmen forget Name: take a character out of the record (admins)");
            if (selfTest) text.Append("\n/kinsmen selftest: the self-test; add progression (a KinsmenTest character only) or watch");
            return text.ToString();
        }

        /// <summary>One skill's cap as /kinsmen caps lists it.</summary>
        public static string CapLine(string skill, Cap cap, float gap, float rate)
        {
            if (cap == null) return skill + ": nobody has a level yet";
            if (cap.SetByHand) return skill + ": held by the server at " + Whole(cap.Limit) + ", each level beyond " + Rate(rate);
            if (cap.ByMinimum) return skill + ": counts in full up to " + Whole(cap.Limit) + " (the server's minimum), each level beyond " + Rate(rate);
            var by = (cap.Holder == null ? "your " : cap.Holder + "'s ") + Whole(cap.HolderLevel);
            return skill + ": counts in full up to " + Whole(cap.Limit) +
                   " (" + by + " + " + gap.ToString("0.#", CultureInfo.InvariantCulture) + "), each level beyond " + Rate(rate);
        }

        private static string Whole(float level) => Count((int)Math.Floor(level));

        /// <summary>How much each level above the ceiling counts, in words where it has them.</summary>
        public static string Rate(float rate)
        {
            var percent = (float)Math.Round(rate * 100f, 1);
            switch (percent)
            {
                case 0f: return "nothing";
                case 10f: return "a tenth";
                case 25f: return "a quarter";
                case 50f: return "half";
                case 75f: return "three quarters";
                case 100f: return "in full";
                default: return percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
            }
        }

        private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
