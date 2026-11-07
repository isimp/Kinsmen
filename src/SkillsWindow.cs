using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Kinsmen
{
    /// <summary>
    /// The game's skills window already shows a held skill lower: its bonus slot prints the level
    /// the game counts minus the saved one ("100 -42"), and its total bar follows the counted
    /// level. This colours that slot where it went below the saved level, and adds a line to the
    /// skill's tooltip saying what it counts as here and whose level sets it.
    ///
    /// The window's rows follow Skills.GetSkillList in order (SkillsDialog.Setup), and so does
    /// this, through the same list another mod may sort.
    /// </summary>
    [HarmonyPatch(typeof(SkillsDialog), "Setup")]
    internal static class SkillsWindow
    {
        /// <summary>The colour of a bonus slot gone below the saved level.</summary>
        private static readonly Color Lowered = new Color(226f / 255f, 104f / 255f, 90f / 255f);

        private static readonly AccessTools.FieldRef<SkillsDialog, List<GameObject>> Rows = AccessTools.FieldRefAccess<SkillsDialog, List<GameObject>>("m_elements");

        [HarmonyPriority(Priority.Low)]
        private static void Postfix(SkillsDialog __instance, Player player)
        {
            try
            {
                if (player == null || !RecordClient.Table.Holds) return;
                var skills = player.GetSkills().GetSkillList();
                var rows = Rows(__instance);
                var rate = Plugin.Settings.Rate;
                for (var i = 0; i < skills.Count && i < rows.Count; i++)
                {
                    var skill = skills[i];
                    var real = skill.m_level;
                    var type = (int)skill.m_info.m_skill;
                    var counted = RecordClient.Table.Lower(type, real);
                    var why = RecordClient.Table.Why(type, real);
                    if (why == null || Math.Floor(counted) >= Math.Floor(real)) continue;

                    var bonus = Utils.FindChild(rows[i].transform, "bonustext").OrNull()?.GetComponent<TMP_Text>().OrNull();
                    if (bonus != null && bonus.text.StartsWith("-", StringComparison.Ordinal)) bonus.color = Lowered;

                    var tooltip = rows[i].GetComponentInChildren<UITooltip>().OrNull();
                    if (tooltip == null) continue;
                    var line = Words.Tooltip(RecordClient.Name(skill.m_info.m_skill), RecordClient.Whole(counted), why.Holder,
                        RecordClient.Whole(why.HolderLevel), RecordClient.Whole(why.Limit), rate);
                    tooltip.m_text = string.IsNullOrEmpty(tooltip.m_text) ? line : tooltip.m_text + "\n\n" + line;
                }
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("marking the skills window", e);
            }
        }
    }
}
