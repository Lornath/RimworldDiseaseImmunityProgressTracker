using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RecoveryProcessTracker
{
    public class RecoveryProcessTrackerMod : Mod
    {
        public static RecoveryProcessTrackerSettings Settings;

        public RecoveryProcessTrackerMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RecoveryProcessTrackerSettings>();

            var harmony = new Harmony("Lornath.RecoveryProcessTracker");
            harmony.PatchAll();

            Log.Message("[RecoveryProcessTracker] Initialized");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Recovery Process Tracker";
        }
    }

    public class RecoveryProcessTrackerSettings : ModSettings
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
