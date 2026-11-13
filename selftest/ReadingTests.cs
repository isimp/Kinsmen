using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Kinsmen.SelfTest
{
    /// <summary>
    /// The part that only reads the character, safe on any character anywhere. For a moment it
    /// counts from a made-up record in which a character of its own stands at 0 in every listed
    /// skill, so every listed skill above the gap is held, then puts the server's record back. For
    /// every skill of the character it checks what the game then gives through its own entry
    /// points: the level (GetSkillLevel), the factor every effect scales by (GetSkillFactor), the
    /// damage roll's range (GetRandomSkillRange) and the run speed (Player.GetRunSpeedFactor);
    /// that a skill within the gap or off the list is untouched; that an effect adding levels adds
    /// them in full on top; what the skills window, the effect icon and the commands show; and that
    /// the saved levels, their progress and the bytes the save writes never move.
    /// </summary>
    internal static class ReadingTests
    {
        private static readonly AccessTools.FieldRef<SkillsDialog, List<GameObject>> Rows = AccessTools.FieldRefAccess<SkillsDialog, List<GameObject>>("m_elements");

        public static void Run(Report report)
        {
            var player = Player.m_localPlayer;
            var skills = player.GetSkills();
            var kept = Kept.Take(skills);
            var trained = skills.GetSkillList();
            var listed = Holds.Listed();
            var full = trained.ToDictionary(s => s.m_info.m_skill, s => skills.GetSkillLevel(s.m_info.m_skill));
            var effects = trained.ToDictionary(s => s.m_info.m_skill, s => Holds.FromEffects(player, s.m_info.m_skill));
            var runFull = RunSpeed(player);
            var runTrained = trained.Any(s => s.m_info.m_skill == Skills.SkillType.Run);
            var runFactorFull = runTrained ? skills.GetSkillFactor(Skills.SkillType.Run) : 0f;
            report.Note("the character has " + Report.Count(trained.Count) + " skills; Kinsmen lists " + Report.Count(listed.Count));

            RecordClient.UseRecord(Holds.Below(listed));
            try
            {
                if (RecordClient.Table.Exempt) report.Note("this server exempts this character (Exempt), so none of its skills is held; every check below then expects them in full");
                var held = trained.Where(s => RecordClient.Table.Why((int)s.m_info.m_skill, s.m_level) != null).ToList();
                if (held.Count == 0)
                {
                    report.Skip("no listed skill of this character is above " + Report.N(Math.Max(Plugin.Settings.Gap, Plugin.Settings.Minimum)) + ", the higher of the gap above 0 and the minimum, so none can be held; train one past it or use a character that has");
                }

                foreach (var skill in trained)
                {
                    var type = skill.m_info.m_skill;
                    report.Guarded("reading " + type, () => OneSkill(report, player, skills, skill, full[type], effects[type], held.Contains(skill), listed.Contains((int)type)));
                }

                if (held.Any(s => s.m_info.m_skill == Skills.SkillType.Run)) report.Guarded("run speed", () => RunSpeedFollows(report, player, runFull, runFactorFull));
                report.Guarded("an effect on top", () => EffectOnTop(report, player, skills, held));
                report.Guarded("the effect icon", () => Icon(report, player, held));
                report.Guarded("the effect icon's picture", () => IconPicture(report, player));
                report.Guarded("the skills window", () => Window(report, player, skills));
                report.Guarded("the commands", () => Commands(report, held));
                report.Guarded("rising", () => RisingIgnoresTheHold(report, held));
                kept.Unchanged(report, skills, "while skills are held");
            }
            finally
            {
                RecordClient.UseRecord(null);
                HeldEffect.Refresh();
            }

            var back = trained.Where(s => skills.GetSkillLevel(s.m_info.m_skill) != full[s.m_info.m_skill]).Select(s => s.m_info.m_skill.ToString()).ToList();
            report.Check(back.Count == 0, "every skill counts as before once the made-up record is gone", string.Join(", ", back));
            kept.Unchanged(report, skills, "after the reading test");
        }

        private static void OneSkill(Report report, Player player, Skills skills, Skills.Skill skill, float full, float effects, bool held, bool listed)
        {
            var type = skill.m_info.m_skill;
            var saved = skill.m_level;
            var level = skills.GetSkillLevel(type);
            var counted = RecordClient.Table.Lower((int)type, saved);
            if (!held)
            {
                report.Check(level == full, type + (listed ? " within its limit" : " off the list") + " counts in full", "gave " + Report.N(level) + ", " + Report.N(full) + " before");
                return;
            }

            var expected = Mathf.Floor(counted + effects);
            report.Check(level == expected && level < full, type + " at " + Report.N(saved) + " counts as " + Report.N(expected) + " (" + Report.N(counted) + " plus " + Report.N(effects) + " from effects)",
                "the game gives " + Report.N(level) + ", " + Report.N(full) + " in full");
            report.Check(skills.GetSkillFactor(type) == Mathf.Clamp01(expected / 100f), type + "'s factor, which every effect of the skill scales by, follows the counted level",
                "factor " + Report.N(skills.GetSkillFactor(type)));
            skills.GetRandomSkillRange(out var min, out var max, type);
            var middle = Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(expected / 100f));
            report.Check(min == Mathf.Clamp01(middle - 0.15f) && max == Mathf.Clamp01(middle + 0.15f), type + "'s damage roll follows the counted level",
                "range " + Report.N(min) + " to " + Report.N(max));
            report.Check(Holds.FromEffects(player, type) == effects, type + ": what effects add alone is unchanged", "now " + Report.N(Holds.FromEffects(player, type)));
            report.Check(skill.m_level == saved, type + "'s saved level stays " + Report.N(saved) + " while held", "now " + Report.N(skill.m_level));
        }

        private static float RunSpeed(Player player) => (float)AccessTools.Method(typeof(Player), "GetRunSpeedFactor").Invoke(player, null);

        private static void RunSpeedFollows(Report report, Player player, float runFull, float runFactorFull)
        {
            var skills = player.GetSkills();
            var gear = runFull / (1f + runFactorFull * 0.25f);
            var expected = (1f + skills.GetSkillFactor(Skills.SkillType.Run) * 0.25f) * gear;
            var now = RunSpeed(player);
            report.Check(Mathf.Abs(now - expected) < 0.0001f && now < runFull, "run speed follows the counted Run level",
                "factor " + Report.N(now) + ", expected " + Report.N(expected) + ", " + Report.N(runFull) + " in full");
        }

        /// <summary>
        /// Puts an effect of the game that adds levels to a held skill on the character for this
        /// frame, and checks those levels come in full on top of the counted level. The effect is
        /// taken off again at once; one the character already has is never touched.
        /// </summary>
        private static void EffectOnTop(Report report, Player player, Skills skills, List<Skills.Skill> held)
        {
            var heldTypes = held.Select(s => s.m_info.m_skill).ToList();
            var effect = Candidates().FirstOrDefault(se => heldTypes.Contains(se.m_skillLevel) && se.m_skillLevelModifier > 0f && !player.GetSEMan().HaveStatusEffect(se.NameHash()));
            if (effect == null)
            {
                report.Skip("the game has no effect adding levels to a held skill that the character lacks");
                return;
            }

            var type = effect.m_skillLevel;
            var saved = held.First(s => s.m_info.m_skill == type).m_level;
            var before = Holds.FromEffects(player, type);
            player.GetSEMan().AddStatusEffect(effect, resetTime: true);
            try
            {
                var added = Holds.FromEffects(player, type) - before;
                var expected = Mathf.Floor(RecordClient.Table.Lower((int)type, saved) + before + added);
                var level = skills.GetSkillLevel(type);
                report.Note(effect.name + " adds " + Report.N(added) + " to " + type);
                report.Check(added > 0f && level == expected, "an effect's levels (" + effect.name + ", +" + Report.N(added) + " " + type + ") come in full on top of the counted level",
                    "the game gives " + Report.N(level) + ", expected " + Report.N(expected));
            }
            finally
            {
                player.GetSEMan().RemoveStatusEffect(effect.NameHash(), quiet: true);
            }
        }

        /// <summary>The game's effects that add levels to a skill: its list of effects, and the set and equip effects of its items.</summary>
        private static IEnumerable<SE_Stats> Candidates()
        {
            foreach (var se in ObjectDB.instance.m_StatusEffects.OfType<SE_Stats>()) yield return se;
            foreach (var item in ObjectDB.instance.m_items)
            {
                var shared = item.OrNull()?.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared;
                if (shared == null) continue;
                if (shared.m_setStatusEffect is SE_Stats set) yield return set;
                if (shared.m_equipStatusEffect is SE_Stats equip) yield return equip;
            }
        }

        private static void Icon(Report report, Player player, List<Skills.Skill> held)
        {
            var shown = new List<StatusEffect>();
            HeldEffect.Refresh();
            player.GetSEMan().GetHUDStatusEffects(shown);
            var icon = shown.OfType<KinsmenIcon>().FirstOrDefault();
            var count = RecordClient.HeldCount();
            if (held.Count == 0)
            {
                report.Check(icon == null, "no effect icon while nothing is held", "it shows " + Report.Count(icon != null ? icon.Held : 0));
                return;
            }
            report.Check(icon != null && icon.Held == count && count >= 1, "the effect icon shows " + Words.HeldIcon(count), icon == null ? "not in the HUD's list" : "it says " + Report.Count(icon.Held));
            report.Check(!player.GetSEMan().GetStatusEffects().OfType<KinsmenIcon>().Any(), "the icon is not among the character's own effects, so it is never saved or sent", "the character carries it");
        }

        /// <summary>
        /// The icon's picture: the game's Rested knot in bronze, made from the game's own icon. Its
        /// pixels must be bronze and the knot whole; the picture is saved beside the log as
        /// Kinsmen-icon.png, to be looked at.
        /// </summary>
        private static void IconPicture(Report report, Player player)
        {
            var icon = HeldEffect.IconFor(player);
            var sprite = icon != null ? icon.m_icon : null;
            report.Check(sprite != null && sprite.name == "Kinsmen knot", "the effect icon is the game's own Rested knot in bronze",
                sprite == null ? "no icon" : "it is " + sprite.name + ", which stands in when the knot cannot be made (see the log)");
            if (sprite == null || sprite.name != "Kinsmen knot") return;

            var pixels = sprite.texture.GetPixels32();
            var opaque = pixels.Where(p => p.a > 128).ToList();
            var share = (float)opaque.Count / pixels.Length;
            var red = opaque.Count > 0 ? opaque.Average(p => (float)p.r) : 0f;
            var green = opaque.Count > 0 ? opaque.Average(p => (float)p.g) : 0f;
            var blue = opaque.Count > 0 ? opaque.Average(p => (float)p.b) : 0f;
            report.Note("knot " + Report.Count(sprite.texture.width) + " x " + Report.Count(sprite.texture.height) + ", " + Report.N(share * 100f) + "% covered, average " +
                        Report.N(red) + " / " + Report.N(green) + " / " + Report.N(blue));
            report.Check(share > 0.1f && share < 0.9f, "the knot came through whole, neither empty nor a filled square", Report.N(share * 100f) + "% of it is covered");
            report.Check(red > green && green > blue && red > 120f, "the knot is bronze", "average " + Report.N(red) + " / " + Report.N(green) + " / " + Report.N(blue));

            var path = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "Kinsmen-icon.png");
            System.IO.File.WriteAllBytes(path, ImageConversion.EncodeToPNG(sprite.texture));
            report.Note("the icon is saved as " + path);
        }

        /// <summary>Builds the game's skills window for the character, reads every row, and leaves the window as it was.</summary>
        private static void Window(Report report, Player player, Skills skills)
        {
            var dialog = InventoryGui.instance.OrNull()?.m_skillsDialog;
            if (dialog == null)
            {
                report.Skip("the game's skills window is not there");
                return;
            }
            // The window lives inside the inventory, which has to be open for it to set itself up
            // (Setup ends by starting a coroutine on it), so it is opened here as the player opens it.
            var inventoryWasOpen = InventoryGui.IsVisible();
            if (!inventoryWasOpen) InventoryGui.instance.Show(null);
            var wasOpen = dialog.gameObject.activeSelf;
            dialog.Setup(player);
            try
            {
                var rows = Rows(dialog);
                var list = skills.GetSkillList();
                var wrong = new List<string>();
                var marked = 0;
                for (var i = 0; i < list.Count && i < rows.Count; i++)
                {
                    var skill = list[i];
                    var type = skill.m_info.m_skill;
                    var why = RecordClient.Table.Why((int)type, skill.m_level);
                    var counted = RecordClient.Table.Lower((int)type, skill.m_level);
                    var bonus = Utils.FindChild(rows[i].transform, "bonustext").OrNull()?.GetComponent<TMP_Text>().OrNull();
                    var tip = rows[i].GetComponentInChildren<UITooltip>().OrNull()?.m_text ?? "";
                    var lowered = why != null && Math.Floor(counted) < Math.Floor(skill.m_level);
                    if (!lowered)
                    {
                        if (tip.IndexOf(" counts as ", StringComparison.Ordinal) >= 0) wrong.Add(type + " says it counts lower but is not held");
                        continue;
                    }
                    marked++;
                    var shownLevel = skills.GetSkillLevel(type);
                    var slot = (shownLevel - skill.m_level).ToString("+0", System.Globalization.CultureInfo.InvariantCulture);
                    if (bonus == null || !bonus.gameObject.activeSelf || bonus.text != slot) wrong.Add(type + " bonus slot \"" + (bonus != null ? bonus.text : "none") + "\", expected \"" + slot + "\"");
                    else if (slot.StartsWith("-", StringComparison.Ordinal) && bonus.color != new Color(226f / 255f, 104f / 255f, 90f / 255f)) wrong.Add(type + " bonus slot not coloured");
                    var line = Words.Tooltip(RecordClient.Name(type), RecordClient.Whole(counted), why.Holder, RecordClient.Whole(why.HolderLevel), RecordClient.Whole(why.Limit), Plugin.Settings.Rate);
                    if (tip.IndexOf(line, StringComparison.Ordinal) < 0) wrong.Add(type + " tooltip lacks \"" + line.Replace("\n", " ") + "\"");
                }
                report.Note("the skills window marks " + Report.Count(marked) + " rows");
                report.Check(wrong.Count == 0, "the skills window shows each held skill's lowered level and why, and nothing on the rest", string.Join("; ", wrong));
            }
            finally
            {
                if (!wasOpen) dialog.gameObject.SetActive(false);
                if (!inventoryWasOpen) InventoryGui.instance.Hide();
            }
        }

        private static void Commands(Report report, List<Skills.Skill> held)
        {
            var caps = RecordClient.DescribeCaps();
            var described = RecordClient.DescribeHeld();
            report.Note("/kinsmen caps: " + caps.Replace("\n", " | "));
            // Each held skill's line as the command words it, from the table as it stands, so the
            // check follows the wording rather than repeating it.
            var settings = Plugin.Settings;
            var missing = held
                .Where(s => caps.IndexOf(Words.CapLine(RecordClient.Name(s.m_info.m_skill), RecordClient.Table.Cap((int)s.m_info.m_skill, s.m_level), settings.Gap, settings.Rate), StringComparison.Ordinal) < 0)
                .Select(s => RecordClient.Name(s.m_info.m_skill)).ToList();
            report.Check(missing.Count == 0, "/kinsmen caps lists every held skill's cap as it stands", "missing " + string.Join(", ", missing));
            var unsaid = RecordClient.Held(Player.m_localPlayer)
                .Where(h => described.IndexOf(Words.Tooltip(RecordClient.Name(h.Skill), RecordClient.Whole(h.Counted), h.Why.Holder,
                    RecordClient.Whole(h.Why.HolderLevel), RecordClient.Whole(h.Why.Limit), settings.Rate), StringComparison.Ordinal) < 0)
                .Select(h => RecordClient.Name(h.Skill)).ToList();
            report.Check(unsaid.Count == 0, "/kinsmen held says why each held skill counts lower", "missing " + string.Join(", ", unsaid));
        }

        /// <summary>The game's own Skill.Raise, on skill objects of the test's own: the same rises end the same with the hold in force and without.</summary>
        private static void RisingIgnoresTheHold(Report report, List<Skills.Skill> held)
        {
            var target = held.FirstOrDefault();
            if (target == null) return;
            var withHold = new Skills.Skill(target.m_info);
            for (var i = 0; i < 400; i++) withHold.Raise(1f);
            RecordClient.UseRecord(null);
            var without = new Skills.Skill(target.m_info);
            for (var i = 0; i < 400; i++) without.Raise(1f);
            RecordClient.UseRecord(Holds.Below(Holds.Listed()));
            report.Check(withHold.m_level == without.m_level && withHold.m_accumulator == without.m_accumulator,
                "the game's own rising ends the same with a hold in force and without",
                "held " + Report.N(withHold.m_level) + "/" + Report.N(withHold.m_accumulator) + ", free " + Report.N(without.m_level) + "/" + Report.N(without.m_accumulator));
        }
    }
}
