using HarmonyLib;
using RimWorld;
using Verse;
using RecoveryProcessTracker.Core;

namespace RecoveryProcessTracker.Patches
{
    /// <summary>
    /// Holds context about the current tending action (doctor, medicine)
    /// so that it can be retrieved when the individual hediffs are tended.
    /// </summary>
    public static class TendingContext
    {
        public static string DoctorName;
        public static string MedicineName;
        public static string MedicineDefName;
        public static int DoctorSkill;
        public static bool Active;

        public static void Begin(Pawn doctor, Medicine medicine)
        {
            DoctorName = doctor?.LabelShort ?? "Self/Unknown";
            MedicineName = medicine?.def?.LabelCap ?? "No medicine";
            MedicineDefName = medicine?.def?.defName;
            DoctorSkill = doctor?.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;
            Active = true;
        }

        public static void End()
        {
            DoctorName = null;
            MedicineName = null;
            MedicineDefName = null;
            DoctorSkill = 0;
            Active = false;
        }
    }

    /// <summary>
    /// Patch TendUtility.DoTend to set up the tending context.
    /// </summary>
    [HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend))]
    public static class TendUtility_DoTend_Patch
    {
        public static void Prefix(Pawn doctor, Pawn patient, Medicine medicine)
        {
            if (patient != null && patient.IsColonist && doctor != null)
            {
                Log.Message($"[RecoveryProcessTracker] TendUtility.DoTend Prefix called. Doctor: {doctor}, Patient: {patient}");
            }
            TendingContext.Begin(doctor, medicine);
        }

        public static void Postfix(Pawn patient)
        {
            // if (patient != null && patient.IsColonist) Log.Message($"[RecoveryProcessTracker] TendUtility.DoTend Postfix called.");
            TendingContext.End();
        }
    }

    /// <summary>
    /// Patch Hediff.Tended to record the event if we are in a tending context.
    /// </summary>
    [HarmonyPatch(typeof(HediffWithComps), nameof(HediffWithComps.Tended))]
    public static class Hediff_Tended_Patch
    {
        public static void Postfix(HediffWithComps __instance)
        {
            // Only record if we are in a managed tending context
            if (!TendingContext.Active) return;

            // Only track immunizable diseases that we are monitoring
            var immunizable = __instance.TryGetComp<HediffComp_Immunizable>();
            if (immunizable == null) return;

            // Get the actual tend quality from the comp (includes random variance)
            var tendComp = __instance.TryGetComp<HediffComp_TendDuration>();
            float actualQuality = tendComp?.tendQuality ?? 0f;

            if (__instance.pawn != null && __instance.pawn.IsColonist)
            {
                Log.Message($"[RecoveryProcessTracker] Recording Tend Event for {__instance.Label} on {__instance.pawn}. Quality: {actualQuality:P1}, Medicine: {TendingContext.MedicineName} ({TendingContext.MedicineDefName})");
            }

            // Record the event
            DiseaseTracker.Instance?.RecordTendEvent(
                __instance,
                TendingContext.DoctorName,
                TendingContext.MedicineName,
                TendingContext.MedicineDefName,
                actualQuality,
                TendingContext.DoctorSkill
            );
        }
    }
}
