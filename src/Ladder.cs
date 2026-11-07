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

        /// <summary>The whole level, which gaps are measured in: a death takes a share of a level, and the game counts whole ones.</summary>
        public float Whole => (float)Math.Floor(Level);
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

    /// <summary>The server's numbers for the rule: the gap that splits off the outliers, the margin above the best below it, and what each level beyond counts.</summary>
    internal sealed class LadderSettings
    {
        public LadderSettings(float gap, float margin, float rate)
        {
            Gap = gap;
            Margin = margin;
            Rate = rate;
        }

        public float Gap { get; }

        public float Margin { get; }

        public float Rate { get; }
    }

    /// <summary>
    /// The rule, per skill. Walking up everyone's whole levels, the first step up wider than the
    /// gap splits off the outliers. Everyone below it counts in full, so a group's own spread is
    /// never held; everyone at or above it counts in full up to the best level below the gap plus
    /// the margin, and each level beyond that by the rate. Without such a step nobody is held.
    /// Two characters far ahead stand above the gap together, so neither lifts the other.
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

        /// <summary>The ceiling a level stands under among the others; none when it is not above the first wide gap.</summary>
        public static Ceiling CeilingFor(IEnumerable<Standing> others, float level, float gap, float margin)
        {
            var asking = new Standing(long.MinValue, null, level);
            var split = Split(others.Concat(new[] { asking }), gap);
            if (split == null || asking.Whole < split.Value.Above) return null;
            var best = split.Value.Best;
            return new Ceiling(best.Level + margin, best.Name, best.Level);
        }

        /// <summary>What every character's level counts as, by id.</summary>
        public static Dictionary<long, float> CountAll(IEnumerable<Standing> standings, float gap, float margin, float rate)
        {
            var all = standings.ToList();
            var split = Split(all, gap);
            var counted = new Dictionary<long, float>();
            foreach (var each in all)
            {
                var held = split != null && each.Whole >= split.Value.Above;
                counted[each.Id] = held ? Lower(each.Level, split.Value.Best.Level + margin, rate) : each.Level;
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

        /// <summary>The first step up wider than the gap: the whole level above it and the best character below it. None without one.</summary>
        private static (float Above, Standing Best)? Split(IEnumerable<Standing> standings, float gap)
        {
            Standing below = null;
            foreach (var step in standings.GroupBy(s => s.Whole).OrderBy(g => g.Key))
            {
                if (below != null && step.Key - below.Whole > gap) return (step.Key, below);
                below = step.OrderByDescending(s => s.Level).ThenBy(s => s.Name, StringComparer.Ordinal).First();
            }
            return null;
        }
    }

    /// <summary>
    /// A skill's cap as everyone can see it: a character whose whole level is above HeldAbove
    /// counts in full up to Limit, set by the group's best (Holder, none for the asking character
    /// itself) plus the margin, or by the server by hand.
    /// </summary>
    internal sealed class Cap
    {
        public Cap(float limit, string holder, float holderLevel, float heldAbove, bool setByHand)
        {
            Limit = limit;
            Holder = holder;
            HolderLevel = holderLevel;
            HeldAbove = heldAbove;
            SetByHand = setByHand;
        }

        public float Limit { get; }

        public string Holder { get; }

        public float HolderLevel { get; }

        public float HeldAbove { get; }

        public bool SetByHand { get; }
    }
}
