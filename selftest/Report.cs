using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace Kinsmen.SelfTest
{
    /// <summary>
    /// One self-test run's results: [selftest] lines in LogOutput.log, a PASS, FAIL, SKIP or NOTE
    /// each, ending with a DONE line, and the summary and every failure in chat.
    /// </summary>
    internal sealed class Report
    {
        private readonly ManualLogSource _log;
        private readonly string _title;
        private readonly List<string> _failed = new List<string>();
        private int _passed;
        private int _skipped;

        public Report(ManualLogSource log, string title)
        {
            _log = log;
            _title = title;
            Write("START " + title);
        }

        public bool Failed => _failed.Count > 0;

        public void Check(bool ok, string what, string otherwise)
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

        public void Skip(string why)
        {
            _skipped++;
            Write("SKIP " + why);
        }

        public void Note(string text) => Write("NOTE " + text);

        /// <summary>Runs a step; a failure inside it is a failed check, told with its stack, and the run goes on.</summary>
        public void Guarded(string what, Action step)
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

        public void Summary()
        {
            var line = _title + ": " + Count(_passed) + " passed, " + Count(_failed.Count) + " failed, " + Count(_skipped) + " skipped.";
            Write("DONE " + line);
            Chat.instance.OrNull()?.AddString(line);
            foreach (var failure in _failed.Take(12)) Chat.instance.OrNull()?.AddString("Failed: " + failure);
            if (_failed.Count > 12) Chat.instance.OrNull()?.AddString("... and " + Count(_failed.Count - 12) + " more in LogOutput.log");
        }

        /// <summary>A number the same way on every PC.</summary>
        public static string N(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        public static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>Waits up to the given seconds for a condition, a frame at a time; the box holds whether it came true.</summary>
        public static IEnumerator Until(Func<bool> condition, float seconds, bool[] came)
        {
            var end = Time.unscaledTime + seconds;
            while (!condition())
            {
                if (Time.unscaledTime > end)
                {
                    came[0] = false;
                    yield break;
                }
                yield return null;
            }
            came[0] = true;
        }

        private void Write(string text) => _log.LogInfo("[selftest] " + text);
    }

    /// <summary>
    /// What the character keeps of its skills, taken before a test touches anything: each skill's
    /// saved level and progress, how many skills it has, and the bytes the game's save writes for
    /// them (Skills.Save, what Player.Save writes into the character file).
    /// </summary>
    internal sealed class Kept
    {
        private readonly Dictionary<Skills.SkillType, (float Level, float Progress)> _skills;
        private readonly byte[] _saved;

        private Kept(Dictionary<Skills.SkillType, (float, float)> skills, byte[] saved)
        {
            _skills = skills;
            _saved = saved;
        }

        public static Kept Take(Skills skills) =>
            new Kept(skills.GetSkillList().ToDictionary(s => s.m_info.m_skill, s => (s.m_level, s.m_accumulator)), Saved(skills));

        public float Level(Skills.SkillType skill) => _skills.TryGetValue(skill, out var kept) ? kept.Level : 0f;

        /// <summary>Checks that nothing the character keeps moved since it was taken.</summary>
        public void Unchanged(Report report, Skills skills, string when)
        {
            var now = skills.GetSkillList();
            report.Check(now.Count == _skills.Count, "the character has the same skills " + when,
                "had " + Report.Count(_skills.Count) + ", now " + Report.Count(now.Count));
            var moved = now.Where(s => !_skills.TryGetValue(s.m_info.m_skill, out var was) || was.Level != s.m_level || was.Progress != s.m_accumulator)
                .Select(s => s.m_info.m_skill + " " + Report.N(Level(s.m_info.m_skill)) + " to " + Report.N(s.m_level)).ToList();
            report.Check(moved.Count == 0, "every saved level and its progress is as before " + when, string.Join(", ", moved));
            report.Check(Saved(skills).SequenceEqual(_saved), "the character's save writes the same skill bytes " + when, "the bytes differ");
        }

        public static byte[] Saved(Skills skills)
        {
            var package = new ZPackage();
            skills.Save(package);
            return package.GetArray();
        }
    }

    /// <summary>Holds made up for a test, and what the game says of a skill, read without changing it.</summary>
    internal static class Holds
    {
        /// <summary>The made-up character's id, which no real character has.</summary>
        public const long SomeoneElse = 424242424242L;

        public const string SomeoneElsesName = "Kinsmen self-test";

        /// <summary>The skills Kinsmen holds on this server, by id.</summary>
        public static HashSet<int> Listed() => SkillList.Parse(Plugin.SkillNames, new List<string>());

        /// <summary>A record with one made-up character at the given level in each of these skills; at 0 it holds every one of them above the gap.</summary>
        public static Record Below(IEnumerable<int> skills, float level = 0f)
        {
            var record = new Record();
            record.Note(SomeoneElse, SomeoneElsesName, skills.ToDictionary(s => s, s => level), 0);
            return record;
        }

        /// <summary>What the character's effects add to a skill now, asked the way Scry asks: from 0, which Kinsmen leaves as it is.</summary>
        public static float FromEffects(Player player, Skills.SkillType skill)
        {
            var added = 0f;
            player.GetSEMan().ModifySkillLevel(skill, ref added);
            return added;
        }

        /// <summary>The level the game should give a skill under the table now: the counted level, effects added, floored.</summary>
        public static float Expected(Player player, Skills.SkillType skill, float saved) =>
            Mathf.Floor(RecordClient.Table.Lower((int)skill, saved) + FromEffects(player, skill));
    }
}
