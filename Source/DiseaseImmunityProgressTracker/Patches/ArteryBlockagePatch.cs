using HarmonyLib;
using RimWorld;
using Verse;
using DiseaseImmunityProgressTracker.Core;
using DiseaseImmunityProgressTracker.UI;

namespace DiseaseImmunityProgressTracker.Patches
{
    /// <summary>
    /// Harmony patch to detect when the tooltip for Artery Blockage is being displayed.
    /// Opens a companion window showing severity, progression, heart attack risk, and cure options.
    ///
    /// Artery Blockage (Type 6) has HediffComp_Immunizable which extends HediffComp_Immunizable,
    /// but it has unique mechanics:
    /// - No immunity gain (just slow severity progression)
    /// - Very slow progression (0.0007/day with 0.5-3x random factor)
    /// - NOT tendable (cannot be treated medically)
    /// - Lethal at 100% severity
    /// - Causes heart attacks with escalating MTB at each stage
    /// - Only curable by: heart replacement, healer serum, luciferium, biosculpter
    /// </summary>
    [HarmonyPatch(typeof(HediffComp_Immunizable), nameof(HediffComp_Immunizable.CompTipStringExtra), MethodType.Getter)]
    public static class ArteryBlockagePatch
    {
        /// <summary>
        /// Called after CompTipStringExtra is accessed (when tooltip is being rendered).
        /// Only handles Artery Blockage - other immunizable diseases are handled by other patches.
        /// </summary>
        public static void Postfix(HediffComp_Immunizable __instance)
        {
            if (__instance?.parent == null) return;

            var hediff = __instance.parent;

            // Only handle Artery Blockage
            if (!DiseaseTracker.IsArteryBlockage(hediff)) return;

            var pawn = __instance.Pawn;
            if (pawn == null || pawn.Dead) return;

            // Only proceed if we're in a vanilla tooltip context (Hediff.GetTooltip call).
            // Mods like Moody and Numbers call CompTipStringExtra outside of tooltip rendering,
            // which would cause false-positive window openings.
            if (!TooltipContextPatch.IsInVanillaTooltipContext) return;

            // Register this hediff's tooltip as active (for multi-disease support)
            CompanionWindowManager.RegisterTooltipActive(hediff);

            // Open a new window if one isn't already open for this hediff
            if (!ArteryBlockageWindow.IsOpenFor(hediff))
            {
                Find.WindowStack.Add(new ArteryBlockageWindow(hediff));

                if (DiseaseImmunityProgressTrackerMod.Settings.verboseLogging)
                {
                    Log.Message($"[DiseaseImmunityProgressTracker] Opened artery blockage window for {hediff.Label}");
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
