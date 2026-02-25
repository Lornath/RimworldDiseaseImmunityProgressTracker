using HarmonyLib;
using RimWorld;
using Verse;
using DiseaseImmunityProgressTracker.Core;
using DiseaseImmunityProgressTracker.UI;

namespace DiseaseImmunityProgressTracker.Patches
{
    /// <summary>
    /// Harmony patch to detect when the tooltip for a Type 1 (immunizable) disease is being displayed.
    /// Opens a companion graph window showing disease progression vs immunity gain.
    /// Skips Type 3a (Mechanites) which have Immunizable but should use TimeBasedWindow instead.
    /// </summary>
    [HarmonyPatch(typeof(HediffComp_Immunizable), nameof(HediffComp_Immunizable.CompTipStringExtra), MethodType.Getter)]
    public static class TooltipCompanionPatch
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

            // Skip Type 4 (Toxic Buildup) - handled by ToxicBuildupPatch
            if (DiseaseTracker.IsToxicBuildupDisease(hediff)) return;

            // Skip Type 6 (Artery Blockage) - handled by ArteryBlockagePatch
            if (DiseaseTracker.IsArteryBlockage(hediff)) return;

            // Skip Type 5 (Chronic diseases like Asthma) - handled by ChronicDiseasePatch
            if (DiseaseTracker.IsChronicDisease(hediff)) return;

            // Skip Type 3a (Mechanites) - they have Immunizable but should use TimeBasedWindow
            // (handled by TimeBasedDiseasePatch instead)
            if (DiseaseTracker.IsMechaniteDisease(hediff)) return;

            // Disable when Numbers mod window is open - it calls CompTipStringExtra during
            // table rendering which interferes with our tooltip detection
            if (ModCompatibility.IsNumbersWindowOpen()) return;

            // Register this hediff's tooltip as active (for multi-disease support)
            CompanionWindowManager.RegisterTooltipActive(hediff);

            // Open a new graph window if one isn't already open for this hediff
            bool alreadyOpen = DiseaseGraphWindow.IsOpenFor(hediff);

            if (!alreadyOpen)
            {
                var newWindow = new DiseaseGraphWindow(hediff);
                Find.WindowStack.Add(newWindow);

                if (DiseaseImmunityProgressTrackerMod.Settings.verboseLogging)
                {
                    // Check if it's immediately visible in the stack
                    bool inStack = Find.WindowStack.Windows.Contains(newWindow);
                    var allWindows = CompanionWindowManager.GetOpenCompanionWindows();
                    Log.Message($"[DiseaseImmunityProgressTracker] Opened graph window for {hediff.Label}, inStack={inStack}, position=({newWindow.windowRect.x:F0},{newWindow.windowRect.y:F0}), companionWindowCount={allWindows.Count}");
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
