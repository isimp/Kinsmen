using System;
using System.Collections.Generic;
using System.Globalization;

namespace Kinsmen
{
    /// <summary>
    /// The characters a server exempts, as its Exempt setting names them: by name, any case, or by
    /// id (Player.GetPlayerID) where two characters share a name. An exempt character's skills
    /// always count in full; its real levels still go into the record and count for everyone else.
    /// </summary>
    internal sealed class Exemptions
    {
        private readonly HashSet<string> _names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<long> _ids = new HashSet<long>();

        private Exemptions()
        {
        }

        /// <summary>Reads a comma-separated list of names and ids; empty names nobody.</summary>
        public static Exemptions Parse(string text)
        {
            var exemptions = new Exemptions();
            foreach (var part in (text ?? "").Split(','))
            {
                var written = part.Trim();
                if (written.Length == 0) continue;
                if (long.TryParse(written, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) exemptions._ids.Add(id);
                else exemptions._names.Add(written);
            }
            return exemptions;
        }

        /// <summary>Whether this character is exempt, by its id or its name.</summary>
        public bool Covers(long id, string name) => _ids.Contains(id) || (name != null && _names.Contains(name.Trim()));
    }
}
