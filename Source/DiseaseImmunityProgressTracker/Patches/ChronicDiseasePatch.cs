using HarmonyLib;
using RimWorld;
using Verse;
using DiseaseImmunityProgressTracker.Core;
using DiseaseImmunityProgressTracker.UI;

namespace DiseaseImmunityProgressTracker.Patches
{
    /// <summary>
    /// Harmony patch to detect when the tooltip for a Type 5 (chronic) disease like Asthma is being displayed.
    /// Opens a companion window showing treatment status and severity progression.
    ///
    /// Patches HediffComp_Immunizable.CompTipStringExtra since chronic diseases have this component
    /// (used for severity changes, not immunity gain).
    /// </summary>
    [HarmonyPatch(typeof(HediffComp_Immunizable), nameof(HediffComp_Immunizable.CompTipStringExtra), MethodType.Getter)]
    public static class ChronicDiseasePatch
    {
        /// <summary>
        /// Called after CompTipStringExtra is accessed (when tooltip is being rendered).
        /// </summary>
        public static void Postfix(HediffComp_Immunizable __instance)
        {
            if (__instance?.parent == null) return;

            var hediff = __instance.parent;
            var pawn = __instance.Pawn;
            if (pawn == null || pawn.Dead) return;

            // Only handle Type 5 (Chronic) diseases like Asthma
            if (!DiseaseTracker.IsChronicDisease(hediff)) return;

            // Disable when Numbers mod window is open
            if (ModCompatibility.IsNumbersWindowOpen()) return;

            // Register this hediff's tooltip as active
            CompanionWindowManager.RegisterTooltipActive(hediff);

            // Open a new window if one isn't already open for this hediff
            bool alreadyOpen = ChronicDiseaseWindow.IsOpenFor(hediff);

            if (!alreadyOpen)
            {
                var newWindow = new ChronicDiseaseWindow(hediff);
                Find.WindowStack.Add(newWindow);

                if (DiseaseImmunityProgressTrackerMod.Settings.verboseLogging)
                {
                    bool inStack = Find.WindowStack.Windows.Contains(newWindow);
                    var allWindows = CompanionWindowManager.GetOpenCompanionWindows();
                    Log.Message($"[DiseaseImmunityProgressTracker] Opened chronic disease window for {hediff.Label}, inStack={inStack}, position=({newWindow.windowRect.x:F0},{newWindow.windowRect.y:F0}), companionWindowCount={allWindows.Count}");
                }
            }
        }

        /// <summary>
        /// Check if the tooltip is currently active for the given hediff.
        /// Delegates to the central CompanionWindowManager.
        /// </summary>
        public static bool IsTooltipActiveFor(Hediff hediff)
        {
            return CompanionWindowManager.IsTooltipActiveFor(hediff);
        }
    }
}
