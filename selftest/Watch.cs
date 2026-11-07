using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Kinsmen.SelfTest
{
    /// <summary>
    /// The watch, for any character in normal play: /kinsmen selftest watch holds the character's
    /// listed skills as if a player at 0 stood beside it (in this game only; the server and other
    /// players see nothing of it), then watches every rise the game makes while the player plays
    /// as usual. Each rise is checked against the game's own formula, held or not, and after each
    /// level-up the counted level must follow at once. The same command again ends the watch,
    /// lets the server's record count again and reports. It only reads the skills: the game makes
    /// every rise.
    /// </summary>
    [HarmonyPatch(typeof(Skills), "RaiseSkill")]
    internal static class Watch
    {
        private static bool _on;
        private static int _rises;
        private static int _whileHeld;
        private static int _wrong;
        private static int _levelUps;
        private static int _countWrong;
        private static readonly Dictionary<Skills.SkillType, int> PerSkill = new Dictionary<Skills.SkillType, int>();
        private static readonly List<string> Details = new List<string>();

        public static void Toggle(ManualLogSource log)
        {
            if (!_on)
            {
                _rises = _whileHeld = _wrong = _levelUps = _countWrong = 0;
                PerSkill.Clear();
                Details.Clear();
                RecordClient.UseRecord(Holds.Below(Holds.Listed()));
                _on = true;
                var held = RecordClient.Held(Player.m_localPlayer).Select(h => RecordClient.Name(h.Skill)).ToList();
                Chat.instance.OrNull()?.AddString("Kinsmen watch on. Held as if a player at 0 were here: " +
                    (held.Count == 0 ? "nothing yet (no listed skill above the gap)" : string.Join(", ", held)) +
                    ". Play as usual and open the skills window; type /kinsmen selftest watch again to end it and see the report.");
                return;
            }

            _on = false;
            RecordClient.UseRecord(null);
            var report = new Report(log, "Kinsmen watch");
            if (_rises == 0)
            {
                report.Skip("no skill rose while watched");
            }
            else
            {
                foreach (var detail in Details) report.Note(detail);
                report.Note("rises by skill: " + string.Join(", ", PerSkill.Select(p => p.Key + " " + Report.Count(p.Value))));
                report.Check(_wrong == 0, "each of " + Report.Count(_rises) + " rises in play added exactly what the game's formula gives, " + Report.Count(_whileHeld) + " of them while held",
                    Report.Count(_wrong) + " did not");
                report.Check(_countWrong == 0, "after each of " + Report.Count(_levelUps) + " level-ups in play the counted level followed at once", Report.Count(_countWrong) + " did not");
            }
            report.Summary();
        }

        private static void Prefix(Skills __instance, Skills.SkillType skillType, out (bool Watched, float Level, float Progress, bool Held) __state)
        {
            __state = default;
            if (!_on || skillType == Skills.SkillType.None) return;
            var player = Player.m_localPlayer;
            if (player == null || player.GetSkills() != __instance) return;
            var skill = __instance.GetSkillList().FirstOrDefault(s => s.m_info.m_skill == skillType);
            var level = skill?.m_level ?? 0f;
            __state = (true, level, skill?.m_accumulator ?? 0f, RecordClient.Table.Why((int)skillType, level) != null);
        }

        private static void Postfix(Skills __instance, Skills.SkillType skillType, float factor, (bool Watched, float Level, float Progress, bool Held) __state)
        {
            if (!__state.Watched) return;
            _rises++;
            if (__state.Held) _whileHeld++;
            PerSkill[skillType] = PerSkill.TryGetValue(skillType, out var n) ? n + 1 : 1;

            var skill = __instance.GetSkillList().First(s => s.m_info.m_skill == skillType);
            var step = skill.m_info.m_increseStep * factor * Game.m_skillGainRate;
            float level = __state.Level, progress = __state.Progress;
            if (level < 100f)
            {
                progress += step;
                if (progress >= Mathf.Pow(Mathf.Floor(level + 1f), 1.5f) * 0.5f + 0.5f)
                {
                    level = Mathf.Clamp(level + 1f, 0f, 100f);
                    progress = 0f;
                }
            }
            if (skill.m_level != level || skill.m_accumulator != progress)
            {
                _wrong++;
                if (Details.Count < 8) Details.Add(skillType + " at " + Report.N(__state.Level) + "/" + Report.N(__state.Progress) + " got " + Report.N(skill.m_level) + "/" + Report.N(skill.m_accumulator) + ", expected " + Report.N(level) + "/" + Report.N(progress));
            }
            if (skill.m_level == __state.Level) return;

            _levelUps++;
            var player = Player.m_localPlayer;
            var counted = __instance.GetSkillLevel(skillType);
            var shouldBe = Holds.Expected(player, skillType, skill.m_level);
            if (counted != shouldBe)
            {
                _countWrong++;
                if (Details.Count < 8) Details.Add(skillType + " level " + Report.N(skill.m_level) + " counts " + Report.N(counted) + ", should " + Report.N(shouldBe));
            }
            else
            {
                Chat.instance.OrNull()?.AddString("Kinsmen watch: " + RecordClient.Name(skillType) + " rose to " + Report.N(skill.m_level) + ", counts as " + Report.N(counted) + " here");
            }
        }
    }
}
