using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using RecoveryProcessTracker.UI;

namespace RecoveryProcessTracker.Patches
{
    /// <summary>
    /// Harmony patch to detect when the tooltip for an immunizable disease is being displayed.
    /// Opens a companion graph window showing disease progression vs immunity gain.
    /// </summary>
    [HarmonyPatch(typeof(HediffComp_Immunizable), nameof(HediffComp_Immunizable.CompTipStringExtra), MethodType.Getter)]
    public static class TooltipCompanionPatch
    {
        // Track which hediff's tooltip is currently active
        private static Hediff activeHediff;
        private static int lastActiveFrame;

        /// <summary>
        /// Called after CompTipStringExtra is accessed (when tooltip is being rendered).
        /// </summary>
        public static void Postfix(HediffComp_Immunizable __instance)
        {
            if (__instance?.parent == null) return;

            var hediff = __instance.parent;
            var pawn = __instance.Pawn;
            if (pawn == null || pawn.Dead) return;

            // Update the active hediff tracker using Unity frame count
            // (works even when game is paused, unlike game ticks)
            activeHediff = hediff;
            lastActiveFrame = Time.frameCount;

            // Close any windows for other hediffs
            DiseaseGraphWindow.CloseOtherWindows(hediff);

            // Open a new graph window if one isn't already open for this hediff
            if (!DiseaseGraphWindow.IsOpenFor(hediff))
            {
                Find.WindowStack.Add(new DiseaseGraphWindow(hediff));

                if (RecoveryProcessTrackerMod.Settings.verboseLogging)
                {
                    Log.Message($"[RecoveryProcessTracker] Opened graph window for {hediff.Label}");
                }
            }
        }

        /// <summary>
        /// Check if the tooltip is currently active for the given hediff.
        /// Returns false if we haven't seen the tooltip update in a few frames.
        /// </summary>
        public static bool IsTooltipActiveFor(Hediff hediff)
        {
            if (hediff == null || activeHediff != hediff) return false;

            // Consider tooltip "stale" if it hasn't been refreshed in 3 frames
            // This happens when mouse moves away from the disease entry
            int currentFrame = Time.frameCount;
            if (currentFrame - lastActiveFrame > 3)
            {
                activeHediff = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Clear the active hediff tracking (for cleanup purposes).
        /// </summary>
        public static void ClearActiveHediff()
        {
            activeHediff = null;
            lastActiveFrame = 0;
        }
    }
}
