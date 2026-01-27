using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RecoveryProcessTracker.Core
{
    /// <summary>
    /// Calculates disease prognosis by extracting immunity/severity data from hediffs
    /// and projecting when immunity will be reached vs when severity will be lethal.
    /// </summary>
    public class PrognosisCalculator
    {
        // Ticks per day in RimWorld
        public const float TicksPerDay = 60000f;

        /// <summary>
        /// Results of a prognosis calculation for a disease.
        /// </summary>
        public class PrognosisResult
        {
            // Current values (0-1 range)
            public float CurrentImmunity;
            public float CurrentSeverity;

            // Rates per day (observed from history if available, otherwise theoretical)
            public float ImmunityPerDay;
            public float SeverityPerDay;

            // Whether rates are from observed history or theoretical calculation
            public bool UsingObservedRates;

            // Projections (in days, float.PositiveInfinity if not applicable)
            public float DaysUntilImmune;
            public float DaysUntilDeath;

            // Verdict
            public bool WillSurvive;
            public float MarginDays; // Positive = survives with margin, negative = dies before immune

            // Whether this is a valid immunizable disease
            public bool IsValid;

            public string GetVerdictString()
            {
                if (!IsValid) return "Unknown";
                if (CurrentImmunity >= 1f) return "Immune";
                if (ImmunityPerDay <= 0f && SeverityPerDay <= 0f) return "Stable";
                if (ImmunityPerDay <= 0f && SeverityPerDay > 0f) return "At risk";
                if (SeverityPerDay <= 0f) return "Recovering";
                return WillSurvive ? "Will survive" : "At risk";
            }
        }

        /// <summary>
        /// Calculate prognosis for a hediff with an immunizable component.
        /// Uses observed rates from history if available, otherwise falls back to theoretical rates.
        /// </summary>
        public static PrognosisResult Calculate(Hediff hediff, DiseaseHistory history = null)
        {
            var result = new PrognosisResult { IsValid = false };

            if (hediff == null) return result;

            var pawn = hediff.pawn;
            if (pawn == null || pawn.Dead) return result;

            // Find the immunizable component
            var immunizable = hediff.TryGetComp<HediffComp_Immunizable>();
            if (immunizable == null) return result;

            // Get immunity handler and record
            var immunityHandler = pawn.health?.immunity;
            if (immunityHandler == null) return result;

            // Ensure immunity record exists
            immunityHandler.TryAddImmunityRecord(hediff.def, hediff.def);
            var immunityRecord = immunityHandler.GetImmunityRecord(hediff.def);
            if (immunityRecord == null) return result;

            // Extract current values
            result.CurrentImmunity = immunityRecord.immunity;
            result.CurrentSeverity = hediff.Severity;

            // Try to calculate observed rates from history
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            var observedRates = CalculateObservedRates(history, currentTick);

            if (observedRates.HasValue)
            {
                // Use observed rates (these naturally account for treatment effects)
                result.ImmunityPerDay = observedRates.Value.immunityRate;
                result.SeverityPerDay = observedRates.Value.severityRate;
                result.UsingObservedRates = true;
            }
            else
            {
                // Fall back to theoretical rates
                result.ImmunityPerDay = immunityRecord.ImmunityChangePerTick(pawn, sick: true, hediff) * TicksPerDay;
                result.SeverityPerDay = immunizable.SeverityChangePerDay();
                result.UsingObservedRates = false;
            }

            // Calculate projections
            result.DaysUntilImmune = CalculateDaysUntil(result.CurrentImmunity, 1f, result.ImmunityPerDay);
            result.DaysUntilDeath = CalculateDaysUntil(result.CurrentSeverity, 1f, result.SeverityPerDay);

            // Determine verdict
            if (result.DaysUntilImmune < result.DaysUntilDeath)
            {
                result.WillSurvive = true;
                result.MarginDays = result.DaysUntilDeath - result.DaysUntilImmune;
            }
            else
            {
                result.WillSurvive = false;
                result.MarginDays = result.DaysUntilImmune - result.DaysUntilDeath;
            }

            result.IsValid = true;
            return result;
        }

        /// <summary>
        /// Calculate observed rates from historical data.
        /// Returns null if insufficient data.
        /// </summary>
        private static (float immunityRate, float severityRate)? CalculateObservedRates(DiseaseHistory history, int currentTick)
        {
            if (history == null || history.DataPoints.Count < 2)
                return null;

            // Use data from the last few hours to calculate rate
            // This gives us a recent average that accounts for current treatment
            const int minTicksForCalculation = 2500; // About 1 hour minimum
            const int maxTicksForCalculation = 15000; // About 6 hours maximum - recent enough to reflect current conditions

            var points = history.DataPoints;
            var latestPoint = points[points.Count - 1];

            // Find a point far enough back but not too far
            DiseaseDataPoint earlierPoint = null;
            for (int i = points.Count - 2; i >= 0; i--)
            {
                int tickDiff = latestPoint.Tick - points[i].Tick;
                if (tickDiff >= minTicksForCalculation)
                {
                    earlierPoint = points[i];
                    if (tickDiff >= maxTicksForCalculation)
                        break; // Don't go further back than needed
                }
            }

            if (earlierPoint == null)
                return null;

            int ticksElapsed = latestPoint.Tick - earlierPoint.Tick;
            if (ticksElapsed <= 0)
                return null;

            float daysElapsed = ticksElapsed / TicksPerDay;

            float immunityChange = latestPoint.Immunity - earlierPoint.Immunity;
            float severityChange = latestPoint.Severity - earlierPoint.Severity;

            float immunityRate = immunityChange / daysElapsed;
            float severityRate = severityChange / daysElapsed;

            return (immunityRate, severityRate);
        }

        /// <summary>
        /// Calculate days until a value reaches a target at a given rate.
        /// Returns float.PositiveInfinity if rate is zero or negative, or if already past target.
        /// </summary>
        private static float CalculateDaysUntil(float current, float target, float ratePerDay)
        {
            if (current >= target) return 0f;
            if (ratePerDay <= 0f) return float.PositiveInfinity;

            float remaining = target - current;
            float days = remaining / ratePerDay;

            // Sanity check
            if (float.IsNaN(days) || float.IsInfinity(days) || days < 0f)
                return float.PositiveInfinity;

            return days;
        }

        /// <summary>
        /// Project what a value will be after a certain number of days at a given rate.
        /// Clamps result to 0-1 range.
        /// </summary>
        public static float ProjectValue(float current, float ratePerDay, float days)
        {
            float projected = current + (ratePerDay * days);
            return Math.Max(0f, Math.Min(1f, projected));
        }
    }
}
