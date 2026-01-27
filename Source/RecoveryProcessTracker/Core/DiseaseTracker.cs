using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RecoveryProcessTracker.Core
{
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

    /// <summary>
    /// Tracks historical data for a specific disease instance.
    /// </summary>
    public class DiseaseHistory : IExposable
    {
        public int HediffLoadId;
        public List<DiseaseDataPoint> DataPoints = new List<DiseaseDataPoint>();

        // Track the tick when we started recording (for relative time display)
        public int StartTick;

        // Maximum number of data points to keep (about 2 days of data at 1 per hour)
        private const int MaxDataPoints = 50;

        // Minimum ticks between recordings (roughly every hour = 2500 ticks)
        private const int RecordingInterval = 2500;

        private int lastRecordedTick = -99999;

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

        public void ExposeData()
        {
            Scribe_Values.Look(ref HediffLoadId, "hediffLoadId");
            Scribe_Values.Look(ref StartTick, "startTick");
            Scribe_Collections.Look(ref DataPoints, "dataPoints", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                DataPoints ??= new List<DiseaseDataPoint>();
                if (DataPoints.Count > 0)
                {
                    lastRecordedTick = DataPoints[DataPoints.Count - 1].Tick;
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
            // Find all colonists and prisoners with immunizable diseases
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawns)
                {
                    if (pawn.Dead || pawn.health?.hediffSet == null) continue;
                    if (!pawn.IsColonist && !pawn.IsPrisonerOfColony) continue;

                    foreach (var hediff in pawn.health.hediffSet.hediffs)
                    {
                        var immunizable = hediff.TryGetComp<HediffComp_Immunizable>();
                        if (immunizable == null) continue;

                        RecordDiseaseData(hediff, currentTick);
                    }
                }
            }
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
