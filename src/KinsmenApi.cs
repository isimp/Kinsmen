namespace Kinsmen
{
    /// <summary>
    /// For other mods that show the character's skills by reading the saved level themselves: what
    /// a skill counts as on this server, and why. Reach it by reflection to keep Kinsmen optional:
    /// type "Kinsmen.KinsmenApi" in the assembly "Kinsmen", method "TryCounted".
    /// </summary>
    public static class KinsmenApi
    {
        /// <summary>Raised when what this class answers changes shape.</summary>
        public const int Version = 1;

        /// <summary>
        /// What a saved level of one of the local character's skills counts as here, before effects
        /// add, and a line saying why. False, with the level unchanged and no reason, when the
        /// skill counts in full.
        /// </summary>
        public static bool TryCounted(int skill, float savedLevel, out float counted, out string why)
        {
            counted = savedLevel;
            why = null;
            var table = RecordClient.Table;
            var ceiling = table.Why(skill, savedLevel);
            if (ceiling == null) return false;
            counted = table.Lower(skill, savedLevel);
            why = Words.Tooltip(RecordClient.Name((Skills.SkillType)skill), RecordClient.Whole(counted), ceiling.Holder,
                RecordClient.Whole(ceiling.HolderLevel), RecordClient.Whole(ceiling.Limit), Plugin.Settings.Rate);
            return true;
        }

        /// <summary>
        /// What a saved level of one of the local character's skills counts as here, before effects
        /// add, and whose level holds it there: the best of the group below the gap and that
        /// character's level, or no name and the cap itself where the server set the cap by hand.
        /// False, with the level unchanged and no holder, when the skill counts in full. Builds no
        /// text, so a mod can ask it for every skill each time it reads them.
        /// </summary>
        public static bool TryHolder(int skill, float savedLevel, out float counted, out string holder, out float holderLevel)
        {
            counted = savedLevel;
            holder = null;
            holderLevel = 0f;
            var table = RecordClient.Table;
            var ceiling = table.Why(skill, savedLevel);
            if (ceiling == null) return false;
            counted = table.Lower(skill, savedLevel);
            holder = ceiling.Holder;
            holderLevel = ceiling.Holder == null ? ceiling.Limit : ceiling.HolderLevel;
            return true;
        }
    }
}
