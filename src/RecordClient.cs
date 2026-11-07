using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Kinsmen
{
    /// <summary>
    /// A game's side of the record: sends its character's real levels to the server on joining
    /// and whenever one changes, takes the record the server sends, and builds the table its
    /// lowering reads. The levels are read from the skills the character has (Skills.GetSkillList);
    /// nothing here asks the game for a skill, which would add one the character never trained.
    /// </summary>
    internal static class RecordClient
    {
        /// <summary>How often the character's levels are looked at for a change, in seconds.</summary>
        private const float LookEvery = 2f;

        private static Record _record;

        /// <summary>A record the self-test counts from instead of the server's while it is set; the server's keeps arriving underneath.</summary>
        private static Record _madeUp;
        private static CountingTable _table = CountingTable.None;
        private static long _tableFor;
        private static string _sent;
        private static float _nextLook;
        private static bool _joinTold;
        private static HashSet<int> _held = new HashSet<int>();

        /// <summary>What the lowering reads; lowers nothing until the server's record arrived.</summary>
        public static CountingTable Table => _table;

        /// <summary>A world started: forget the last one's record and send afresh.</summary>
        public static void Joined()
        {
            _record = null;
            _madeUp = null;
            _table = CountingTable.None;
            _tableFor = 0;
            _sent = null;
            _nextLook = 0f;
            _joinTold = false;
            HeldEffect.Refresh();
        }

        /// <summary>For the self-test: counts from this record instead of the server's, which keeps arriving underneath, until handed none.</summary>
        internal static void UseRecord(Record madeUp)
        {
            _madeUp = madeUp;
            Rebuild();
        }

        /// <summary>For the self-test: the record as the server last sent it, none before.</summary>
        internal static Record ServerRecord => _record;

        /// <summary>The server sent the record.</summary>
        public static void OnRecord(long sender, string text)
        {
            if (!FromServer(sender)) return;
            _record = Record.Read(text, out var problems);
            foreach (var problem in problems) Plugin.Log.LogWarning("Kinsmen record from the server: " + problem);
            Rebuild();
        }

        /// <summary>The server answered a forget.</summary>
        public static void OnSaid(long sender, string said)
        {
            if (!FromServer(sender)) return;
            Chat.instance.OrNull()?.AddString(said);
            global::Console.instance.OrNull()?.Print(said);
        }

        /// <summary>Builds the table again from the record and the settings; the settings call it when one changes.</summary>
        public static void Rebuild()
        {
            var player = Player.m_localPlayer;
            var unknown = new List<string>();
            _held = SkillList.Parse(Plugin.SkillNames, unknown);
            if (unknown.Count > 0) Plugin.Log.LogWarning("Kinsmen does not know these skills of its list and leaves them free: " + string.Join(", ", unknown));
            var record = _madeUp ?? _record;
            if (record == null || player == null || !Plugin.Enabled)
            {
                _table = CountingTable.None;
                _tableFor = 0;
            }
            else
            {
                var unreadable = new List<string>();
                var caps = SkillList.ParseCaps(Plugin.FixedCaps, unreadable);
                if (unreadable.Count > 0) Plugin.Log.LogWarning("Kinsmen cannot read these caps set by hand and leaves them out: " + string.Join(", ", unreadable));
                _tableFor = player.GetPlayerID();
                var exempt = Exemptions.Parse(Plugin.ExemptNames).Covers(_tableFor, player.GetPlayerName());
                _table = new CountingTable(record, _tableFor, _held, Plugin.Settings, caps, exempt);
            }
            HeldEffect.Refresh();
        }

        /// <summary>Every capped skill's cap as it stands, with the character at its own level among the rest; what /kinsmen caps says.</summary>
        public static string DescribeCaps()
        {
            if (_record == null) return "Kinsmen: no record from the server yet, so no skill has a cap.";
            if (!Plugin.Enabled) return "Kinsmen is turned off on this server: no skill has a cap.";
            var player = Player.m_localPlayer;
            if (player == null) return "Kinsmen: the caps are worked out for a character in the world.";
            var levels = new Dictionary<int, float>();
            foreach (var skill in player.GetSkills().GetSkillList()) levels[(int)skill.m_info.m_skill] = skill.m_level;
            var settings = Plugin.Settings;
            var lines = _table.Capped
                .Select(id => (Name: Name((Skills.SkillType)id), Id: id))
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .Select(s => Words.CapLine(s.Name, _table.Cap(s.Id, levels.TryGetValue(s.Id, out var level) ? level : 0f), settings.Gap, settings.Rate));
            return "Kinsmen caps on this server:\n" + string.Join("\n", lines);
        }

        /// <summary>Every frame: now and then, sends the character's levels if they changed, and tells what is held once on joining.</summary>
        public static void Tick()
        {
            if (Time.unscaledTime < _nextLook) return;
            _nextLook = Time.unscaledTime + LookEvery;
            try
            {
                var player = Player.m_localPlayer;
                if (player == null || ZRoutedRpc.instance == null || ZNet.instance == null) return;
                if ((_madeUp ?? _record) != null && player.GetPlayerID() != _tableFor && Plugin.Enabled) Rebuild();
                SendIfChanged(player);
                TellOnJoining(player);
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("sending the character's levels", e);
            }
        }

        /// <summary>Asks the server to take a character out of the record.</summary>
        public static void AskToForget(string name)
        {
            if (ZRoutedRpc.instance == null)
            {
                Chat.instance.OrNull()?.AddString("Kinsmen keeps its record in a world; join one first.");
                return;
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(RecordMessages.Forget, name);
        }

        /// <summary>The character's held skills, each with what it counts as and why; what /kinsmen held says.</summary>
        public static string DescribeHeld()
        {
            if (_record == null) return "Kinsmen: no record from the server yet, so every skill counts in full.";
            if (!Plugin.Enabled) return "Kinsmen is turned off on this server: every skill counts in full.";
            if (_table.Exempt) return Words.Exempt;
            var held = Held(Player.m_localPlayer);
            if (held.Count == 0) return "Kinsmen: none of your skills count lower here.";
            var text = new StringBuilder();
            foreach (var (skill, real, counted, why) in held)
            {
                text.Append(Words.Tooltip(Name(skill), Whole(counted), why.Holder, Whole(why.HolderLevel), Whole(why.Limit), Plugin.Settings.Rate)).Append('\n');
            }
            return text.ToString().TrimEnd('\n');
        }

        /// <summary>Who is in the record and when they were last seen; what /kinsmen record says.</summary>
        public static string DescribeRecord()
        {
            if (_record == null) return "Kinsmen: no record from the server yet.";
            if (_record.Entries.Count == 0) return "Kinsmen: the record is empty.";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return "Kinsmen record: " + string.Join(", ", _record.Entries
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Select(e => e.Name + " (" + Seen(now - e.LastSeen) + ")"));
        }

        /// <summary>How many of the character's skills count lower here, for the effect icon.</summary>
        public static int HeldCount() => Held(Player.m_localPlayer).Count;

        /// <summary>The character's trained held skills that count at least a whole level lower here: skill, real level, counted level, why.</summary>
        public static List<(Skills.SkillType Skill, float Real, float Counted, Ceiling Why)> Held(Player player)
        {
            var held = new List<(Skills.SkillType, float, float, Ceiling)>();
            if (player == null || !_table.Holds) return held;
            foreach (var skill in player.GetSkills().GetSkillList())
            {
                var type = skill.m_info.m_skill;
                var real = skill.m_level;
                var counted = _table.Lower((int)type, real);
                if (Math.Floor(counted) >= Math.Floor(real)) continue;
                held.Add((type, real, counted, _table.Why((int)type, real)));
            }
            return held;
        }

        /// <summary>A skill's name in the game's language.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308", Justification = "The game's skill tokens are lower case: $skill_swords.")]
        public static string Name(Skills.SkillType skill) =>
            Localization.instance == null ? skill.ToString() : Localization.instance.Localize("$skill_" + skill.ToString().ToLowerInvariant());

        /// <summary>A level as the game counts it, whole.</summary>
        public static int Whole(float level) => (int)Math.Floor(level);

        private static void SendIfChanged(Player player)
        {
            var levels = new List<(int Skill, float Level)>();
            foreach (var skill in player.GetSkills().GetSkillList())
            {
                var id = (int)skill.m_info.m_skill;
                if (_held.Contains(id)) levels.Add((id, skill.m_level));
            }
            var name = player.GetPlayerName();
            var key = player.GetPlayerID().ToString(CultureInfo.InvariantCulture) + "|" + name + "|" +
                      string.Join(";", levels.Select(l => l.Skill.ToString(CultureInfo.InvariantCulture) + "=" + l.Level.ToString("R", CultureInfo.InvariantCulture)));
            if (key == _sent) return;
            var message = new ZPackage();
            message.Write(player.GetPlayerID());
            message.Write(name);
            message.Write(levels.Count);
            foreach (var (skill, level) in levels)
            {
                message.Write(skill);
                message.Write(level);
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(RecordMessages.Levels, message);
            _sent = key;
        }

        private static void TellOnJoining(Player player)
        {
            if (_joinTold || _record == null) return;
            _joinTold = true;
            if (!Plugin.ShowJoinMessage) return;
            var held = Held(player);
            var joined = Words.Joined(held.Count);
            if (joined == null) return;
            MessageHud.instance.OrNull()?.ShowMessage(MessageHud.MessageType.Center, joined);
            Chat.instance.OrNull()?.AddString(Words.ChatList(held.Select(h => (Name(h.Skill), Whole(h.Counted), Whole(h.Real)))));
        }

        /// <summary>Whether a message came from whatever runs the world: the record is the server's to send.</summary>
        private static bool FromServer(long sender)
        {
            if (ZNet.instance == null) return false;
            if (ZNet.instance.IsServer()) return sender == ZDOMan.GetSessionID();
            var server = ZNet.instance.GetServerPeer();
            return server != null && sender == server.m_uid;
        }

        private static string Seen(long secondsAgo)
        {
            var days = secondsAgo / (24 * 60 * 60);
            if (days <= 0) return "seen today";
            return days == 1 ? "seen yesterday" : "seen " + days.ToString(CultureInfo.InvariantCulture) + " days ago";
        }
    }
}
