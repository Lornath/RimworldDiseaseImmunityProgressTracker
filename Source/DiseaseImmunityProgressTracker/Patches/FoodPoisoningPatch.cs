using HarmonyLib;
using RimWorld;
using Verse;
using DiseaseImmunityProgressTracker.Core;
using DiseaseImmunityProgressTracker.UI;

namespace DiseaseImmunityProgressTracker.Patches
{
    /// <summary>
    /// Harmony patch to detect when the tooltip for Food Poisoning is being displayed.
    /// Opens a companion window showing countdown timer, staged progress, and symptoms.
    ///
    /// Food Poisoning (Type 7) only has HediffComp_SeverityPerDay:
    /// - Severity starts at 1.0 and decreases at -1/day
    /// - No immunity, no tending, no disappears timer
    /// - Three stages: Initial (sev >= 0.80), Major (sev >= 0.20), Recovering (sev &lt; 0.20)
    ///
    /// We patch CompTipStringExtra on HediffComp_SeverityPerDay. This comp is used by many hediffs,
    /// so we filter strictly by DiseaseTracker.IsFoodPoisoning().
    /// </summary>
    [HarmonyPatch(typeof(HediffComp_SeverityPerDay), nameof(HediffComp_SeverityPerDay.CompTipStringExtra), MethodType.Getter)]
    public static class FoodPoisoningPatch
    {
        /// <summary>
        /// Called after CompTipStringExtra is accessed (when tooltip is being rendered).
        /// Only handles Food Poisoning - other SeverityPerDay hediffs are ignored.
        /// </summary>
        public static void Postfix(HediffComp_SeverityPerDay __instance)
        {
            if (__instance?.parent == null) return;

            var hediff = __instance.parent;

            // Only handle Food Poisoning
            if (!DiseaseTracker.IsFoodPoisoning(hediff)) return;

            var pawn = __instance.Pawn;
            if (pawn == null || pawn.Dead) return;

            // Disable when Numbers mod window is open - it calls CompTipStringExtra during
            // table rendering which interferes with our tooltip detection
            if (ModCompatibility.IsNumbersWindowOpen()) return;

            // Register this hediff's tooltip as active (for multi-disease support)
            CompanionWindowManager.RegisterTooltipActive(hediff);

            // Open a new window if one isn't already open for this hediff
            if (!FoodPoisoningWindow.IsOpenFor(hediff))
            {
                Find.WindowStack.Add(new FoodPoisoningWindow(hediff));

                if (DiseaseImmunityProgressTrackerMod.Settings.verboseLogging)
                {
                    Log.Message($"[DiseaseImmunityProgressTracker] Opened food poisoning window for {hediff.Label}");
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
