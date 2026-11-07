using System.Collections.Generic;

namespace Kinsmen.Standards
{
    /// <summary>
    /// The files under src/ the unit tests leave out, as tests/Kinsmen.Tests/Kinsmen.Tests.csproj
    /// compiles only what it names, each with why it needs the game. The self-test reaches these in
    /// game.
    /// </summary>
    internal static class GameOnlyFiles
    {
        public static readonly Dictionary<string, string> List = new Dictionary<string, string>
        {
            ["src/Plugin.cs"] = "the plugin class, which BepInEx starts with the game, with its settings and the /kinsmen command",
            ["src/CapHook.cs"] = "the Harmony prefix on the game's SEMan that lowers the copy GetSkillLevel hands it; what it counts as is src/CountingTable.cs",
            ["src/SkillsWindow.cs"] = "marks rows of the game's skills window, Unity objects and TextMeshPro text",
            ["src/HeldEffect.cs"] = "adds a Unity ScriptableObject effect to the list the game's HUD draws; its text is src/Words.cs",
            ["src/RecordMessages.cs"] = "registers the record's messages with the game's routed RPCs as a world starts",
            ["src/RecordServer.cs"] = "the record's file under BepInEx's config folder and the game's network; the record itself is src/Record.cs",
            ["src/RecordClient.cs"] = "reads the local character's skills and sends them over the game's network; the table it builds is src/CountingTable.cs",
            ["src/KinsmenApi.cs"] = "answers other mods from the table the running game built",
            ["src/UnityNull.cs"] = "Unity's own check for an object that is gone, which needs UnityEngine",
            ["src/ServerSync/**"] = "blaxxun's ServerSync, compiled in unchanged, which syncs settings over the game's network",
        };
    }
}
