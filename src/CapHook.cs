using System;
using HarmonyLib;

namespace Kinsmen
{
    /// <summary>
    /// The lowering. Skills.GetSkillLevel, which every skill effect reads its level through, copies
    /// the saved level into a local, hands that copy to SEMan.ModifySkillLevel for effects to add
    /// to, then floors and returns it; that is the method's only caller in the game. This prefix
    /// lowers the copy before effects add, so gear and potions add their full levels on top, and
    /// the saved level is never reached. It only ever lowers, so a caller asking what effects add
    /// alone (starting from 0) gets the same answer as without Kinsmen.
    /// </summary>
    [HarmonyPatch(typeof(SEMan), "ModifySkillLevel")]
    internal static class CapHook
    {
        private static readonly AccessTools.FieldRef<SEMan, Character> CharacterOf = AccessTools.FieldRefAccess<SEMan, Character>("m_character");

        private static void Prefix(SEMan __instance, Skills.SkillType skill, ref float level)
        {
            try
            {
                var table = RecordClient.Table;
                if (!table.Holds) return;
                var player = Player.m_localPlayer;
                if (player == null || CharacterOf(__instance) != player) return;
                var counted = table.Lower((int)skill, level);
                if (counted < level) level = counted;
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("lowering a skill", e);
            }
        }
    }
}
