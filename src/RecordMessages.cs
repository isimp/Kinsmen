using System;
using HarmonyLib;

namespace Kinsmen
{
    /// <summary>
    /// The record's messages over the game's routed RPCs, registered as each world starts
    /// (ZNetScene.Awake runs once a world is loaded, on a server and in every game). The server's
    /// side only answers where this side runs the world.
    /// </summary>
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class RecordMessages
    {
        /// <summary>Game to server: its character's id, name and real level in each listed skill it has trained.</summary>
        public const string Levels = "Kinsmen_Levels";

        /// <summary>Server to every game: the whole record as text.</summary>
        public const string Record = "Kinsmen_Record";

        /// <summary>Game to server: take a character of this name out of the record.</summary>
        public const string Forget = "Kinsmen_Forget";

        /// <summary>Server to one game: the answer to a forget.</summary>
        public const string Said = "Kinsmen_Said";

        private static void Postfix()
        {
            try
            {
                var rpc = ZRoutedRpc.instance;
                if (rpc == null || ZNet.instance == null) return;
                var server = ZNet.instance.IsServer();
                rpc.Register<ZPackage>(Levels, (sender, message) =>
                {
                    if (server) RecordServer.OnLevels(sender, message);
                });
                rpc.Register<string>(Forget, (sender, name) =>
                {
                    if (server) RecordServer.OnForget(sender, name);
                });
                rpc.Register<string>(Record, RecordClient.OnRecord);
                rpc.Register<string>(Said, RecordClient.OnSaid);
                RecordClient.Joined();
                if (server) RecordServer.Start(ZNet.instance.GetWorldName());
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Kinsmen could not set up its record for this world and holds no skill in it: " + e);
            }
        }
    }
}
