using BepInEx;

namespace Kinsmen.SelfTest
{
    /// <summary>
    /// The self-test as a plugin of its own beside Kinsmen. Typing /kinsmen selftest in chat runs
    /// it on the player's own character, which it reads and never changes.
    /// </summary>
    [BepInPlugin(Guid, "Kinsmen self-test", Plugin.Version)]
    [BepInDependency(Plugin.Guid)]
    [BepInProcess("valheim.exe")]
    public sealed class SelfTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "isimp.Kinsmen.SelfTest";

        private void Awake()
        {
            Plugin.CommandWords["selftest"] = args =>
            {
                if (Player.m_localPlayer == null)
                {
                    args.Context.OrNull()?.AddString("The Kinsmen self-test runs in a world, with your character in it.");
                    return;
                }
                new Tests(Logger).Run();
            };
        }
    }
}
