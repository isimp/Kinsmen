using System;
using System.Collections.Generic;
using System.Linq;

namespace Kinsmen
{
    /// <summary>
    /// What a game works out from the record the server sent: how far each held skill of its own
    /// character counts. The character's own entry in the record is left out; its live level is
    /// what is asked about. Built again whenever the record or the settings change, never while a
    /// skill is asked about: the game asks every frame (run speed, stamina), so asking walks a
    /// short prepared list and makes nothing new.
    /// </summary>
    internal sealed class CountingTable
    {
        /// <summary>Lowers nothing: before a record arrives, without Kinsmen on the server, or turned off.</summary>
        public static readonly CountingTable None = new CountingTable();

        /// <summary>Per held skill, everyone else's whole levels from the lowest up, each with its best character.</summary>
        private readonly Dictionary<int, Standing[]> _steps = new Dictionary<int, Standing[]>();

        /// <summary>Caps the server set by hand, by skill: they replace the group's for their skill, listed or not.</summary>
        private readonly Dictionary<int, float> _fixed = new Dictionary<int, float>();
        private readonly LadderSettings _settings;

        /// <summary>The server exempts this character: nothing of it is lowered, while the caps still read as they stand.</summary>
        private readonly bool _exempt;

        private CountingTable()
        {
        }

        public CountingTable(Record record, long self, ISet<int> held, LadderSettings settings, IDictionary<int, float> fixedCaps = null, bool exempt = false)
        {
            _settings = settings;
            _exempt = exempt;
            if (fixedCaps != null)
            {
                foreach (var cap in fixedCaps) _fixed[cap.Key] = cap.Value;
            }
            foreach (var skill in held)
            {
                if (_fixed.ContainsKey(skill)) continue;
                _steps[skill] = record.Others(skill, self)
                    .OrderBy(s => s.Level)
                    .ThenByDescending(s => s.Name, StringComparer.Ordinal)
                    .ToArray();
            }
        }

        /// <summary>Whether any skill can be lowered at all.</summary>
        public bool Holds => !_exempt && (_steps.Count > 0 || _fixed.Count > 0);

        /// <summary>Whether the server exempts this character.</summary>
        public bool Exempt => _exempt;

        /// <summary>What a level of a skill counts as; never more than the level.</summary>
        public float Lower(int skill, float level)
        {
            if (_exempt) return level;
            if (_fixed.TryGetValue(skill, out var cap)) return Ladder.Lower(level, cap, _settings.Rate);
            var best = BestBelowGap(skill, level);
            return best == null ? level : Ladder.Lower(level, best.Level + _settings.Gap, _settings.Rate);
        }

        /// <summary>The ceiling a level stands under where it is lowered, none where it counts in full. A cap set by hand names no holder.</summary>
        public Ceiling Why(int skill, float level)
        {
            if (_exempt) return null;
            if (_fixed.TryGetValue(skill, out var cap)) return level > cap ? new Ceiling(cap, null, 0f) : null;
            var best = BestBelowGap(skill, level);
            if (best == null) return null;
            var limit = best.Level + _settings.Gap;
            return level > limit ? new Ceiling(limit, best.Name, best.Level) : null;
        }

        /// <summary>
        /// A skill's cap as it stands, with the asking character at its own live level among the
        /// rest: where a character far ahead counts in full up to. None for a skill neither listed
        /// nor capped by hand.
        /// </summary>
        public Cap Cap(int skill, float level)
        {
            if (_fixed.TryGetValue(skill, out var cap)) return new Cap(cap, null, 0f, true);
            if (!_steps.TryGetValue(skill, out var steps)) return null;
            var asking = new Standing(long.MinValue, null, level);
            var best = Ladder.GroupBest(steps.Concat(new[] { asking }), _settings.Gap);
            return new Cap(best.Level + _settings.Gap, best.Name, best.Level, false);
        }

        /// <summary>The skills with a cap: the listed ones and those capped by hand.</summary>
        public IEnumerable<int> Capped => _steps.Keys.Concat(_fixed.Keys).Distinct();

        /// <summary>
        /// Walks up everyone's levels with this one among them, and where the first step up is wider
        /// than the gap and this level is at or above it, the best character below; none otherwise.
        /// Makes nothing new.
        /// </summary>
        private Standing BestBelowGap(int skill, float level)
        {
            if (!_steps.TryGetValue(skill, out var steps)) return null;
            var gap = _settings.Gap;
            var placed = false;
            float? previous = null;
            Standing best = null;
            foreach (var step in steps)
            {
                if (!placed && level <= step.Level)
                {
                    placed = true;
                    if (level < step.Level)
                    {
                        if (previous != null && level - previous.Value > gap) return best;
                        previous = level;
                        best = null;
                    }
                }
                if (previous != null && step.Level - previous.Value > gap) return level >= step.Level ? best : null;
                previous = step.Level;
                best = step;
            }
            if (!placed && previous != null && level - previous.Value > gap) return best;
            return null;
        }
    }
}
