using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace Kinsmen
{
    /// <summary>
    /// The server's side of the record: kept in a file per world under BepInEx/config/Kinsmen,
    /// noted from what each game sends, thinned of characters long unseen, and sent whole to every
    /// game after each change. Runs on whatever runs the world: a dedicated server, or the game of
    /// the player hosting it.
    /// </summary>
    internal static class RecordServer
    {
        /// <summary>The most skills one message may carry; the game has a few dozen, mods add some.</summary>
        private const int MostSkills = 512;

        /// <summary>The longest name kept; the game allows far shorter ones.</summary>
        private const int LongestName = 64;

        private static Record _record = new Record();
        private static string _path;

        /// <summary>For the self-test: where this world's record is written, none where this game does not run the world.</summary>
        internal static string RecordPath => _path;

        /// <summary>Opens the world's record. Called as the world starts.</summary>
        public static void Start(string worldName)
        {
            _path = Path.Combine(Paths.ConfigPath, "Kinsmen", FileName(worldName) + ".record.txt");
            _record = Load(_path);
            if (_record.DropUnseen(Now(), Plugin.ForgetAfterDays) > 0) Save();
            Plugin.Log.LogInfo("Kinsmen record for this world: " + _path);
        }

        /// <summary>This game joined a world it does not run: nothing of an earlier world's record stays.</summary>
        public static void Stop()
        {
            _path = null;
            _record = new Record();
        }

        /// <summary>A game sent its character's real levels: note them and send the record to everyone.</summary>
        public static void OnLevels(long sender, ZPackage message)
        {
            if (_path == null) return;
            try
            {
                var id = message.ReadLong();
                var name = message.ReadString();
                var count = message.ReadInt();
                if (id == 0 || count < 0 || count > MostSkills) return;
                var levels = new Dictionary<int, float>();
                for (var i = 0; i < count; i++)
                {
                    var skill = message.ReadInt();
                    var level = message.ReadSingle();
                    if (float.IsNaN(level) || float.IsInfinity(level)) continue;
                    levels[skill] = Math.Max(0f, Math.Min(1000f, level));
                }
                if (name.Length > LongestName) name = name.Substring(0, LongestName);
                _record.Note(id, name, levels, Now());
                _record.DropUnseen(Now(), Plugin.ForgetAfterDays);
                Save();
                Send();
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("reading a game's levels", e);
            }
        }

        /// <summary>A game asks to take a character out of the record: done for an admin or the host, refused otherwise; the asker is told either way.</summary>
        public static void OnForget(long sender, string name)
        {
            if (_path == null) return;
            string said;
            if (!IsAdmin(sender)) said = "Only an admin can take a character out of the Kinsmen record.";
            else
            {
                var gone = _record.Forget(name);
                said = gone == 0 ? "Nobody called " + name + " is in the Kinsmen record." : name + " is out of the Kinsmen record.";
                if (gone > 0)
                {
                    Save();
                    Send();
                }
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RecordMessages.Said, said);
        }

        /// <summary>The record as it stands, to every game, this one too.</summary>
        private static void Send() => ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RecordMessages.Record, _record.Write());

        private static bool IsAdmin(long sender)
        {
            if (sender == ZDOMan.GetSessionID()) return true;
            var peer = ZNet.instance.GetPeer(sender);
            return peer?.m_socket != null && ZNet.instance.IsAdmin(peer.m_socket.GetHostName());
        }

        /// <summary>
        /// Reads the world's record. A file that cannot be read at all is moved aside rather than
        /// written over, so nothing in it is lost; its lines that can be read are still used.
        /// </summary>
        private static Record Load(string path)
        {
            if (!File.Exists(path)) return new Record();
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Kinsmen could not read its record, starts an empty one and leaves the file alone: " + e.Message);
                _path = path + ".new";
                return new Record();
            }
            var record = Record.Read(text, out var problems);
            foreach (var problem in problems) Plugin.Log.LogWarning("Kinsmen record " + path + ": " + problem);
            if (problems.Count > 0 && record.Entries.Count == 0)
            {
                try
                {
                    File.Move(path, path + ".unreadable");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Kinsmen could not move its unreadable record aside, keeps the new one beside it: " + e.Message);
                    _path = path + ".new";
                }
            }
            return record;
        }

        /// <summary>Writes the record through a file beside it, so a failed write leaves the old one whole.</summary>
        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                var next = _path + ".writing";
                File.WriteAllText(next, _record.Write());
                if (File.Exists(_path)) File.Replace(next, _path, null);
                else File.Move(next, _path);
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("writing the record", e);
            }
        }

        private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>A world's name as a file name: characters a file name cannot hold become underscores.</summary>
        private static string FileName(string worldName)
        {
            var name = string.IsNullOrEmpty(worldName) ? "world" : worldName;
            foreach (var bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, '_');
            return name;
        }
    }
}
