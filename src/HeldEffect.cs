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
    /// sees it. The icon is the game's own Rested knot in bronze (KnotIcon); where that cannot be
    /// made, the game's Blocking skill icon stands in.
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

        /// <summary>For the self-test: the icon's effect as the HUD gets it, made on first use.</summary>
        internal static KinsmenIcon IconFor(Player player) => Icon(player);

        private static KinsmenIcon Icon(Player player)
        {
            if (_icon != null) return _icon;
            var sprite = KnotIcon.Make();
            if (sprite == null)
            {
                foreach (var def in player.GetSkills().m_skills)
                {
                    if (def.m_skill == Skills.SkillType.Blocking) sprite = def.m_icon;
                }
            }
            if (sprite == null) return null;
            _icon = ScriptableObject.CreateInstance<KinsmenIcon>();
            _icon.name = "Kinsmen";
            _icon.m_name = "Kinsmen";
            _icon.m_icon = sprite;
            return _icon;
        }
    }

    /// <summary>
    /// The game's Rested knot in bronze, made once from the game's own icon while it runs, so no
    /// picture of the game is shipped with Kinsmen. The game's sprites cannot be read directly, so
    /// the icon's texture is drawn into a render texture and its square read back from there, then
    /// recoloured (Bronze) into a texture of Kinsmen's own.
    /// </summary>
    internal static class KnotIcon
    {
        private static Sprite _made;

        /// <summary>The bronze knot, none when the game's knot is not there or cannot be read.</summary>
        public static Sprite Make()
        {
            if (_made != null) return _made;
            var rested = ObjectDB.instance.OrNull()?.GetStatusEffect(SEMan.s_statusEffectRested);
            var source = rested != null ? rested.m_icon : null;
            if (source == null || source.texture == null) return null;
            RenderTexture copy = null;
            var before = RenderTexture.active;
            try
            {
                var area = source.packed ? source.textureRect : source.rect;
                var texture = source.texture;
                copy = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(texture, copy);
                RenderTexture.active = copy;
                var width = Mathf.RoundToInt(area.width);
                var height = Mathf.RoundToInt(area.height);
                var knot = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Kinsmen knot" };
                knot.ReadPixels(new Rect(area.x, area.y, width, height), 0, 0);
                var pixels = knot.GetPixels32();
                var bytes = new byte[pixels.Length * 4];
                for (var i = 0; i < pixels.Length; i++)
                {
                    bytes[i * 4] = pixels[i].r;
                    bytes[i * 4 + 1] = pixels[i].g;
                    bytes[i * 4 + 2] = pixels[i].b;
                    bytes[i * 4 + 3] = pixels[i].a;
                }
                Bronze.Recolour(bytes);
                for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color32(bytes[i * 4], bytes[i * 4 + 1], bytes[i * 4 + 2], bytes[i * 4 + 3]);
                knot.SetPixels32(pixels);
                knot.Apply(false, false);
                _made = Sprite.Create(knot, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
                _made.name = "Kinsmen knot";
                return _made;
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("making the bronze knot from the game's Rested icon, the Blocking icon stands in", e);
                return null;
            }
            finally
            {
                RenderTexture.active = before;
                if (copy != null) RenderTexture.ReleaseTemporary(copy);
            }
        }
    }

    /// <summary>The icon's effect, which says how many skills are held where the game shows an effect's time.</summary>
    internal sealed class KinsmenIcon : StatusEffect
    {
        public int Held { get; set; }

        public override string GetIconText() => Words.HeldIcon(Held);
    }
}
