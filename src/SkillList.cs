using System;
using System.Collections.Generic;
using System.Globalization;

namespace Kinsmen
{
    /// <summary>
    /// Which skills are held, as a comma-separated list of the game's skill names, any case,
    /// spaces ignored ("Blood Magic" is BloodMagic). A number stands for a skill another mod adds,
    /// by its id.
    /// </summary>
    internal static class SkillList
    {
        /// <summary>
        /// Every skill that changes anything in a fight: the weapons, both magics, Blocking and the
        /// ways of moving, and Crafting and Cooking for the arrows, bolts and meads their bonus
        /// yield adds. Wood Cutting only counts when an axe hits a tree, and Fishing and Farming
        /// change no fight.
        /// </summary>
        public const string Default =
            "Swords, Knives, Clubs, Polearms, Spears, Axes, Bows, Crossbows, Unarmed, Pickaxes, ElementalMagic, BloodMagic, " +
            "Blocking, Run, Jump, Dodge, Sneak, Swim, Ride, Crafting, Cooking";

        /// <summary>The skills a list names, by id; names it does not know go into unknown, as written.</summary>
        public static HashSet<int> Parse(string text, List<string> unknown)
        {
            var held = new HashSet<int>();
            foreach (var part in (text ?? "").Split(','))
            {
                var written = part.Trim();
                if (written.Length == 0) continue;
                var skill = Skill(written);
                if (skill == null) unknown.Add(written);
                else held.Add(skill.Value);
            }
            return held;
        }

        /// <summary>
        /// Caps set by hand: "Swords=50, Blood Magic=30", each a skill as in the list and the level it
        /// counts in full up to. Entries it cannot read go into unknown, as written. Empty by default.
        /// </summary>
        public static Dictionary<int, float> ParseCaps(string text, List<string> unknown)
        {
            var caps = new Dictionary<int, float>();
            foreach (var part in (text ?? "").Split(','))
            {
                var written = part.Trim();
                if (written.Length == 0) continue;
                var halves = written.Split('=');
                var skill = halves.Length == 2 ? Skill(halves[0]) : null;
                if (skill == null || !float.TryParse(halves[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var cap) || cap < 0f)
                {
                    unknown.Add(written);
                    continue;
                }
                caps[skill.Value] = cap;
            }
            return caps;
        }

        /// <summary>A skill as written: a game skill's name, any case, spaces ignored, or a mod skill's id. None for anything else.</summary>
        private static int? Skill(string written)
        {
            var name = written.Replace(" ", "");
            if (int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                return number == (int)Skills.SkillType.None || number == (int)Skills.SkillType.All ? (int?)null : number;
            foreach (Skills.SkillType skill in Enum.GetValues(typeof(Skills.SkillType)))
            {
                if (skill == Skills.SkillType.None || skill == Skills.SkillType.All) continue;
                if (string.Equals(skill.ToString(), name, StringComparison.OrdinalIgnoreCase)) return (int)skill;
            }
            return null;
        }
    }
}
