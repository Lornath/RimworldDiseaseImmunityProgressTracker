using HarmonyLib;
using RimWorld;
using Verse;
using DiseaseImmunityProgressTracker.Core;
using DiseaseImmunityProgressTracker.UI;

namespace DiseaseImmunityProgressTracker.Patches
{
    /// <summary>
    /// Harmony patch to detect when the tooltip for a cumulative tend disease is being displayed.
    /// Opens a companion window showing tend progress for diseases like Gut Worms and Muscle Parasites.
    /// </summary>
    [HarmonyPatch(typeof(HediffComp_TendDuration), nameof(HediffComp_TendDuration.CompTipStringExtra), MethodType.Getter)]
    public static class CumulativeTendPatch
    {
        /// <summary>
        /// Called after CompTipStringExtra is accessed (when tooltip is being rendered).
        /// </summary>
        public static void Postfix(HediffComp_TendDuration __instance)
        {
            if (__instance?.parent == null) return;

            // Skip if this is not a cumulative tend disease
            if (__instance.TProps.disappearsAtTotalTendQuality < 0) return;

            // Skip if this hediff also has HediffComp_Immunizable (handled by TooltipCompanionPatch)
            var immunizable = __instance.parent.TryGetComp<HediffComp_Immunizable>();
            if (immunizable != null) return;

            var hediff = __instance.parent;
            var pawn = __instance.Pawn;
            if (pawn == null || pawn.Dead) return;

            // Disable when Numbers mod window is open - it calls CompTipStringExtra during
            // table rendering which interferes with our tooltip detection
            if (ModCompatibility.IsNumbersWindowOpen()) return;

            // Register this hediff's tooltip as active (for multi-disease support)
            CompanionWindowManager.RegisterTooltipActive(hediff);

            // Open a new window if one isn't already open for this hediff
            if (!CumulativeTendWindow.IsOpenFor(hediff))
            {
                Find.WindowStack.Add(new CumulativeTendWindow(hediff, __instance));

                if (DiseaseImmunityProgressTrackerMod.Settings.verboseLogging)
                {
                    Log.Message($"[DiseaseImmunityProgressTracker] Opened cumulative tend window for {hediff.Label}");
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
