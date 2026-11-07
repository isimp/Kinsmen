using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;

namespace Kinsmen
{
    /// <summary>
    /// Kinsmen. A character far ahead of the rest of the group in a skill that matters in a fight
    /// counts on this server as a few levels above the best of the rest, and each level beyond that
    /// by a share. Nothing a character keeps is changed: the game reads a skill's level through
    /// Skills.GetSkillLevel, which copies the saved level, lets effects add to the copy and floors
    /// it; Kinsmen lowers that copy before effects add (CapHook) and leaves the saved level, gains
    /// and losses to the game.
    ///
    /// Skills live only in each player's character file, so the server keeps a record of everyone's
    /// real levels, which each game sends it (RecordClient), and hands the record to every game
    /// (RecordServer). Each game then works out its own character's counted levels by the same rule
    /// (CountingTable). The lowering runs in the held player's own game; the server refuses a game
    /// without Kinsmen, which is as far as a server can see into a client.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInProcess("valheim.exe")]
    [BepInProcess("valheim_server.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "isimp.Kinsmen";
        public const string Name = "Kinsmen";
        public const string Version = "0.1.0";

        /// <summary>
        /// The oldest Kinsmen a peer may run and still be let in. Raise it only when a release
        /// changes what the games must agree on (the record's messages and text, the rule), not
        /// merely because the version moved: ServerSync demands each side be at least the other's
        /// minimum.
        /// </summary>
        private const string MinimumVersion = "0.1.0";

        internal static ManualLogSource Log;

        /// <summary>Kept for the session: Harmony's patches live as long as the game.</summary>
        internal static Harmony Harmony;

        private static ConfigSync _sync;
        private static ConfigEntry<bool> _lockConfig;
        private static ConfigEntry<bool> _enabled;
        private static ConfigEntry<string> _skills;
        private static ConfigEntry<float> _gap;
        private static ConfigEntry<float> _rate;
        private static ConfigEntry<int> _forgetAfterDays;
        private static ConfigEntry<string> _fixedCaps;
        private static ConfigEntry<string> _exempt;
        private static ConfigEntry<bool> _showEffectIcon;
        private static ConfigEntry<bool> _showJoinMessage;

        private static readonly HashSet<string> Warned = new HashSet<string>(StringComparer.Ordinal);

        internal static bool Enabled => _enabled == null || _enabled.Value;
        internal static string SkillNames => _skills?.Value ?? SkillList.Default;
        internal static int ForgetAfterDays => _forgetAfterDays?.Value ?? 30;
        internal static string FixedCaps => _fixedCaps?.Value ?? "";
        internal static string ExemptNames => _exempt?.Value ?? "";
        internal static bool ShowEffectIcon => _showEffectIcon == null || _showEffectIcon.Value;
        internal static bool ShowJoinMessage => _showJoinMessage == null || _showJoinMessage.Value;

        internal static LadderSettings Settings =>
            new LadderSettings(_gap?.Value ?? 15f, _rate?.Value ?? 0.25f);

        /// <summary>A setting that is a rule of the server: ServerSync hands the server's value to every game, and a game cannot change it while the lock holds.</summary>
        private ConfigEntry<T> Rule<T>(string group, string name, T value, ConfigDescription description)
        {
            var entry = Config.Bind(group, name, value, description);
            _sync.AddConfigEntry(entry).SynchronizedConfig = true;
            entry.SettingChanged += (_, __) => RecordClient.Rebuild();
            return entry;
        }

        private void Awake()
        {
            Log = Logger;

            // Required: the lowering runs in each player's own game, so a game without Kinsmen
            // would play at its full levels whatever the server's rules say.
            _sync = new ConfigSync(Guid)
            {
                DisplayName = Name,
                CurrentVersion = Info.Metadata.Version.ToString(),
                MinimumRequiredVersion = MinimumVersion,
                ModRequired = true,
            };

            _lockConfig = Config.Bind("General", "LockConfiguration", true,
                "Whether the server's rules override local settings. Admins are exempt. The display settings always stay local.");
            _sync.AddLockingConfigEntry(_lockConfig);

            _enabled = Rule("General", "Enabled", true, new ConfigDescription(
                "Whether held skills count lower. Off, every skill counts in full and the record is still kept."));

            _skills = Rule("Rule", "Skills", SkillList.Default, new ConfigDescription(
                "The skills that can be held, separated by commas, by the game's own names as in the default value, such as ElementalMagic, " +
                "Unarmed and Ride (not the window's Fists and Riding); case and spaces do not matter. A number stands for a skill another " +
                "mod adds, by its id. A name Kinsmen does not know is named in the log and left free."));

            _gap = Rule("Rule", "OutlierGap", 15f, new ConfigDescription(
                "Walking up everyone's levels in a skill, the first step up wider than this splits off the characters far ahead; " +
                "everyone below it counts in full. A character far ahead counts in full up to the best level below the step plus " +
                "this many levels, and RateAbove for each level beyond, so one more level of training never lowers what a skill counts as. " +
                "Smaller holds characters far ahead tighter, but also catches a normal step between two players sooner.",
                new AcceptableValueRange<float>(1f, 100f)));

            _rate = Rule("Rule", "RateAbove", 0.25f, new ConfigDescription(
                "What each level beyond that counts as: 0.25 is a quarter of a level, 0 holds the character there, 1 holds nobody.",
                new AcceptableValueRange<float>(0f, 1f)));

            _fixedCaps = Rule("Rule", "FixedCaps", "", new ConfigDescription(
                "Caps set by hand, replacing the group's cap for their skill, as Skill=level separated by commas, such as " +
                "\"Swords=50, ElementalMagic=30\", with skill names as in Skills. Anyone above such a cap counts in full up to it and " +
                "RateAbove beyond, whatever the group's levels; it holds a skill missing from Skills too. An entry Kinsmen cannot read " +
                "is named in the log and left out. Empty by default."));

            _exempt = Rule("Rule", "Exempt", "", new ConfigDescription(
                "Characters whose skills always count in full, by name (any case) or by id for two of the same name, separated by commas. " +
                "Their real levels still go into the record and still count for everyone else's limits. Empty by default."));

            _forgetAfterDays = Rule("Record", "ForgetAfterDays", 30, new ConfigDescription(
                "A character not seen on the server for this many days drops out of the record. 0 keeps everyone.",
                new AcceptableValueRange<int>(0, 3650)));

            _showEffectIcon = Config.Bind("Display", "ShowEffectIcon", true,
                "Show an effect icon beside the others while any of your skills counts lower here.");

            _showJoinMessage = Config.Bind("Display", "ShowJoinMessage", true,
                "On joining, say in the middle of the screen and in chat which of your skills count lower here.");

            _showEffectIcon.SettingChanged += (_, __) => HeldEffect.Refresh();

            Harmony = new Harmony(Guid);
            try
            {
                Harmony.PatchAll(typeof(CapHook));
                Harmony.PatchAll(typeof(SkillsWindow));
                Harmony.PatchAll(typeof(HeldEffect));
                Harmony.PatchAll(typeof(RecordMessages));
            }
            catch (Exception e)
            {
                Harmony.UnpatchSelf();
                Log.LogError("Kinsmen could not hook into the game and holds no skill this session: " + e);
                return;
            }

            RegisterCommand();
            Log.LogInfo(Name + " " + Version + " loaded.");
        }

        private void Update()
        {
            RecordClient.Tick();
        }

        /// <summary>Tells a failure once for its place, however often it happens: code that runs every frame would flood the log.</summary>
        internal static void WarnOnce(string place, Exception failure)
        {
            if (!Warned.Add(place)) return;
            Log.LogWarning("Kinsmen, " + place + ": " + failure);
        }

        /// <summary>Words that may follow /kinsmen, each with what it runs. The self-test adds its word here.</summary>
        internal static readonly Dictionary<string, Action<Terminal.ConsoleEventArgs>> CommandWords =
            new Dictionary<string, Action<Terminal.ConsoleEventArgs>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Plain /kinsmen, or a word it does not know, lists the words that can follow it (Words.Help):
        /// /kinsmen held lists which of your skills count lower here and why; /kinsmen caps lists
        /// every capped skill's cap as it stands; /kinsmen forget Name takes a character out of the
        /// server's record (admins); /kinsmen record lists the record; /kinsmen selftest where the
        /// self-test is installed. Registered with isCheat false, which makes it reachable from chat too.
        /// </summary>
        private static void RegisterCommand()
        {
            CommandWords["forget"] = args =>
            {
                var name = args.Length >= 3 ? string.Join(" ", args.Args, 2, args.Args.Length - 2) : "";
                if (name.Length == 0)
                {
                    args.Context.OrNull()?.AddString("Name the character to forget: /kinsmen forget Name");
                    return;
                }
                RecordClient.AskToForget(name);
            };
            CommandWords["record"] = args => args.Context.OrNull()?.AddString(RecordClient.DescribeRecord());
            CommandWords["caps"] = args => args.Context.OrNull()?.AddString(RecordClient.DescribeCaps());
            CommandWords["held"] = args => args.Context.OrNull()?.AddString(RecordClient.DescribeHeld());

            _ = new Terminal.ConsoleCommand("kinsmen", "Kinsmen: type /kinsmen for its commands.", args =>
            {
                if (args.Length >= 2 && CommandWords.TryGetValue(args[1], out var word))
                {
                    word(args);
                    return;
                }
                var unknown = args.Length >= 2 ? args[1] : null;
                args.Context.OrNull()?.AddString(Words.Help(CommandWords.ContainsKey("selftest"), unknown));
            });
        }
    }
}
