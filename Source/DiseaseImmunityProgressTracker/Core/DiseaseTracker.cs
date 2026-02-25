using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace DiseaseImmunityProgressTracker.Core
{
    /// <summary>
    /// Flags indicating what toxic exposure sources are affecting a pawn.
    /// Multiple flags can be set simultaneously.
    /// </summary>
    [Flags]
    public enum ExposureFlags
    {
        None = 0,
        UnderRoof = 1,          // Safe from fallout (blue in graph)
        InPollution = 2,        // On polluted terrain (orange in graph)
        InToxGas = 4,           // In tox gas cloud (red in graph)
        ToxicFalloutActive = 8  // Map has active toxic fallout event
    }

    /// <summary>
    /// A data point recording severity and exposure state for toxic buildup.
    /// </summary>
    public class ToxicBuildupDataPoint : IExposable
    {
        public int Tick;
        public float Severity;
        public ExposureFlags Exposure;

        public ToxicBuildupDataPoint() { }

        public ToxicBuildupDataPoint(int tick, float severity, ExposureFlags exposure)
        {
            Tick = tick;
            Severity = severity;
            Exposure = exposure;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Tick, "tick");
            Scribe_Values.Look(ref Severity, "severity");
            Scribe_Values.Look(ref Exposure, "exposure");
        }
    }

    /// <summary>
    /// Tracks a period of toxic exposure with a specific set of flags.
    /// </summary>
    public class ExposureInterval : IExposable
    {
        public int StartTick;
        public int EndTick;  // -1 = ongoing
        public ExposureFlags Exposure;

        public ExposureInterval() { }

        public ExposureInterval(int startTick, ExposureFlags exposure)
        {
            StartTick = startTick;
            EndTick = -1;
            Exposure = exposure;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref StartTick, "startTick");
            Scribe_Values.Look(ref EndTick, "endTick", -1);
            Scribe_Values.Look(ref Exposure, "exposure");
        }
    }

    /// <summary>
    /// A single data point recording immunity and severity at a point in time.
    /// </summary>
    public class DiseaseDataPoint : IExposable
    {
        public int Tick;
        public float Immunity;
        public float Severity;

        public DiseaseDataPoint() { }

        public DiseaseDataPoint(int tick, float immunity, float severity)
        {
            Tick = tick;
            Immunity = immunity;
            Severity = severity;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Tick, "tick");
            Scribe_Values.Look(ref Immunity, "immunity");
            Scribe_Values.Look(ref Severity, "severity");
        }
    }

    public class BedRestInterval : IExposable
    {
        public int StartTick;
        public int EndTick;
        public float ImmunityGainSpeedFactor; // The bed's immunity gain speed factor (1.0 = no bonus)

        public BedRestInterval() { }

        public BedRestInterval(int startTick, float immunityGainSpeedFactor)
        {
            StartTick = startTick;
            EndTick = -1; // -1 means ongoing
            ImmunityGainSpeedFactor = immunityGainSpeedFactor;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref StartTick, "startTick");
            Scribe_Values.Look(ref EndTick, "endTick", -1);
            Scribe_Values.Look(ref ImmunityGainSpeedFactor, "immunityGainSpeedFactor", 1f);
        }
    }

    public class TendingEvent : IExposable
    {
        public int Tick;
        public string DoctorName;
        public string MedicineName;
        public string MedicineDefName; // For looking up the icon
        public float Quality;
        public int DoctorSkill;

        public TendingEvent() { }

        public TendingEvent(int tick, string doctorName, string medicineName, string medicineDefName, float quality, int doctorSkill)
        {
            Tick = tick;
            DoctorName = doctorName;
            MedicineName = medicineName;
            MedicineDefName = medicineDefName;
            Quality = quality;
            DoctorSkill = doctorSkill;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Tick, "tick");
            Scribe_Values.Look(ref DoctorName, "doctorName");
            Scribe_Values.Look(ref MedicineName, "medicineName");
            Scribe_Values.Look(ref MedicineDefName, "medicineDefName");
            Scribe_Values.Look(ref Quality, "quality");
            Scribe_Values.Look(ref DoctorSkill, "doctorSkill");
        }
    }

    /// <summary>
    /// A data point recording severity and time remaining for time-based diseases.
    /// </summary>
    public class TimeBasedDataPoint : IExposable
    {
        public int Tick;
        public float Severity;           // Current severity (0-1)
        public int TicksRemaining;       // Ticks until cure
        public int TotalDuration;        // Original duration when disease started
        public bool WasTended;           // Was actively tended at this point

        public TimeBasedDataPoint() { }

        public TimeBasedDataPoint(int tick, float severity, int ticksRemaining, int totalDuration, bool wasTended)
        {
            Tick = tick;
            Severity = severity;
            TicksRemaining = ticksRemaining;
            TotalDuration = totalDuration;
            WasTended = wasTended;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Tick, "tick");
            Scribe_Values.Look(ref Severity, "severity");
            Scribe_Values.Look(ref TicksRemaining, "ticksRemaining");
            Scribe_Values.Look(ref TotalDuration, "totalDuration");
            Scribe_Values.Look(ref WasTended, "wasTended");
        }
    }

    /// <summary>
    /// Tracks historical data for a specific disease instance.
    /// </summary>
    public class DiseaseHistory : IExposable
    {
        public int HediffLoadId;
        public List<DiseaseDataPoint> DataPoints = new List<DiseaseDataPoint>();
        public List<TimeBasedDataPoint> TimeBasedDataPoints = new List<TimeBasedDataPoint>();
        public List<ToxicBuildupDataPoint> ToxicBuildupDataPoints = new List<ToxicBuildupDataPoint>();
        public List<BedRestInterval> BedRestIntervals = new List<BedRestInterval>();
        public List<ExposureInterval> ExposureIntervals = new List<ExposureInterval>();
        public List<TendingEvent> TendingEvents = new List<TendingEvent>();

        // Track the tick when we started recording (for relative time display)
        public int StartTick;

        // Maximum number of data points to keep (about 2 days of data at 1 per hour)
        private const int MaxDataPoints = 50;

        // Minimum ticks between recordings (roughly every hour = 2500 ticks)
        private const int RecordingInterval = 2500;

        private int lastRecordedTick = -99999;
        private int lastTimeBasedRecordedTick = -99999;
        private int lastToxicBuildupRecordedTick = -99999;

        public DiseaseHistory() { }

        public DiseaseHistory(int hediffLoadId, int currentTick)
        {
            HediffLoadId = hediffLoadId;
            StartTick = currentTick;
        }

        public void RecordDataPoint(int tick, float immunity, float severity)
        {
            // Only record if enough time has passed since last recording
            if (tick - lastRecordedTick < RecordingInterval) return;

            DataPoints.Add(new DiseaseDataPoint(tick, immunity, severity));
            lastRecordedTick = tick;

            // Trim old data points if we have too many
            while (DataPoints.Count > MaxDataPoints)
            {
                DataPoints.RemoveAt(0);
            }
        }

        public void RecordTimeBasedDataPoint(int tick, float severity, int ticksRemaining, int totalDuration, bool wasTended)
        {
            // Only record if enough time has passed since last recording
            if (tick - lastTimeBasedRecordedTick < RecordingInterval) return;

            TimeBasedDataPoints.Add(new TimeBasedDataPoint(tick, severity, ticksRemaining, totalDuration, wasTended));
            lastTimeBasedRecordedTick = tick;

            // Trim old data points if we have too many
            while (TimeBasedDataPoints.Count > MaxDataPoints)
            {
                TimeBasedDataPoints.RemoveAt(0);
            }
        }

        public void RecordToxicBuildupDataPoint(int tick, float severity, ExposureFlags exposure)
        {
            // Only record if enough time has passed since last recording
            if (tick - lastToxicBuildupRecordedTick < RecordingInterval) return;

            ToxicBuildupDataPoints.Add(new ToxicBuildupDataPoint(tick, severity, exposure));
            lastToxicBuildupRecordedTick = tick;

            // Trim old data points if we have too many
            while (ToxicBuildupDataPoints.Count > MaxDataPoints)
            {
                ToxicBuildupDataPoints.RemoveAt(0);
            }
        }

        public void UpdateExposureState(int tick, ExposureFlags exposure)
        {
            // Get the last interval if it exists
            ExposureInterval currentInterval = null;
            if (ExposureIntervals.Count > 0)
            {
                currentInterval = ExposureIntervals[ExposureIntervals.Count - 1];
            }

            // Check if exposure state changed
            if (currentInterval == null || currentInterval.EndTick != -1)
            {
                // No current interval - start a new one
                ExposureIntervals.Add(new ExposureInterval(tick, exposure));
            }
            else if (currentInterval.Exposure != exposure)
            {
                // Exposure state changed - close current and start new
                currentInterval.EndTick = tick;
                ExposureIntervals.Add(new ExposureInterval(tick, exposure));
            }
            // If exposure is the same, just continue the current interval
        }

        public void RecordTendEvent(int tick, string doctorName, string medicineName, string medicineDefName, float quality, int doctorSkill)
        {
            TendingEvents.Add(new TendingEvent(tick, doctorName, medicineName, medicineDefName, quality, doctorSkill));
        }

        public void UpdateBedRest(int tick, bool inBed, float immunityGainSpeedFactor)
        {
            // Get the last interval if it exists
            BedRestInterval currentInterval = null;
            if (BedRestIntervals.Count > 0)
            {
                currentInterval = BedRestIntervals[BedRestIntervals.Count - 1];
            }

            if (inBed)
            {
                // If we're not currently tracking an open interval, start one
                if (currentInterval == null || currentInterval.EndTick != -1)
                {
                    BedRestIntervals.Add(new BedRestInterval(tick, immunityGainSpeedFactor));
                }
                // If we have an open interval but the factor changed significantly, close it and start a new one
                else if (currentInterval.EndTick == -1 &&
                         UnityEngine.Mathf.Abs(currentInterval.ImmunityGainSpeedFactor - immunityGainSpeedFactor) > 0.01f)
                {
                    currentInterval.EndTick = tick;
                    BedRestIntervals.Add(new BedRestInterval(tick, immunityGainSpeedFactor));
                }
                // If we have an open interval with same factor, it just continues (no action needed)
            }
            else
            {
                // If we have an open interval, close it
                if (currentInterval != null && currentInterval.EndTick == -1)
                {
                    currentInterval.EndTick = tick;
                }
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref HediffLoadId, "hediffLoadId");
            Scribe_Values.Look(ref StartTick, "startTick");
            Scribe_Collections.Look(ref DataPoints, "dataPoints", LookMode.Deep);
            Scribe_Collections.Look(ref TimeBasedDataPoints, "timeBasedDataPoints", LookMode.Deep);
            Scribe_Collections.Look(ref ToxicBuildupDataPoints, "toxicBuildupDataPoints", LookMode.Deep);
            Scribe_Collections.Look(ref BedRestIntervals, "bedRestIntervals", LookMode.Deep);
            Scribe_Collections.Look(ref ExposureIntervals, "exposureIntervals", LookMode.Deep);
            Scribe_Collections.Look(ref TendingEvents, "tendingEvents", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                DataPoints ??= new List<DiseaseDataPoint>();
                TimeBasedDataPoints ??= new List<TimeBasedDataPoint>();
                ToxicBuildupDataPoints ??= new List<ToxicBuildupDataPoint>();
                BedRestIntervals ??= new List<BedRestInterval>();
                ExposureIntervals ??= new List<ExposureInterval>();
                TendingEvents ??= new List<TendingEvent>();
                if (DataPoints.Count > 0)
                {
                    lastRecordedTick = DataPoints[DataPoints.Count - 1].Tick;
                }
                if (TimeBasedDataPoints.Count > 0)
                {
                    lastTimeBasedRecordedTick = TimeBasedDataPoints[TimeBasedDataPoints.Count - 1].Tick;
                }
                if (ToxicBuildupDataPoints.Count > 0)
                {
                    lastToxicBuildupRecordedTick = ToxicBuildupDataPoints[ToxicBuildupDataPoints.Count - 1].Tick;
                }
            }
        }
    }

    /// <summary>
    /// GameComponent that tracks disease progression history for all pawns.
    /// </summary>
    public class DiseaseTracker : GameComponent
    {
        // Map from hediff loadID to history
        private Dictionary<int, DiseaseHistory> histories = new Dictionary<int, DiseaseHistory>();

        // Temporary list for save/load
        private List<DiseaseHistory> historiesList;

        // Update interval (roughly every in-game hour)
        private const int UpdateInterval = 2500;
        private int lastUpdateTick = 0;

        public DiseaseTracker(Game game) { }

        public override void GameComponentTick()
        {
            int currentTick = Find.TickManager.TicksGame;

            // Only update periodically
            if (currentTick - lastUpdateTick < UpdateInterval) return;
            lastUpdateTick = currentTick;

            // Update all tracked diseases
            UpdateAllDiseases(currentTick);

            // Clean up stale entries periodically
            if (currentTick % (UpdateInterval * 10) == 0)
            {
                CleanupStaleEntries();
            }
        }

        private void UpdateAllDiseases(int currentTick)
        {
            // Find all colonists and prisoners with trackable diseases
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawns)
                {
                    if (pawn.Dead || pawn.health?.hediffSet == null) continue;
                    if (!pawn.IsColonist && !pawn.IsPrisonerOfColony) continue;

                    foreach (var hediff in pawn.health.hediffSet.hediffs)
                    {
                        // Type 4 - Toxic Buildup (check FIRST)
                        // Has HediffComp_ImmunizableToxic but no immunity gain, not tendable
                        // Recovery happens when not exposed to toxins
                        if (IsToxicBuildupDisease(hediff))
                        {
                            RecordToxicBuildupData(hediff, currentTick, pawn);
                            continue;
                        }

                        // Type 3 - Time-based diseases (check before Type 1)
                        // Includes Type 3a (Mechanites) and Type 3b (Fatal rots like Lung Rot, Blood Rot)
                        // Note: Type 3a has HediffComp_Immunizable but should still be tracked here
                        var disappearsComp = hediff.TryGetComp<HediffComp_Disappears>();
                        if (disappearsComp != null && IsTimeBasedDisease(hediff))
                        {
                            var tendComp = hediff.TryGetComp<HediffComp_TendDuration>();
                            RecordTimeBasedData(hediff, currentTick, disappearsComp, tendComp);
                            continue;
                        }

                        // Type 1 - Immunizable diseases (Plague, Flu, Malaria, etc.)
                        // These have immunity racing against severity; whoever hits 100% first wins
                        // Note: Type 3a Mechanites are excluded above via IsTimeBasedDisease check
                        // Note: Type 4 Toxic Buildup is excluded above via IsToxicBuildupDisease check
                        var immunizable = hediff.TryGetComp<HediffComp_Immunizable>();
                        if (immunizable != null)
                        {
                            RecordDiseaseData(hediff, currentTick);
                            RecordBedRestData(hediff, currentTick, pawn);
                            continue;
                        }

                        // Type 2 - Cumulative tend diseases (Gut Worms, Muscle Parasites)
                        // These cure through accumulated tend quality (typically 300% total)
                        var tendCompCumulative = hediff.TryGetComp<HediffComp_TendDuration>();
                        if (tendCompCumulative != null && tendCompCumulative.TProps.disappearsAtTotalTendQuality >= 0)
                        {
                            // Just ensure history exists for tracking tend events
                            // No data points needed since there's no immunity/severity race
                            GetOrCreateHistory(hediff);
                            continue;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a hediff is a Type 3a (mechanite) disease (Fibrous/Sensory Mechanites).
        /// These have HediffComp_Disappears (time-based cure) AND HediffComp_Immunizable,
        /// but the Immunizable component is ONLY used for severity changes (no immunity gain).
        /// Treatment manages severity/pain while waiting for time-based cure.
        /// Non-fatal: severity controls pain level (20% mild, 60% intense at 0.5 threshold).
        /// </summary>
        public static bool IsMechaniteDisease(Hediff hediff)
        {
            if (hediff == null) return false;

            // Must have HediffComp_Disappears (time-based cure)
            var disappearsComp = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappearsComp == null) return false;

            // Must have HediffComp_Immunizable
            var immunizable = hediff.TryGetComp<HediffComp_Immunizable>();
            if (immunizable == null) return false;

            // Key check: NO immunity gain when sick (immunityPerDaySick <= 0)
            // This means the Immunizable comp is only used for severity changes, not immunity racing
            if (immunizable.Props.immunityPerDaySick > 0) return false;

            // Must be tendable (mechanites are tendable)
            if (!hediff.def.tendable) return false;

            return true;
        }

        /// <summary>
        /// Checks if a hediff is a Type 3 (time-based) disease.
        /// Type 3 diseases cure when a countdown timer expires; treatment manages severity while waiting.
        ///
        /// Subtypes:
        /// - Type 3a (Mechanites): Non-fatal, severity controls pain. Has Immunizable but no immunity gain.
        ///   Examples: Fibrous Mechanites, Sensory Mechanites
        /// - Type 3b (Fatal rots): Potentially fatal, severity can kill. No Immunizable component.
        ///   Examples: Lung Rot, Blood Rot
        ///
        /// Common traits: HediffComp_Disappears (timer), NOT cumulative tend.
        /// Filters out temporary effects like drug highs.
        /// </summary>
        public static bool IsTimeBasedDisease(Hediff hediff)
        {
            if (hediff == null) return false;
            var def = hediff.def;

            // Mechanite diseases are a special case of time-based diseases
            if (IsMechaniteDisease(hediff)) return true;

            // Must have HediffComp_Disappears (the timer component)
            var disappearsComp = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappearsComp == null) return false;

            // Must NOT have HediffComp_Immunizable (those are Type 1 - immunity race)
            // Exception: mechanites have Immunizable but are handled above
            var immunizable = hediff.TryGetComp<HediffComp_Immunizable>();
            if (immunizable != null) return false;

            // Must NOT be cumulative tend (those are Type 2)
            var tendComp = hediff.TryGetComp<HediffComp_TendDuration>();
            if (tendComp != null && tendComp.TProps.disappearsAtTotalTendQuality >= 0) return false;

            // --- Filter out non-disease temporary effects ---

            // Must be "bad" (not a buff like drug high benefits)
            if (!def.isBad) return false;

            // Must be tendable (real diseases are tendable; drug effects, anesthetic, etc. are not)
            if (!def.tendable) return false;

            // Should have lethal severity potential (can actually kill the pawn)
            // This excludes things like Hangover, CryptosleepSickness
            if (def.lethalSeverity < 0) return false;

            return true;
        }

        /// <summary>
        /// Checks if a hediff is a Type 4 (toxic buildup) disease.
        /// These have HediffComp_ImmunizableToxic (extends HediffComp_Immunizable) but:
        /// - No immunity gain (immunityPerDaySick = 0)
        /// - Lethal at 100% severity
        /// - NOT tendable (recovery is environmental, not medical)
        /// - NO HediffComp_Disappears (not time-based)
        ///
        /// Recovery happens naturally (-8%/day) when not exposed to toxins.
        /// Exposure stops recovery and can add severity.
        /// </summary>
        public static bool IsToxicBuildupDisease(Hediff hediff)
        {
            if (hediff == null) return false;
            var def = hediff.def;

            // Must have HediffComp_Immunizable
            var immunizable = hediff.TryGetComp<HediffComp_Immunizable>();
            if (immunizable == null) return false;

            // Key check: NO immunity gain when sick (immunityPerDaySick <= 0)
            // This distinguishes toxic buildup from Type 1 (immunizable) diseases
            if (immunizable.Props.immunityPerDaySick > 0) return false;

            // Must NOT have HediffComp_Disappears (that would be Type 3 time-based)
            var disappearsComp = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappearsComp != null) return false;

            // Must be lethal (toxic buildup kills at 100%)
            if (def.lethalSeverity < 0) return false;

            // Must NOT be tendable (toxic buildup is not treated medically)
            // This distinguishes from Type 3a mechanites which ARE tendable
            if (def.tendable) return false;

            // Must be a "bad" hediff
            if (!def.isBad) return false;

            return true;
        }

        /// <summary>
        /// Get the current exposure flags for a pawn.
        /// Checks for toxic fallout, polluted terrain, and tox gas.
        /// </summary>
        public static ExposureFlags GetCurrentExposure(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
                return ExposureFlags.None;

            ExposureFlags flags = ExposureFlags.None;
            var map = pawn.Map;
            var position = pawn.Position;

            // Check if under roof (relevant for toxic fallout)
            if (position.Roofed(map))
            {
                flags |= ExposureFlags.UnderRoof;
            }

            // Check for active toxic fallout on the map
            bool toxicFalloutActive = map.GameConditionManager.ActiveConditions
                .Any(x => x.def.conditionClass == typeof(GameCondition_ToxicFallout));
            if (toxicFalloutActive)
            {
                flags |= ExposureFlags.ToxicFalloutActive;
            }

            // Check for polluted terrain (requires Biotech)
            if (ModsConfig.BiotechActive && position.IsPolluted(map))
            {
                flags |= ExposureFlags.InPollution;
            }

            // Check for tox gas (requires Biotech)
            if (ModsConfig.BiotechActive && position.GasDensity(map, GasType.ToxGas) > 0)
            {
                flags |= ExposureFlags.InToxGas;
            }

            return flags;
        }

        /// <summary>
        /// Check if a pawn is currently safe from toxic exposure (can recover).
        /// Based on HediffComp_ImmunizableToxic.SeverityChangePerDay logic.
        /// </summary>
        public static bool IsSafeFromToxicExposure(Pawn pawn, ExposureFlags exposure)
        {
            if (pawn == null) return true;

            // In tox gas = always exposed
            if ((exposure & ExposureFlags.InToxGas) != 0)
                return false;

            // On polluted terrain and not immune = exposed (blocks recovery)
            if ((exposure & ExposureFlags.InPollution) != 0)
            {
                if (pawn.GetStatValue(StatDefOf.ToxicEnvironmentResistance) < 1f)
                    return false;
            }

            // Toxic fallout active and not roofed and not immune = exposed (blocks recovery)
            if ((exposure & ExposureFlags.ToxicFalloutActive) != 0 &&
                (exposure & ExposureFlags.UnderRoof) == 0)
            {
                if (pawn.GetStatValue(StatDefOf.ToxicResistance) < 1f)
                    return false;
            }

            return true;
        }

        private void RecordToxicBuildupData(Hediff hediff, int currentTick, Pawn pawn)
        {
            var history = GetOrCreateHistory(hediff);
            if (history == null) return;

            ExposureFlags exposure = GetCurrentExposure(pawn);

            // Record data point
            history.RecordToxicBuildupDataPoint(currentTick, hediff.Severity, exposure);

            // Update exposure interval tracking
            history.UpdateExposureState(currentTick, exposure);
        }

        private void RecordTimeBasedData(Hediff hediff, int currentTick, HediffComp_Disappears disappearsComp, HediffComp_TendDuration tendComp)
        {
            var history = GetOrCreateHistory(hediff);
            if (history == null) return;

            bool wasTended = tendComp != null && tendComp.IsTended;
            history.RecordTimeBasedDataPoint(
                currentTick,
                hediff.Severity,
                disappearsComp.ticksToDisappear,
                disappearsComp.disappearsAfterTicks,
                wasTended
            );
        }

        private void RecordBedRestData(Hediff hediff, int currentTick, Pawn pawn)
        {
            int loadId = hediff.loadID;
            if (!histories.TryGetValue(loadId, out var history)) return;

            // Check if the pawn is in bed and get the bed's immunity gain speed factor
            bool inBed = pawn.InBed();
            float immunityGainSpeedFactor = 1f;

            if (inBed)
            {
                Building_Bed bed = pawn.CurrentBed();
                if (bed != null)
                {
                    // Get the bed's ImmunityGainSpeedFactor stat (includes vitals monitor bonus)
                    immunityGainSpeedFactor = bed.GetStatValue(StatDefOf.ImmunityGainSpeedFactor);
                }
            }

            history.UpdateBedRest(currentTick, inBed, immunityGainSpeedFactor);
        }

        private void RecordDiseaseData(Hediff hediff, int currentTick)
        {
            var prognosis = PrognosisCalculator.Calculate(hediff);
            if (!prognosis.IsValid) return;

            int loadId = hediff.loadID;

            if (!histories.TryGetValue(loadId, out var history))
            {
                history = new DiseaseHistory(loadId, currentTick);
                histories[loadId] = history;
            }

            history.RecordDataPoint(currentTick, prognosis.CurrentImmunity, prognosis.CurrentSeverity);
        }

        /// <summary>
        /// Record a tending event for a specific disease.
        /// </summary>
        public void RecordTendEvent(Hediff hediff, string doctorName, string medicineName, string medicineDefName, float quality, int doctorSkill)
        {
            if (hediff == null) return;

            var history = GetOrCreateHistory(hediff);
            if (history != null)
            {
                history.RecordTendEvent(Find.TickManager.TicksGame, doctorName, medicineName, medicineDefName, quality, doctorSkill);
            }
        }

        /// <summary>
        /// Get the history for a specific hediff, or null if not tracked.
        /// </summary>
        public DiseaseHistory GetHistory(Hediff hediff)
        {
            if (hediff == null) return null;
            histories.TryGetValue(hediff.loadID, out var history);
            return history;
        }

        /// <summary>
        /// Get or create history for a hediff (forces immediate tracking).
        /// </summary>
        public DiseaseHistory GetOrCreateHistory(Hediff hediff)
        {
            if (hediff == null) return null;

            int loadId = hediff.loadID;
            if (!histories.TryGetValue(loadId, out var history))
            {
                int currentTick = Find.TickManager.TicksGame;
                history = new DiseaseHistory(loadId, currentTick);
                histories[loadId] = history;

                // Record initial data point
                var prognosis = PrognosisCalculator.Calculate(hediff);
                if (prognosis.IsValid)
                {
                    history.RecordDataPoint(currentTick, prognosis.CurrentImmunity, prognosis.CurrentSeverity);
                }
            }
            return history;
        }

        private void CleanupStaleEntries()
        {
            // Remove entries for hediffs that no longer exist
            var toRemove = new List<int>();

            foreach (var kvp in histories)
            {
                bool found = false;
                foreach (var map in Find.Maps)
                {
                    foreach (var pawn in map.mapPawns.AllPawns)
                    {
                        if (pawn.Dead || pawn.health?.hediffSet == null) continue;

                        foreach (var hediff in pawn.health.hediffSet.hediffs)
                        {
                            if (hediff.loadID == kvp.Key)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (found) break;
                    }
                    if (found) break;
                }

                if (!found)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
            {
                histories.Remove(key);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                historiesList = new List<DiseaseHistory>(histories.Values);
            }

            Scribe_Collections.Look(ref historiesList, "histories", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                histories = new Dictionary<int, DiseaseHistory>();
                if (historiesList != null)
                {
                    foreach (var history in historiesList)
                    {
                        histories[history.HediffLoadId] = history;
                    }
                }
            }
        }

        /// <summary>
        /// Get the tracker instance from the current game.
        /// </summary>
        public static DiseaseTracker Instance => Current.Game?.GetComponent<DiseaseTracker>();
    }
}
