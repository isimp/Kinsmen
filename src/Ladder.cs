using System;
using System.Collections.Generic;
using System.Linq;

namespace Kinsmen
{
    /// <summary>One character's real level in one skill, as the record holds it.</summary>
    internal sealed class Standing
    {
        public Standing(long id, string name, float level)
        {
            Id = id;
            Name = name;
            Level = level;
        }

        public long Id { get; }

        public string Name { get; }

        public float Level { get; }
    }

    /// <summary>The level a skill counts in full up to, and the character whose level set it.</summary>
    internal sealed class Ceiling
    {
        public Ceiling(float limit, string holder, float holderLevel)
        {
            Limit = limit;
            Holder = holder;
            HolderLevel = holderLevel;
        }

        /// <summary>Up to here a level counts in full.</summary>
        public float Limit { get; }

        /// <summary>The name of the best character below the gap.</summary>
        public string Holder { get; }

        /// <summary>That character's level, which counts in full.</summary>
        public float HolderLevel { get; }
    }

    /// <summary>
    /// The server's numbers for the rule: the gap, which both splits off the outliers and is how
    /// far above the best below it they count in full; what each level beyond counts; and the
    /// minimum, the level they always count in full up to however low the rest stand (0 for none).
    /// </summary>
    internal sealed class LadderSettings
    {
        public LadderSettings(float gap, float rate, float minimum = 0f)
        {
            Gap = gap;
            Rate = rate;
            Minimum = minimum;
        }

        public float Gap { get; }

        public float Rate { get; }

        public float Minimum { get; }
    }

    /// <summary>
    /// The rule, per skill. Walking up everyone's levels, the first step up wider than the gap
    /// splits off the outliers. Everyone below it counts in full, so a group's own spread is
    /// never held; everyone at or above it counts in full up to the best level below the gap plus
    /// the gap, or up to the minimum where that is higher, and each level beyond that by the rate.
    /// Without such a step nobody is held. The limit lies at or past the gap's far edge, so a
    /// character crossing it keeps every level it had and only the levels beyond count less:
    /// training never lowers the counted level. Two characters far ahead stand above the gap
    /// together, so neither lifts the other.
    /// </summary>
    internal static class Ladder
    {
        /// <summary>What a level counts as under a ceiling: in full up to it, by the rate above it. Never more than the level.</summary>
        public static float Lower(float level, float? limit, float rate)
        {
            if (limit == null || level <= limit.Value) return level;
            var share = Math.Min(1f, Math.Max(0f, rate));
            return limit.Value + (level - limit.Value) * share;
        }

        /// <summary>What a level counts as under a ceiling, none meaning in full.</summary>
        public static float Lower(float level, Ceiling ceiling, float rate) => Lower(level, ceiling?.Limit, rate);

        /// <summary>Where a character far ahead counts in full up to: the best below the gap plus the gap, or the minimum where that is higher.</summary>
        public static float Limit(float best, float gap, float minimum) => Math.Max(best + gap, minimum);

        /// <summary>Whether the minimum, not the group's best, sets the limit.</summary>
        public static bool ByMinimum(float best, float gap, float minimum) => minimum > best + gap;

        /// <summary>The ceiling a level stands under among the others; none when it is not above the first wide gap. Where the minimum sets it, it names no holder.</summary>
        public static Ceiling CeilingFor(IEnumerable<Standing> others, float level, float gap, float minimum)
        {
            var asking = new Standing(long.MinValue, null, level);
            var split = Split(others.Concat(new[] { asking }), gap);
            if (split == null || asking.Level < split.Value.Above) return null;
            var best = split.Value.Best;
            var limit = Limit(best.Level, gap, minimum);
            return ByMinimum(best.Level, gap, minimum) ? new Ceiling(limit, null, 0f) : new Ceiling(limit, best.Name, best.Level);
        }

        /// <summary>What every character's level counts as, by id.</summary>
        public static Dictionary<long, float> CountAll(IEnumerable<Standing> standings, float gap, float rate, float minimum)
        {
            var all = standings.ToList();
            var split = Split(all, gap);
            var counted = new Dictionary<long, float>();
            foreach (var each in all)
            {
                var held = split != null && each.Level >= split.Value.Above;
                counted[each.Id] = held ? Lower(each.Level, Limit(split.Value.Best.Level, gap, minimum), rate) : each.Level;
            }
            return counted;
        }

        /// <summary>The group's best: the best character below the first wide gap, or the best of all without one. None for nobody.</summary>
        public static Standing GroupBest(IEnumerable<Standing> standings, float gap)
        {
            var all = standings.ToList();
            var split = Split(all, gap);
            if (split != null) return split.Value.Best;
            return all.OrderByDescending(s => s.Level).ThenBy(s => s.Name, StringComparer.Ordinal).FirstOrDefault();
        }

        /// <summary>
        /// The first step up wider than the gap: the level above it and the best character below it.
        /// None without one. Real levels, a share of a level included, so the limit the step gives
        /// lies exactly where the hold starts.
        /// </summary>
        private static (float Above, Standing Best)? Split(IEnumerable<Standing> standings, float gap)
        {
            Standing below = null;
            foreach (var each in standings.OrderBy(s => s.Level).ThenByDescending(s => s.Name, StringComparer.Ordinal))
            {
                if (below != null && each.Level - below.Level > gap) return (each.Level, below);
                below = each;
            }
            return null;
        }
    }

    /// <summary>
    /// A skill's cap as everyone can see it: a character far ahead counts in full up to Limit,
    /// set by the group's best (Holder, none for the asking character itself) plus the gap, by the
    /// server's minimum where that is higher, or by the server by hand.
    /// </summary>
    internal sealed class Cap
    {
        public Cap(float limit, string holder, float holderLevel, bool setByHand, bool byMinimum = false)
        {
            Limit = limit;
            Holder = holder;
            HolderLevel = holderLevel;
            SetByHand = setByHand;
            ByMinimum = byMinimum;
        }

        public float Limit { get; }

        public string Holder { get; }

        public float HolderLevel { get; }

        public bool SetByHand { get; }

        /// <summary>Whether the server's minimum, higher than the group's best plus the gap, sets the limit.</summary>
        public bool ByMinimum { get; }
    }
}
