using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;

namespace Kinsmen.SelfTest
{
    /// <summary>
    /// The self-test, on the player's own character, which it reads and never changes. It counts
    /// from a made-up record for a moment, in which a character of its own stands at 0 in the
    /// character's best listed skill, so that skill is held; then puts the server's record back.
    /// It checks what the game's own GetSkillLevel then gives (effects added on top of the counted
    /// level), that the saved level and the bytes the save writes stay the same throughout, that the
    /// effect icon shows without entering the character's effects, and that the game's own
    /// Skill.Raise, run on skill objects of the test's own, ends the same with the hold in force and
    /// without. Results are [selftest] lines in LogOutput.log, ending with a DONE line.
    /// </summary>
    internal sealed class Tests
    {
        private const long SomeoneElse = 424242424242L;

        /// <summary>A skill at this level or above is held by a character at 0 under the default gap of 20.</summary>
        private const float HighEnough = 30f;

        private readonly ManualLogSource _log;
        private readonly List<string> _failed = new List<string>();
        private int _passed;
        private int _skipped;

        public Tests(ManualLogSource log)
        {
            _log = log;
        }

        public void Run()
        {
            var player = Player.m_localPlayer;
            var skills = player.GetSkills();
            Write("Kinsmen self-test. It reads your character and changes nothing it keeps.");

            var trained = skills.GetSkillList();
            var kept = trained.ToDictionary(s => s.m_info.m_skill, s => (s.m_level, s.m_accumulator));
            var saveBefore = Save(skills);
            var listed = SkillList.Parse(Plugin.SkillNames, new List<string>());
            var target = trained
                .Where(s => listed.Contains((int)s.m_info.m_skill) && s.m_level >= HighEnough)
                .OrderByDescending(s => s.m_level)
                .FirstOrDefault();

            if (target == null)
            {
                Skip("no listed skill of this character is at " + N(HighEnough) + " or more, so none can be held");
            }
            else
            {
                Guarded("holding " + target.m_info.m_skill, () => Held(player, skills, target, saveBefore));
            }

            Check(skills.GetSkillList().Count == trained.Count, "the character has the same skills as before the test",
                "had " + trained.Count + ", now " + skills.GetSkillList().Count);
            Check(skills.GetSkillList().All(s => kept.TryGetValue(s.m_info.m_skill, out var was) && was.m_level == s.m_level && was.m_accumulator == s.m_accumulator),
                "every saved level and progress is as before the test", "a level or its progress moved");
            Check(Same(saveBefore, Save(skills)), "the save writes the same bytes as before the test", "the bytes differ");
            Summary();
        }

        private void Held(Player player, Skills skills, Skills.Skill target, byte[] saveBefore)
        {
            var type = target.m_info.m_skill;
            var saved = target.m_level;
            var fromEffects = 0f;
            player.GetSEMan().ModifySkillLevel(type, ref fromEffects);
            var unheld = skills.GetSkillLevel(type);

            var madeUp = new Record();
            madeUp.Note(SomeoneElse, "Self-test", new Dictionary<int, float> { [(int)type] = 0f }, 0);
            var serverRecord = RecordClient.UseRecord(madeUp);
            var withHold = new Skills.Skill(target.m_info);
            try
            {
                var counted = RecordClient.Table.Lower((int)type, saved);
                var expected = (float)Math.Floor(counted + fromEffects);
                var asked = skills.GetSkillLevel(type);
                Note(type + ": saved " + N(saved) + ", counted " + N(counted) + ", effects add " + N(fromEffects) + ", the game gives " + N(asked) + " (" + N(unheld) + " before)");
                Check(asked == expected && asked < unheld, "the game counts a held skill lower, with effects added on top",
                    "gave " + N(asked) + ", expected " + N(expected) + " below " + N(unheld));
                Check(target.m_level == saved, "the saved level stays as it was while held", "now " + N(target.m_level));
                Check(Same(saveBefore, Save(skills)), "the save writes the same bytes while a skill is held", "the bytes differ");

                var shown = new List<StatusEffect>();
                HeldEffect.Refresh();
                player.GetSEMan().GetHUDStatusEffects(shown);
                Check(shown.OfType<KinsmenIcon>().Any(i => i.Held >= 1), "the effect icon shows while a skill is held", "not in the HUD's list");
                Check(!player.GetSEMan().GetStatusEffects().OfType<KinsmenIcon>().Any(), "the icon is not among the character's own effects",
                    "the character carries it");

                var caps = RecordClient.DescribeCaps();
                Note(caps.Replace("\n", " | "));
                Check(caps.IndexOf(RecordClient.Name(type) + ": above ", StringComparison.Ordinal) >= 0,
                    "/kinsmen caps lists the held skill's cap", "it says: " + caps);

                for (var i = 0; i < 400; i++) withHold.Raise(1f);
            }
            finally
            {
                RecordClient.UseRecord(serverRecord);
                HeldEffect.Refresh();
            }

            Check(skills.GetSkillLevel(type) == unheld, "the server's record counts again once the test's is gone",
                "gave " + N(skills.GetSkillLevel(type)) + ", " + N(unheld) + " before");

            var without = new Skills.Skill(target.m_info);
            for (var i = 0; i < 400; i++) without.Raise(1f);
            Check(withHold.m_level == without.m_level && withHold.m_accumulator == without.m_accumulator,
                "the game's own rising ends the same with a hold in force and without",
                "held " + N(withHold.m_level) + "/" + N(withHold.m_accumulator) + ", free " + N(without.m_level) + "/" + N(without.m_accumulator));
        }

        private static byte[] Save(Skills skills)
        {
            var package = new ZPackage();
            skills.Save(package);
            return package.GetArray();
        }

        private static string N(float value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        private static bool Same(byte[] a, byte[] b) => a.Length == b.Length && a.SequenceEqual(b);

        private void Guarded(string what, Action step)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                _failed.Add(what + ": " + e.Message);
                _log.LogError("[selftest] FAIL " + what + ": " + e);
            }
        }

        private void Check(bool ok, string what, string otherwise)
        {
            if (ok)
            {
                _passed++;
                Write("PASS " + what);
            }
            else
            {
                _failed.Add(what + " (" + otherwise + ")");
                Write("FAIL " + what + " (" + otherwise + ")");
            }
        }

        private void Skip(string why)
        {
            _skipped++;
            Write("SKIP " + why);
        }

        private void Note(string text) => Write("NOTE " + text);

        private void Write(string text) => _log.LogInfo("[selftest] " + text);

        private void Summary()
        {
            var line = "Kinsmen self-test: " + _passed + " passed, " + _failed.Count + " failed, " + _skipped + " skipped.";
            Write("DONE " + line);
            Chat.instance.OrNull()?.AddString(line);
            foreach (var failure in _failed) Chat.instance.OrNull()?.AddString("Failed: " + failure);
        }
    }
}
