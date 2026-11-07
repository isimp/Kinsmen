using System.Collections;
using BepInEx;
using HarmonyLib;

namespace Kinsmen.SelfTest
{
    /// <summary>
    /// The self-test as a plugin of its own beside Kinsmen, in three commands:
    /// /kinsmen selftest reads the character and goes through the server, and changes nothing the
    /// character keeps (ReadingTests, ServerTests); /kinsmen selftest progression raises and lowers
    /// real skills, on a character made for it only (Throwaway); /kinsmen selftest watch holds the
    /// character's skills in this game while the player plays as usual and checks every rise the
    /// game makes, until typed again (Watch).
    /// </summary>
    [BepInPlugin(Guid, "Kinsmen self-test", Plugin.Version)]
    [BepInDependency(Plugin.Guid)]
    [BepInProcess("valheim.exe")]
    public sealed class SelfTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "isimp.Kinsmen.SelfTest";

        private bool _running;

        /// <summary>Kept for the session: the watch's patch lives as long as the game.</summary>
        private static Harmony _harmony;

        private void Awake()
        {
            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Watch));
            Plugin.CommandWords["selftest"] = args =>
            {
                if (Player.m_localPlayer == null)
                {
                    args.Context.OrNull()?.AddString("The Kinsmen self-test runs in a world, with your character in it.");
                    return;
                }
                var word = args.Length >= 3 ? args[2].ToUpperInvariant() : "";
                if (word == "WATCH")
                {
                    Watch.Toggle(Logger);
                    return;
                }
                if (_running)
                {
                    args.Context.OrNull()?.AddString("A Kinsmen self-test is already running.");
                    return;
                }
                StartCoroutine(word == "PROGRESSION" ? Progression() : Reading());
            };
        }

        private IEnumerator Reading()
        {
            _running = true;
            try
            {
                var report = new Report(Logger, "Kinsmen self-test");
                report.Guarded("the reading test", () => ReadingTests.Run(report));
                yield return ServerTests.Run(report);
                report.Summary();
            }
            finally
            {
                _running = false;
            }
        }

        private IEnumerator Progression()
        {
            _running = true;
            try
            {
                var report = new Report(Logger, "Kinsmen progression test");
                yield return Throwaway.Run(report);
                report.Summary();
            }
            finally
            {
                _running = false;
            }
        }
    }
}
