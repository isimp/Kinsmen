using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kinsmen.SelfTest
{
    /// <summary>
    /// The part that changes the character, and so runs only on a character whose name starts with
    /// KinsmenTest, made for it: it raises a listed skill from where it stands through the game's
    /// own Player.RaiseSkill, one rise at a time, with a made-up record in which a character of its
    /// own stands at 0, so the skill counts in full up to the gap and is held beyond it. It checks
    /// that every rise adds exactly what the game's formula gives (the skill's step, the factor,
    /// effects that speed rising, the world's gain rate), held or not; that after every level-up
    /// the counted level follows at once; that the server learns the new real level; and that a
    /// death (Skills.OnDeath) takes its share of the real level, not the counted one. The
    /// character keeps what it gained and lost.
    /// </summary>
    internal static class Throwaway
    {
        public const string NameStart = "KinsmenTest";

        /// <summary>Raised to here: past the default gap of 20, so the skill is first free, then held.</summary>
        private const float Top = 45f;

        /// <summary>The most rises tried, should a gain rate of the world make rising slow.</summary>
        private const int MostRises = 200000;

        public static bool IsThrowaway(Player player) =>
            player != null && player.GetPlayerName().StartsWith(NameStart, StringComparison.OrdinalIgnoreCase);

        public static IEnumerator Run(Report report)
        {
            if (!IsThrowaway(Player.m_localPlayer))
            {
                report.Check(false, "the progression test runs on a character made for it", "this one is called " +
                    Player.m_localPlayer.GetPlayerName() + "; make a character whose name starts with " + NameStart + ", it raises and lowers real skills");
                yield break;
            }

            var player = Player.m_localPlayer;
            var skills = player.GetSkills();
            var listed = Holds.Listed();
            var type = listed.Contains((int)Skills.SkillType.Swords) ? Skills.SkillType.Swords : (Skills.SkillType)listed.First();
            RecordClient.UseRecord(Holds.Below(listed));
            try
            {
                report.Guarded("rising", () => Rising(report, player, skills, type));
            }
            finally
            {
                RecordClient.UseRecord(null);
            }

            if (ZNet.instance != null && ZRoutedRpc.instance != null)
            {
                var level = Find(skills, type).Level;
                var came = new bool[1];
                yield return Report.Until(() =>
                {
                    var own = RecordClient.ServerRecord?.Find(player.GetPlayerID());
                    return own != null && own.Levels.TryGetValue((int)type, out var recorded) && recorded == level;
                }, 8f, came);
                report.Check(came[0], "the server learns the new real " + type + " level, " + Report.N(level), "not in its record within 8 seconds");
            }

            RecordClient.UseRecord(Holds.Below(listed));
            try
            {
                report.Guarded("a death", () => Death(report, player, skills));
            }
            finally
            {
                RecordClient.UseRecord(null);
            }
            report.Note("the character " + player.GetPlayerName() + " keeps what it gained and lost; delete it when done");
        }

        private static void Rising(Report report, Player player, Skills skills, Skills.SkillType type)
        {
            var step = skills.m_skills.First(d => d.m_skill == type).m_increseStep;
            int rises = 0, whileHeld = 0, wrong = 0, levelUps = 0, countWrong = 0;
            bool sawFree = false, sawHeld = false;
            var details = new List<string>();
            report.Note(type + " starts at " + Report.N(Find(skills, type).Level));
            while (Find(skills, type).Level < Top && rises < MostRises)
            {
                var before = Find(skills, type);
                var held = RecordClient.Table.Why((int)type, before.Level) != null;
                var multiplier = 1f;
                player.GetSEMan().ModifyRaiseSkill(type, ref multiplier);

                player.RaiseSkill(type, 1f);
                rises++;
                if (held) whileHeld++;

                var expected = Expected(before, step * (1f * multiplier) * Game.m_skillGainRate);
                var after = Find(skills, type);
                if (after.Level != expected.Level || after.Progress != expected.Progress)
                {
                    wrong++;
                    if (details.Count < 5) details.Add("at " + Report.N(before.Level) + "/" + Report.N(before.Progress) + " got " + Report.N(after.Level) + "/" + Report.N(after.Progress) + ", expected " + Report.N(expected.Level) + "/" + Report.N(expected.Progress));
                }
                if (after.Level == before.Level) continue;

                levelUps++;
                var counted = skills.GetSkillLevel(type);
                var shouldBe = Holds.Expected(player, type, after.Level);
                var nowHeld = RecordClient.Table.Why((int)type, after.Level) != null;
                sawFree |= !nowHeld;
                sawHeld |= nowHeld;
                if (counted != shouldBe)
                {
                    countWrong++;
                    if (details.Count < 5) details.Add("level " + Report.N(after.Level) + " counts " + Report.N(counted) + ", should " + Report.N(shouldBe));
                }
            }

            foreach (var detail in details) report.Note(detail);
            report.Check(rises > 0 && wrong == 0, "each of " + Report.Count(rises) + " rises through the game's own Player.RaiseSkill added exactly what its formula gives, " + Report.Count(whileHeld) + " of them while held",
                Report.Count(wrong) + " did not");
            report.Check(levelUps > 0 && countWrong == 0, "after each of " + Report.Count(levelUps) + " level-ups the counted level followed at once", Report.Count(countWrong) + " did not");
            report.Check(sawFree && sawHeld, type + " counted in full within the gap and was held past it", "free seen " + sawFree + ", held seen " + sawHeld);
        }

        private static void Death(Report report, Player player, Skills skills)
        {
            var before = skills.GetSkillList().ToDictionary(s => s.m_info.m_skill, s => s.m_level);
            var factor = skills.m_DeathLowerFactor * Game.m_skillReductionRate;
            skills.OnDeath();
            var wrong = new List<string>();
            foreach (var skill in skills.GetSkillList())
            {
                var was = before[skill.m_info.m_skill];
                var expected = was - was * factor;
                if (skill.m_level != expected || skill.m_accumulator != 0f) wrong.Add(skill.m_info.m_skill + " " + Report.N(was) + " to " + Report.N(skill.m_level) + ", expected " + Report.N(expected));
            }
            report.Check(wrong.Count == 0, "a death takes " + Report.N(factor * 100f) + "% of every real level, not of the counted one", string.Join("; ", wrong));
            var countWrong = skills.GetSkillList().Where(s => skills.GetSkillLevel(s.m_info.m_skill) != Holds.Expected(player, s.m_info.m_skill, s.m_level)).Select(s => s.m_info.m_skill.ToString()).ToList();
            report.Check(countWrong.Count == 0, "after the death every skill counts from its new real level", string.Join(", ", countWrong));
        }

        /// <summary>What Skill.Raise makes of a level and its progress for one rise of the given size.</summary>
        private static (float Level, float Progress) Expected((float Level, float Progress) before, float size)
        {
            if (before.Level >= 100f) return before;
            var progress = before.Progress + size;
            var needed = Mathf.Pow(Mathf.Floor(before.Level + 1f), 1.5f) * 0.5f + 0.5f;
            return progress >= needed ? (Mathf.Clamp(before.Level + 1f, 0f, 100f), 0f) : (before.Level, progress);
        }

        private static (float Level, float Progress) Find(Skills skills, Skills.SkillType type)
        {
            var skill = skills.GetSkillList().FirstOrDefault(s => s.m_info.m_skill == type);
            return skill == null ? (0f, 0f) : (skill.m_level, skill.m_accumulator);
        }
    }
}
