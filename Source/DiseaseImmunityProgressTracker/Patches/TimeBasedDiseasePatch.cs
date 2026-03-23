using HarmonyLib;
using Verse;
using DiseaseImmunityProgressTracker.Core;
using DiseaseImmunityProgressTracker.UI;

namespace DiseaseImmunityProgressTracker.Patches
{
    /// <summary>
    /// Harmony patch to detect when the tooltip for a Type 3 (time-based) disease is being displayed.
    /// Opens a companion window showing time remaining and severity management.
    ///
    /// Handles both subtypes:
    /// - Type 3a (Mechanites): Fibrous/Sensory Mechanites - non-fatal, severity controls pain
    /// - Type 3b (Fatal Rots): Lung Rot, Blood Rot - potentially fatal, severity can kill
    ///
    /// Patches HediffComp_TendDuration.CompTipStringExtra since time-based diseases use this
    /// component for treatment (HediffComp_Disappears doesn't have CompTipStringExtra).
    /// </summary>
    [HarmonyPatch(typeof(HediffComp_TendDuration), nameof(HediffComp_TendDuration.CompTipStringExtra), MethodType.Getter)]
    public static class TimeBasedDiseasePatch
    {
        /// <summary>
        /// Called after CompTipStringExtra is accessed (when tooltip is being rendered).
        /// This runs after CumulativeTendPatch, so we check if that patch already handled it.
        /// </summary>
        public static void Postfix(HediffComp_TendDuration __instance)
        {
            if (__instance?.parent == null) return;

            var hediff = __instance.parent;

            // Skip Type 2 (cumulative tend) diseases - handled by CumulativeTendPatch
            if (__instance.TProps.disappearsAtTotalTendQuality >= 0) return;

            // Skip if this is not a Type 3 (time-based) disease
            // Note: IsTimeBasedDisease includes both Type 3a (Mechanites) and Type 3b (Fatal Rots)
            if (!DiseaseTracker.IsTimeBasedDisease(hediff)) return;

            // Skip Type 1 (true immunizable) diseases - handled by TooltipCompanionPatch
            // Exception: Type 3a (Mechanites) have Immunizable but should use TimeBasedWindow
            var immunizable = hediff.TryGetComp<HediffComp_Immunizable>();
            if (immunizable != null && !DiseaseTracker.IsMechaniteDisease(hediff)) return;

            // Get the HediffComp_Disappears for the timer
            var disappearsComp = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappearsComp == null) return;

            var pawn = hediff.pawn;
            if (pawn == null || pawn.Dead) return;

            // Only proceed if we're in a vanilla tooltip context (Hediff.GetTooltip call).
            // Mods like Moody and Numbers call CompTipStringExtra outside of tooltip rendering,
            // which would cause false-positive window openings.
            if (!TooltipContextPatch.IsInVanillaTooltipContext) return;

            // Register this hediff's tooltip as active (for multi-disease support)
            CompanionWindowManager.RegisterTooltipActive(hediff);

            // Open a new window if one isn't already open for this hediff
            if (!TimeBasedWindow.IsOpenFor(hediff))
            {
                Find.WindowStack.Add(new TimeBasedWindow(hediff, disappearsComp));

                if (DiseaseImmunityProgressTrackerMod.Settings.verboseLogging)
                {
                    Log.Message($"[DiseaseImmunityProgressTracker] Opened time-based disease window for {hediff.Label}");
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
