using Verse;

namespace RecoveryProcessTracker.Core
{
    /// <summary>
    /// Helper class for mod compatibility checks.
    /// </summary>
    public static class ModCompatibility
    {
        /// <summary>
        /// Check if the Numbers mod window is currently open.
        /// Numbers calls CompTipStringExtra during table cell rendering (not just tooltips),
        /// which interferes with our tooltip detection. We disable our windows when Numbers is open.
        /// </summary>
        public static bool IsNumbersWindowOpen()
        {
            if (Find.WindowStack == null) return false;

            foreach (var window in Find.WindowStack.Windows)
            {
                // Check if window is from the Numbers mod by namespace
                var typeName = window.GetType().FullName;
                if (typeName != null && typeName.StartsWith("Numbers."))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
