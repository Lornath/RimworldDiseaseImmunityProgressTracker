using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace DiseaseImmunityProgressTracker
{
    public class DiseaseImmunityProgressTrackerMod : Mod
    {
        public static DiseaseImmunityProgressTrackerSettings Settings;

        public DiseaseImmunityProgressTrackerMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<DiseaseImmunityProgressTrackerSettings>();

            var harmony = new Harmony("Lornath.DiseaseImmunityProgressTracker");
            harmony.PatchAll();

            Log.Message("[DiseaseImmunityProgressTracker] Initialized");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Disease Immunity Progress Tracker";
        }
    }

    public class DiseaseImmunityProgressTrackerSettings : ModSettings
    {
        public bool verboseLogging = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref verboseLogging, "verboseLogging", false);
        }

        public void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("Enable verbose logging", ref verboseLogging);
            listing.End();
        }
    }
}
