using HarmonyLib;
using Verse;
using DiseaseImmunityProgressTracker.Core;
using DiseaseImmunityProgressTracker.UI;

namespace DiseaseImmunityProgressTracker.Patches
{
    /// <summary>
    /// Harmony patch to detect when the tooltip for Toxic Buildup is being displayed.
    /// Opens a companion window showing severity, exposure status, and health risks.
    ///
    /// Toxic Buildup (Type 4) has HediffComp_ImmunizableToxic which extends HediffComp_Immunizable,
    /// but it has unique mechanics:
    /// - No immunity gain (no immunity racing)
    /// - Recovery depends on avoiding toxic exposure
    /// - NOT tendable (environmental condition, not medical)
    /// - Lethal at 100% severity
    /// - Causes dementia/carcinoma risks at higher severity stages
    /// </summary>
    [HarmonyPatch(typeof(HediffComp_Immunizable), nameof(HediffComp_Immunizable.CompTipStringExtra), MethodType.Getter)]
    public static class ToxicBuildupPatch
    {
        /// <summary>
        /// Called after CompTipStringExtra is accessed (when tooltip is being rendered).
        /// Only handles Toxic Buildup - other immunizable diseases are handled by TooltipCompanionPatch.
        /// </summary>
        public static void Postfix(HediffComp_Immunizable __instance)
        {
            if (__instance?.parent == null) return;

            var hediff = __instance.parent;

            // Only handle Type 4 (Toxic Buildup) diseases
            if (!DiseaseTracker.IsToxicBuildupDisease(hediff)) return;

            var pawn = __instance.Pawn;
            if (pawn == null || pawn.Dead) return;

            // Disable when Numbers mod window is open - it calls CompTipStringExtra during
            // table rendering which interferes with our tooltip detection
            if (ModCompatibility.IsNumbersWindowOpen()) return;

            // Register this hediff's tooltip as active (for multi-disease support)
            CompanionWindowManager.RegisterTooltipActive(hediff);

            // Open a new window if one isn't already open for this hediff
            if (!ToxicBuildupWindow.IsOpenFor(hediff))
            {
                Find.WindowStack.Add(new ToxicBuildupWindow(hediff));

                if (DiseaseImmunityProgressTrackerMod.Settings.verboseLogging)
                {
                    Log.Message($"[DiseaseImmunityProgressTracker] Opened toxic buildup window for {hediff.Label}");
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
