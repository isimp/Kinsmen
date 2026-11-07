using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Kinsmen.SelfTest
{
    /// <summary>
    /// The part that goes through whatever runs the world: a world of your own (your game is the
    /// server then) or a server running Kinsmen, the local test server included. It never changes
    /// the character. It checks that the server holds the character's real levels, not the counted
    /// ones; then plays a second player by sending the server levels for a made-up character
    /// through the same message a real game sends, and follows each change the way it reaches a
    /// real game: the server notes it, writes its file, sends the record to everyone, and this
    /// game counts again. The made-up character first stands at 0, which holds the character's
    /// skills above the gap; then catches up to within the gap, which lifts the hold; then stands
    /// just past it, which holds again at the made-up level plus the gap. Last it is taken out
    /// of the record, which an admin or the host may do.
    /// </summary>
    internal static class ServerTests
    {
        /// <summary>How long a change may take to come back from the server, in seconds.</summary>
        private const float Patience = 8f;

        public static IEnumerator Run(Report report)
        {
            var player = Player.m_localPlayer;
            var skills = player.GetSkills();
            var kept = Kept.Take(skills);
            if (ZNet.instance == null || ZRoutedRpc.instance == null)
            {
                report.Skip("not in a world");
                yield break;
            }
            report.Note(ZNet.instance.IsServer() ? "this game runs the world" : "a server runs the world");

            var came = new bool[1];
            var self = player.GetPlayerID();
            yield return Report.Until(() => RecordClient.ServerRecord?.Find(self) != null, Patience, came);
            report.Check(came[0], "the server's record arrives and holds this character", "nothing came within " + Report.N(Patience) + " seconds; is Kinsmen on the server?");
            if (!came[0]) yield break;

            var listed = Holds.Listed();
            var own = RecordClient.ServerRecord.Find(self);
            var differs = skills.GetSkillList()
                .Where(s => listed.Contains((int)s.m_info.m_skill))
                .Where(s => !own.Levels.TryGetValue((int)s.m_info.m_skill, out var level) || level != s.m_level)
                .Select(s => s.m_info.m_skill.ToString()).ToList();
            report.Check(differs.Count == 0, "the server holds the character's real level in every listed skill it trained", "differs: " + string.Join(", ", differs));

            var others = RecordClient.ServerRecord.Entries.Where(e => e.Id != self && e.Id != Holds.SomeoneElse).Select(e => e.Name).ToList();
            if (others.Count > 0) report.Note("the record also holds " + string.Join(", ", others) + "; the levels below count them too, and the holds are clearest on a world with nobody else");

            var gap = Plugin.Settings.Gap;
            var handSet = SkillList.ParseCaps(Plugin.FixedCaps, new List<string>());
            var target = skills.GetSkillList()
                .Where(s => listed.Contains((int)s.m_info.m_skill) && !handSet.ContainsKey((int)s.m_info.m_skill) && s.m_level >= gap + 6f)
                .OrderByDescending(s => s.m_level).FirstOrDefault();
            if (target == null)
            {
                report.Skip("no listed skill of this character is at " + Report.N(gap + 6f) + " or more, which a second player's changes need");
                yield break;
            }
            var type = target.m_info.m_skill;
            var saved = target.m_level;
            report.Note("following " + type + " at " + Report.N(saved));

            // Another player far below: the skill is held, through the server.
            yield return Send(type, 0f, report);
            var expected = FromRecord(player, type, saved);
            report.Check(skills.GetSkillLevel(type) == expected && expected < Mathf.Floor(saved + Holds.FromEffects(player, type)), "a second player at 0 holds " + type + " to " + Report.N(expected) + ", through the server",
                "the game gives " + Report.N(skills.GetSkillLevel(type)));
            FileHolds(report, true);

            // They catch up to within the gap: the hold lifts at once.
            var close = (float)Math.Floor(saved) - gap;
            yield return Send(type, close, report);
            var free = Mathf.Floor(saved + Holds.FromEffects(player, type));
            report.Check(skills.GetSkillLevel(type) == free, "when the second player rises to " + Report.N(close) + ", within the gap, " + type + " counts in full again",
                "the game gives " + Report.N(skills.GetSkillLevel(type)) + ", " + Report.N(free) + " in full");

            // One level further apart: held again, at their level plus the gap.
            var far = close - 1f;
            yield return Send(type, far, report);
            expected = FromRecord(player, type, saved);
            report.Check(skills.GetSkillLevel(type) == expected, "with the second player at " + Report.N(far) + ", one level past the gap, " + type + " counts as " + Report.N(expected),
                "the game gives " + Report.N(skills.GetSkillLevel(type)));

            RecordClient.AskToForget(Holds.SomeoneElsesName);
            yield return Report.Until(() => RecordClient.ServerRecord?.Find(Holds.SomeoneElse) == null, Patience, came);
            if (came[0])
            {
                report.Check(skills.GetSkillLevel(type) == free, "once the second player is forgotten, " + type + " counts in full again", "the game gives " + Report.N(skills.GetSkillLevel(type)));
                FileHolds(report, false);
            }
            else
            {
                report.Skip("the server kept the made-up character, so this player is no admin there; it drops out after " +
                            Report.Count(Plugin.ForgetAfterDays) + " days, or an admin types /kinsmen forget " + Holds.SomeoneElsesName);
            }
            kept.Unchanged(report, skills, "after the server test");
        }

        /// <summary>
        /// What the game should give a skill under the record the server sent, worked out by the
        /// rule itself from the record's levels, not by the table this game built from it, so the
        /// way from the record to the table is what is checked.
        /// </summary>
        private static float FromRecord(Player player, Skills.SkillType skill, float saved)
        {
            var settings = Plugin.Settings;
            var others = RecordClient.ServerRecord.Others((int)skill, player.GetPlayerID());
            var counted = Ladder.Lower(saved, Ladder.CeilingFor(others, saved, settings.Gap), settings.Rate);
            return Mathf.Floor(counted + Holds.FromEffects(player, skill));
        }

        /// <summary>Sends the server the made-up character's level in one skill, as a real game sends its own, and waits for the record to come back with it.</summary>
        private static IEnumerator Send(Skills.SkillType skill, float level, Report report)
        {
            var message = new ZPackage();
            message.Write(Holds.SomeoneElse);
            message.Write(Holds.SomeoneElsesName);
            message.Write(1);
            message.Write((int)skill);
            message.Write(level);
            ZRoutedRpc.instance.InvokeRoutedRPC(RecordMessages.Levels, message);
            var came = new bool[1];
            yield return Report.Until(() =>
            {
                var entry = RecordClient.ServerRecord?.Find(Holds.SomeoneElse);
                return entry != null && entry.Levels.TryGetValue((int)skill, out var got) && got == level;
            }, Patience, came);
            report.Check(came[0], "the server notes a second player at " + Report.N(level) + " " + skill + " and sends the record back", "not back within " + Report.N(Patience) + " seconds");
        }

        /// <summary>Where this game runs the world: whether the record's file holds the made-up character as it should.</summary>
        private static void FileHolds(Report report, bool should)
        {
            var path = RecordServer.RecordPath;
            if (!ZNet.instance.IsServer() || path == null) return;
            var text = File.Exists(path) ? File.ReadAllText(path) : "";
            var holds = text.IndexOf(Holds.SomeoneElsesName, StringComparison.Ordinal) >= 0;
            report.Check(holds == should, "the server's record file " + (should ? "holds" : "no longer holds") + " the second player (" + path + ")", "it " + (holds ? "does" : "does not"));
        }
    }
}
