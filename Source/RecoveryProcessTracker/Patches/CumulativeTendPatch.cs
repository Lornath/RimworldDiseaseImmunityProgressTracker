using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using RecoveryProcessTracker.UI;

namespace RecoveryProcessTracker.Patches
{
    /// <summary>
    /// Harmony patch to detect when the tooltip for a cumulative tend disease is being displayed.
    /// Opens a companion window showing tend progress for diseases like Gut Worms and Muscle Parasites.
    /// </summary>
    [HarmonyPatch(typeof(HediffComp_TendDuration), nameof(HediffComp_TendDuration.CompTipStringExtra), MethodType.Getter)]
    public static class CumulativeTendPatch
    {
        // Track which hediff's tooltip is currently active
        private static Hediff activeHediff;
        private static int lastActiveFrame;

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

            // Update the active hediff tracker using Unity frame count
            activeHediff = hediff;
            lastActiveFrame = Time.frameCount;

            // Close any windows for other hediffs
            CumulativeTendWindow.CloseOtherWindows(hediff);

            // Open a new window if one isn't already open for this hediff
            if (!CumulativeTendWindow.IsOpenFor(hediff))
            {
                Find.WindowStack.Add(new CumulativeTendWindow(hediff, __instance));

                if (RecoveryProcessTrackerMod.Settings.verboseLogging)
                {
                    Log.Message($"[RecoveryProcessTracker] Opened cumulative tend window for {hediff.Label}");
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
