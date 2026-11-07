using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Kinsmen
{
    /// <summary>
    /// The effect icon on the held player's own screen, beside Rested and the others, saying how
    /// many skills are held. It is shown, not given: the HUD asks SEMan.GetHUDStatusEffects which
    /// effects to draw, and this adds its own to that list only. It never enters the character's
    /// effects, so nothing of it is sent to other players or saved, and no effect of the game
    /// sees it. The icon is the game's Blocking skill icon.
    /// </summary>
    [HarmonyPatch(typeof(SEMan), "GetHUDStatusEffects")]
    internal static class HeldEffect
    {
        /// <summary>How often the held count is worked out again, in seconds; the HUD asks every frame.</summary>
        private const float CountEvery = 1f;

        private static readonly AccessTools.FieldRef<SEMan, Character> CharacterOf = AccessTools.FieldRefAccess<SEMan, Character>("m_character");

        private static KinsmenIcon _icon;
        private static int _held;
        private static float _nextCount;

        /// <summary>Counts again on the next frame: the record, a setting or the world changed.</summary>
        public static void Refresh() => _nextCount = 0f;

        private static void Postfix(SEMan __instance, List<StatusEffect> effects)
        {
            try
            {
                if (!Plugin.ShowEffectIcon) return;
                var player = Player.m_localPlayer;
                if (player == null || CharacterOf(__instance) != player) return;
                if (Time.unscaledTime >= _nextCount)
                {
                    _nextCount = Time.unscaledTime + CountEvery;
                    _held = RecordClient.HeldCount();
                }
                if (_held <= 0) return;
                var icon = Icon(player);
                if (icon == null) return;
                icon.Held = _held;
                effects.Add(icon);
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("showing the effect icon", e);
            }
        }

        private static KinsmenIcon Icon(Player player)
        {
            if (_icon != null) return _icon;
            Sprite sprite = null;
            foreach (var def in player.GetSkills().m_skills)
            {
                if (def.m_skill == Skills.SkillType.Blocking) sprite = def.m_icon;
            }
            if (sprite == null) return null;
            _icon = ScriptableObject.CreateInstance<KinsmenIcon>();
            _icon.name = "Kinsmen";
            _icon.m_name = "Kinsmen";
            _icon.m_icon = sprite;
            return _icon;
        }
    }

    /// <summary>The icon's effect, which says how many skills are held where the game shows an effect's time.</summary>
    internal sealed class KinsmenIcon : StatusEffect
    {
        public int Held { get; set; }

        public override string GetIconText() => Words.HeldIcon(Held);
    }
}
