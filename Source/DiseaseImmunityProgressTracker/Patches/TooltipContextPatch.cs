using HarmonyLib;
using Verse;

namespace DiseaseImmunityProgressTracker.Patches
{
    /// <summary>
    /// Harmony patch on Hediff.GetTooltip to track when CompTipStringExtra is being called
    /// from a genuine vanilla tooltip context vs from another mod reading TipStringExtra directly.
    ///
    /// In vanilla RimWorld, the health tab tooltip lazily calls Hediff.GetTooltip() which then
    /// accesses TipStringExtra → CompTipStringExtra. Mods like Moody and Numbers call
    /// TipStringExtra directly (bypassing GetTooltip) to build their own UI strings.
    /// By gating our companion window logic on this flag, we avoid false-positive tooltip
    /// detection from those mods.
    /// </summary>
    [HarmonyPatch(typeof(Hediff), nameof(Hediff.GetTooltip))]
    public static class TooltipContextPatch
    {
        /// <summary>
        /// True when we're inside a Hediff.GetTooltip() call (vanilla tooltip rendering context).
        /// Our CompTipStringExtra patches should only open companion windows when this is true.
        /// </summary>
        public static bool IsInVanillaTooltipContext { get; private set; }

        public static void Prefix(ref bool __state)
        {
            __state = IsInVanillaTooltipContext;
            IsInVanillaTooltipContext = true;
        }

        public static void Postfix(bool __state)
        {
            IsInVanillaTooltipContext = __state;
        }
    }
}
