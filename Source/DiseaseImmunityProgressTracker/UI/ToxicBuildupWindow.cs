using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using DiseaseImmunityProgressTracker.Core;

namespace DiseaseImmunityProgressTracker.UI
{
    /// <summary>
    /// A tooltip companion window that displays Toxic Buildup (Type 4) disease progress.
    /// Shows severity, exposure status, health risks (dementia/carcinoma), and recovery estimate.
    ///
    /// Toxic Buildup is unique:
    /// - No immunity racing - just severity that heals when safe
    /// - Recovery rate: -8%/day when not exposed to toxins
    /// - Exposure sources: Toxic Fallout (unroofed), Pollution, Tox Gas
    /// - Lethal at 100% severity
    /// - Causes dementia/carcinoma risks at 40%+ severity
    /// </summary>
    [StaticConstructorOnStartup]
    public class ToxicBuildupWindow : Window, ICompanionWindow
    {
        private readonly Hediff hediff;
        public Hediff Hediff => hediff;

        // Layout constants
        private const float Padding = 10f;
        private const float GraphHeight = 100f;

        // Text heights - measured from actual font metrics
        private static float SmallFontHeight => Text.LineHeightOf(GameFont.Small);
        private static float TinyFontHeight => Text.LineHeightOf(GameFont.Tiny);

        // Time constants
        private const float TicksPerDay = 60000f;

        // Severity thresholds for disease stages
        private const float ModerateThreshold = 0.40f;
        private const float SeriousThreshold = 0.60f;
        private const float ExtremeThreshold = 0.80f;

        // Recovery rate (severity per day when safe)
        private const float RecoveryRatePerDay = -0.08f;

        // MTB values for health effects by stage (from HediffDef)
        private static readonly (float dementiaMTB, float carcinomaMTB)[] StageMTBs = {
            (0f, 0f),           // Initial (0-40%)
            (146f, 438f),       // Moderate (40-60%)
            (37f, 111f),        // Serious (60-80%)
            (13f, 39f)          // Extreme (80-100%)
        };

        // Colors
        private static readonly Color BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        private static readonly Color AxisColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color GridColor = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color SeverityColor = new Color(0.8f, 0.2f, 0.2f);
        private static readonly Color SeverityProjectionColor = new Color(0.8f, 0.2f, 0.2f, 0.5f);
        private static readonly Color NowLineColor = new Color(1f, 1f, 1f, 0.5f);
        private static readonly Color SafeColor = new Color(0.4f, 1f, 0.4f);
        private static readonly Color WarningColor = new Color(1f, 0.9f, 0.2f);
        private static readonly Color DangerColor = new Color(1f, 0.4f, 0.4f);

        // Exposure colors for graph background
        private static readonly Color ToxGasColor = new Color(1f, 0.2f, 0.2f, 0.25f);      // Red - most dangerous
        private static readonly Color PollutionColor = new Color(1f, 0.6f, 0.2f, 0.25f);   // Orange
        private static readonly Color SafeRoofedColor = new Color(0.3f, 0.5f, 1f, 0.15f);  // Blue - safe from fallout

        // Threshold line colors
        private static readonly Color ModerateLineColor = new Color(1f, 0.9f, 0.2f, 0.6f);   // Yellow
        private static readonly Color SeriousLineColor = new Color(1f, 0.6f, 0.2f, 0.6f);    // Orange
        private static readonly Color ExtremeLineColor = new Color(1f, 0.3f, 0.2f, 0.6f);    // Red

        public override Vector2 InitialSize => new Vector2(320f, CalculateWindowHeight());

        /// <summary>
        /// Calculate window height based on actual font metrics.
        /// Layout: Padding + Title + Exposure + Resistance + Graph + Legend + Risks (3 rows) + Verdict + Padding
        /// </summary>
        private static float CalculateWindowHeight()
        {
            float smallHeight = SmallFontHeight;
            float tinyHeight = TinyFontHeight;

            return Padding * 2                    // Top and bottom padding
                 + smallHeight + 4f               // Title + gap
                 + smallHeight + 2f               // Exposure status + gap
                 + smallHeight + 6f               // Resistance stat + gap
                 + GraphHeight + 4f               // Graph + gap
                 + tinyHeight + 6f                // Legend + gap
                 + tinyHeight * 3 + 6f            // Health risks (3 rows) + gap
                 + smallHeight;                   // Verdict
        }

        protected override float Margin => 0f;

        public ToxicBuildupWindow(Hediff hediff)
        {
            this.hediff = hediff;

            // Window configuration for tooltip-like behavior
            doCloseX = false;
            doCloseButton = false;
            draggable = false;
            absorbInputAroundWindow = false;
            forcePause = false;
            closeOnClickedOutside = false;
            closeOnCancel = false;
            closeOnAccept = false;
            layer = WindowLayer.Super;
            soundAppear = null;
            soundClose = null;
            drawShadow = true;
            preventCameraMotion = false;
            focusWhenOpened = false;
            onlyOneOfTypeAllowed = false; // Allow multiple disease windows simultaneously
        }

        protected override void SetInitialSizeAndPosition()
        {
            windowRect = CompanionWindowManager.CalculateStackedWindowRect(InitialSize, this);
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Dynamically update position to follow the tooltip as the mouse moves
            Rect newRect = CompanionWindowManager.CalculateStackedWindowRect(InitialSize, this);
            windowRect.x = newRect.x;
            windowRect.y = newRect.y;

            // Get tracking data
            var tracker = DiseaseTracker.Instance;
            var history = tracker?.GetOrCreateHistory(hediff);

            // Get current exposure state
            var pawn = hediff?.pawn;
            ExposureFlags currentExposure = pawn != null ? DiseaseTracker.GetCurrentExposure(pawn) : ExposureFlags.None;
            bool isSafe = pawn != null && DiseaseTracker.IsSafeFromToxicExposure(pawn, currentExposure);

            // Apply padding to create content area
            Rect contentRect = inRect.ContractedBy(Padding);
            float yOffset = contentRect.y;
            float contentWidth = contentRect.width;

            // Draw title with stage
            DrawTitle(new Rect(contentRect.x, yOffset, contentWidth, SmallFontHeight));
            yOffset += SmallFontHeight + 4f;

            // Draw safety/exposure status
            DrawExposureStatus(new Rect(contentRect.x, yOffset, contentWidth, SmallFontHeight), currentExposure, isSafe);
            yOffset += SmallFontHeight + 2f;

            // Draw toxic resistance stat
            DrawResistanceStat(new Rect(contentRect.x, yOffset, contentWidth, SmallFontHeight), pawn);
            yOffset += SmallFontHeight + 6f;

            // Draw severity graph with exposure backgrounds
            DrawSeverityGraph(new Rect(contentRect.x, yOffset, contentWidth, GraphHeight), history, currentExposure, isSafe);
            yOffset += GraphHeight + 4f;

            // Draw legend
            DrawLegend(new Rect(contentRect.x, yOffset, contentWidth, TinyFontHeight));
            yOffset += TinyFontHeight + 6f;

            // Draw health risk info (if severity >= 40%)
            if (hediff.Severity >= ModerateThreshold)
            {
                DrawHealthRisks(new Rect(contentRect.x, yOffset, contentWidth, TinyFontHeight * 3));
                yOffset += TinyFontHeight * 3 + 6f;
            }

            // Draw verdict
            DrawVerdict(new Rect(contentRect.x, yOffset, contentWidth, SmallFontHeight), isSafe, currentExposure);

            Text.Font = GameFont.Small;
        }

        private void DrawTitle(Rect rect)
        {
            Text.Font = GameFont.Small;
            string stageName = GetStageName(hediff.Severity);
            int severityPct = (int)(hediff.Severity * 100);
            string title = "DIPT_TXW_Title".Translate(stageName, $"{severityPct}");
            Widgets.Label(rect, title);
        }

        private string GetStageName(float severity)
        {
            if (severity >= ExtremeThreshold) return "DIPT_TXW_StageExtreme".Translate();
            if (severity >= SeriousThreshold) return "DIPT_TXW_StageSerious".Translate();
            if (severity >= ModerateThreshold) return "DIPT_TXW_StageModerate".Translate();
            if (severity >= 0.2f) return "DIPT_TXW_StageMinor".Translate();
            return "DIPT_TXW_StageInitial".Translate();
        }

        private void DrawExposureStatus(Rect rect, ExposureFlags exposure, bool isSafe)
        {
            Text.Font = GameFont.Small;

            if (isSafe)
            {
                GUI.color = SafeColor;
                Widgets.Label(rect, "DIPT_TXW_SafeRecovering".Translate());
            }
            else
            {
                // Build list of active exposure sources
                var sources = new List<string>();
                if ((exposure & ExposureFlags.InToxGas) != 0)
                    sources.Add("DIPT_TXW_ExposureToxGas".Translate());
                if ((exposure & ExposureFlags.InPollution) != 0)
                    sources.Add("DIPT_TXW_ExposurePollution".Translate());
                if ((exposure & ExposureFlags.ToxicFalloutActive) != 0 && (exposure & ExposureFlags.UnderRoof) == 0)
                    sources.Add("DIPT_TXW_ExposureToxicFallout".Translate());

                GUI.color = DangerColor;
                string sourceList = sources.Count > 0 ? string.Join(", ", sources) : "Unknown";
                Widgets.Label(rect, "DIPT_TXW_ExposedTo".Translate(sourceList));
            }

            GUI.color = Color.white;
        }

        private void DrawResistanceStat(Rect rect, Pawn pawn)
        {
            if (pawn == null) return;

            Text.Font = GameFont.Tiny;
            GUI.color = AxisColor;

            float toxicResistance = pawn.GetStatValue(StatDefOf.ToxicResistance);
            float toxicEnvResistance = pawn.GetStatValue(StatDefOf.ToxicEnvironmentResistance);

            // Show the more relevant resistance based on context
            string resistText;
            if (toxicResistance >= 1f && toxicEnvResistance >= 1f)
            {
                resistText = "DIPT_TXW_ResistanceImmune".Translate();
                GUI.color = SafeColor;
            }
            else if (toxicResistance != toxicEnvResistance)
            {
                resistText = "DIPT_TXW_ResistanceBoth".Translate($"{toxicResistance * 100:0}", $"{toxicEnvResistance * 100:0}");
            }
            else
            {
                resistText = "DIPT_TXW_ResistanceSingle".Translate($"{toxicResistance * 100:0}");
            }

            Widgets.Label(rect, resistText);
            GUI.color = Color.white;
        }

        private void DrawSeverityGraph(Rect graphArea, DiseaseHistory history, ExposureFlags currentExposure, bool isSafe)
        {
            // Reserve space for axis labels
            const float leftMargin = 38f;
            const float rightMargin = 28f;
            const float topMargin = 2f;
            const float bottomMargin = 18f;

            Rect plotArea = new Rect(
                graphArea.x + leftMargin,
                graphArea.y + topMargin,
                graphArea.width - leftMargin - rightMargin,
                graphArea.height - topMargin - bottomMargin
            );

            // Background for the plot area
            Widgets.DrawBoxSolid(plotArea, BackgroundColor);

            // Draw exposure interval backgrounds
            DrawExposureIntervals(plotArea, history);

            // Draw threshold lines at 40%, 60%, 80%
            DrawThresholdLines(plotArea);

            // Draw axes
            GUI.color = AxisColor;
            Widgets.DrawLineVertical(plotArea.x, plotArea.y, plotArea.height);
            Widgets.DrawLineHorizontal(plotArea.x, plotArea.yMax, plotArea.width);
            GUI.color = Color.white;

            // Calculate time window (past 2 days, future based on recovery time)
            float pastDays = 2f;
            float futureDays = CalculateFutureDays(isSafe);
            int currentTick = Find.TickManager.TicksGame;

            float startTick = currentTick - (pastDays * TicksPerDay);
            float endTick = currentTick + (futureDays * TicksPerDay);
            float totalTicks = endTick - startTick;

            // Draw "now" vertical line
            float nowX = plotArea.x + plotArea.width * (pastDays / (pastDays + futureDays));
            GUI.color = NowLineColor;
            Widgets.DrawLineVertical(nowX, plotArea.y, plotArea.height);
            GUI.color = Color.white;

            // Draw historical severity data
            if (history != null && history.ToxicBuildupDataPoints.Count >= 2)
            {
                var points = history.ToxicBuildupDataPoints.OrderBy(p => p.Tick).ToList();

                for (int i = 0; i < points.Count - 1; i++)
                {
                    var p1 = points[i];
                    var p2 = points[i + 1];

                    if (p2.Tick < startTick || p1.Tick > endTick) continue;

                    Vector2 start = TickToGraphPoint(plotArea, p1.Tick, p1.Severity, startTick, totalTicks);
                    Vector2 end = TickToGraphPoint(plotArea, p2.Tick, p2.Severity, startTick, totalTicks);
                    Widgets.DrawLine(start, end, SeverityColor, 2f);
                }
            }

            // Draw current severity point
            float currentSeverity = hediff.Severity;
            Vector2 nowPoint = new Vector2(nowX, plotArea.yMax - plotArea.height * currentSeverity);
            DrawPointMarker(nowPoint, SeverityColor);

            // Draw projection line
            float projectedSeverity = CalculateProjectedSeverity(currentSeverity, futureDays, isSafe, history);
            float projectedX = plotArea.xMax;
            float projectedY = plotArea.yMax - plotArea.height * Mathf.Clamp01(projectedSeverity);

            Widgets.DrawLine(nowPoint, new Vector2(projectedX, projectedY), SeverityProjectionColor, 2f);

            // Show projected value
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = AxisColor;
            int projectedPct = (int)(projectedSeverity * 100);
            Widgets.Label(new Rect(projectedX + 3f, projectedY - 8f, 36f, 18f), $"{projectedPct}%");
            Text.Anchor = TextAnchor.UpperLeft;

            // Y-axis labels
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(graphArea.x, plotArea.y - 2f, leftMargin - 4f, 16f), "100%");
            Widgets.Label(new Rect(graphArea.x, plotArea.yMax - 14f, leftMargin - 4f, 16f), "0%");
            Text.Anchor = TextAnchor.UpperLeft;

            // X-axis labels
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(plotArea.x - 20f, plotArea.yMax + 1f, 40f, 16f), "DIPT_Shared_AxisPast".Translate($"{pastDays:0}"));
            Widgets.Label(new Rect(nowX - 20f, plotArea.yMax + 1f, 40f, 16f), "DIPT_Shared_Now".Translate());
            Widgets.Label(new Rect(plotArea.xMax - 20f, plotArea.yMax + 1f, 40f, 16f), "DIPT_Shared_AxisFuture".Translate($"{futureDays:0.#}"));
            Text.Anchor = TextAnchor.UpperLeft;

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawExposureIntervals(Rect plotArea, DiseaseHistory history)
        {
            if (history == null || history.ExposureIntervals.Count == 0) return;

            int currentTick = Find.TickManager.TicksGame;
            float pastDays = 2f;
            float futureDays = CalculateFutureDays(DiseaseTracker.IsSafeFromToxicExposure(hediff?.pawn, DiseaseTracker.GetCurrentExposure(hediff?.pawn)));
            float startTick = currentTick - (pastDays * TicksPerDay);

            // Calculate the width of just the "past" section (left of "now" line)
            float nowFraction = pastDays / (pastDays + futureDays);
            float pastSectionWidth = plotArea.width * nowFraction;

            foreach (var interval in history.ExposureIntervals)
            {
                // Only draw intervals within our visible past range
                int intervalStart = Mathf.Max(interval.StartTick, (int)startTick);
                int intervalEnd = interval.EndTick == -1 ? currentTick : Mathf.Min(interval.EndTick, currentTick);

                if (intervalEnd < startTick) continue;

                // Get the worst exposure color for this interval
                Color bgColor = GetWorstExposureColor(interval.Exposure);
                if (bgColor.a <= 0) continue;

                // Calculate x positions within the past section only
                float pastTicks = currentTick - startTick;
                float startX = (intervalStart - startTick) / pastTicks * pastSectionWidth;
                float endX = (intervalEnd - startTick) / pastTicks * pastSectionWidth;

                // Clamp to past section bounds
                startX = Mathf.Clamp(startX, 0, pastSectionWidth);
                endX = Mathf.Clamp(endX, 0, pastSectionWidth);

                if (endX > startX)
                {
                    Rect bgRect = new Rect(plotArea.x + startX, plotArea.y, endX - startX, plotArea.height);
                    Widgets.DrawBoxSolid(bgRect, bgColor);
                }
            }
        }

        private Color GetWorstExposureColor(ExposureFlags exposure)
        {
            // Priority: Tox Gas (red) > Pollution (orange) > Safe/Roofed during fallout (blue)
            if ((exposure & ExposureFlags.InToxGas) != 0)
                return ToxGasColor;
            if ((exposure & ExposureFlags.InPollution) != 0)
                return PollutionColor;
            if ((exposure & ExposureFlags.ToxicFalloutActive) != 0 && (exposure & ExposureFlags.UnderRoof) != 0)
                return SafeRoofedColor;

            return Color.clear;
        }

        private void DrawThresholdLines(Rect plotArea)
        {
            // Draw thin horizontal lines at severity thresholds
            DrawThresholdLine(plotArea, ModerateThreshold, ModerateLineColor, "40%");
            DrawThresholdLine(plotArea, SeriousThreshold, SeriousLineColor, "60%");
            DrawThresholdLine(plotArea, ExtremeThreshold, ExtremeLineColor, "80%");
        }

        private void DrawThresholdLine(Rect plotArea, float threshold, Color color, string label)
        {
            float y = plotArea.yMax - plotArea.height * threshold;
            GUI.color = color;
            Widgets.DrawLineHorizontal(plotArea.x, y, plotArea.width);

            // Don't draw labels to avoid clutter - the y-axis already shows scale
            GUI.color = Color.white;
        }

        private Vector2 TickToGraphPoint(Rect plotArea, int tick, float severity, float startTick, float totalTicks)
        {
            float x = plotArea.x + plotArea.width * ((tick - startTick) / totalTicks);
            float y = plotArea.yMax - plotArea.height * Mathf.Clamp01(severity);
            return new Vector2(x, y);
        }

        private void DrawPointMarker(Vector2 center, Color color)
        {
            Rect marker = new Rect(center.x - 3f, center.y - 3f, 6f, 6f);
            Widgets.DrawBoxSolid(marker, color);
        }

        private float CalculateFutureDays(bool isSafe)
        {
            if (!isSafe)
            {
                // If exposed, show shorter future window
                return 1f;
            }

            // Calculate days until fully recovered
            float daysToRecover = hediff.Severity / (-RecoveryRatePerDay);
            return Mathf.Clamp(daysToRecover, 1f, 5f);
        }

        private float CalculateProjectedSeverity(float currentSeverity, float futureDays, bool isSafe, DiseaseHistory history)
        {
            // Use observed rate from historical data for more accurate projection
            float observedRate = CalculateObservedSeverityRate(history);

            if (observedRate != 0f)
            {
                // Use observed rate - this accounts for actual pawn behavior (time indoors vs outdoors, etc.)
                float projectedSeverity = currentSeverity + (observedRate * futureDays);
                return Mathf.Clamp(projectedSeverity, 0f, 1f);
            }

            // Fall back to theoretical rates if no historical data
            if (!isSafe)
            {
                // Assume some accumulation when exposed, but less than max theoretical
                // since pawn is unlikely to be outside 100% of the time
                float projectedSeverity = currentSeverity + (0.20f * futureDays); // Conservative +20%/day
                return Mathf.Clamp01(projectedSeverity);
            }

            // Recovery at -8%/day when safe
            float recoveredSeverity = currentSeverity + (RecoveryRatePerDay * futureDays);
            return Mathf.Max(0f, recoveredSeverity);
        }

        /// <summary>
        /// Calculate observed severity change rate from historical data.
        /// Uses a 1-9 hour lookback window for a stable average that reflects
        /// the pawn's actual mix of indoor/outdoor time.
        ///
        /// If tox gas was detected in recent history, uses a shorter 1-2 hour window
        /// because tox gas causes such rapid severity changes that longer averages
        /// would be misleading after the pawn leaves the gas.
        /// </summary>
        private float CalculateObservedSeverityRate(DiseaseHistory history)
        {
            if (history == null || history.ToxicBuildupDataPoints.Count < 2)
                return 0f;

            var points = history.ToxicBuildupDataPoints;
            var latest = points[points.Count - 1];

            // Check if tox gas appears in recent history (within normal lookback window)
            // If so, use a much shorter lookback to avoid the tox gas spike dominating
            const int toxGasCheckWindow = 22500;  // ~9 hours - check this far back for tox gas
            const int shortLookbackMax = 5000;    // ~2 hours - use this if tox gas found
            const int normalLookbackMax = 22500;  // ~9 hours - normal stable average

            bool recentToxGas = HasRecentToxGasExposure(points, latest.Tick, toxGasCheckWindow);
            int maxTicksForCalculation = recentToxGas ? shortLookbackMax : normalLookbackMax;
            const int minTicksForCalculation = 2500;   // ~1 hour minimum always

            // Find the furthest point back within our window (prefer longer averages for stability)
            ToxicBuildupDataPoint earlier = null;
            for (int i = points.Count - 2; i >= 0; i--)
            {
                int tickDiff = latest.Tick - points[i].Tick;
                if (tickDiff >= minTicksForCalculation)
                {
                    earlier = points[i];
                    // Keep going to find an even older point, up to max window
                    if (tickDiff >= maxTicksForCalculation)
                        break;
                }
            }

            if (earlier == null)
                return 0f;

            int ticksElapsed = latest.Tick - earlier.Tick;
            if (ticksElapsed <= 0)
                return 0f;

            float daysElapsed = ticksElapsed / TicksPerDay;
            float severityChange = latest.Severity - earlier.Severity;

            return severityChange / daysElapsed;
        }

        /// <summary>
        /// Check if any data points within the specified window had tox gas exposure.
        /// </summary>
        private bool HasRecentToxGasExposure(List<ToxicBuildupDataPoint> points, int currentTick, int windowTicks)
        {
            int cutoffTick = currentTick - windowTicks;
            for (int i = points.Count - 1; i >= 0; i--)
            {
                if (points[i].Tick < cutoffTick)
                    break;
                if ((points[i].Exposure & ExposureFlags.InToxGas) != 0)
                    return true;
            }
            return false;
        }

        private void DrawLegend(Rect rect)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = AxisColor;

            float x = rect.x;
            float boxSize = 10f;
            float spacing = 6f;

            // Blue = safe (roofed during fallout)
            Widgets.DrawBoxSolid(new Rect(x, rect.y + 3f, boxSize, boxSize), SafeRoofedColor);
            x += boxSize + 2f;
            Widgets.Label(new Rect(x, rect.y, 50f, rect.height), "DIPT_TXW_LegendSafe".Translate());
            x += 40f + spacing;

            // Orange = pollution
            Widgets.DrawBoxSolid(new Rect(x, rect.y + 3f, boxSize, boxSize), PollutionColor);
            x += boxSize + 2f;
            Widgets.Label(new Rect(x, rect.y, 55f, rect.height), "DIPT_TXW_ExposurePollution".Translate());
            x += 50f + spacing;

            // Red = tox gas
            Widgets.DrawBoxSolid(new Rect(x, rect.y + 3f, boxSize, boxSize), ToxGasColor);
            x += boxSize + 2f;
            Widgets.Label(new Rect(x, rect.y, 55f, rect.height), "DIPT_TXW_ExposureToxGas".Translate());

            GUI.color = Color.white;
        }

        private void DrawHealthRisks(Rect rect)
        {
            Text.Font = GameFont.Tiny;
            float rowHeight = TinyFontHeight;

            // Draw header with severity stage
            string stageName = GetStageName(hediff.Severity).ToUpper();
            GUI.color = GetStageColor(hediff.Severity);
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, rowHeight), "DIPT_TXW_ExposureRisks".Translate(stageName));
            GUI.color = Color.white;

            var (dementiaMTB, carcinomaMTB) = GetCurrentStageMTBs(hediff.Severity);
            float yOffset = rect.y + rowHeight;

            if (dementiaMTB > 0)
            {
                float dailyDementiaRisk = CalculateDailyRisk(dementiaMTB);
                GUI.color = GetRiskColor(dailyDementiaRisk);
                Widgets.Label(new Rect(rect.x + 8f, yOffset, rect.width - 8f, rowHeight), "DIPT_TXW_RiskDementia".Translate($"{dailyDementiaRisk:0.#}"));
                yOffset += rowHeight;
            }

            if (carcinomaMTB > 0)
            {
                float dailyCarcinomaRisk = CalculateDailyRisk(carcinomaMTB);
                GUI.color = GetRiskColor(dailyCarcinomaRisk);
                Widgets.Label(new Rect(rect.x + 8f, yOffset, rect.width - 8f, rowHeight), "DIPT_TXW_RiskCarcinoma".Translate($"{dailyCarcinomaRisk:0.#}"));
            }

            GUI.color = Color.white;
        }

        private Color GetStageColor(float severity)
        {
            if (severity >= ExtremeThreshold) return DangerColor;
            if (severity >= SeriousThreshold) return new Color(1f, 0.6f, 0.2f); // Orange
            return WarningColor; // Moderate (40-60%)
        }

        private (float dementiaMTB, float carcinomaMTB) GetCurrentStageMTBs(float severity)
        {
            if (severity >= ExtremeThreshold) return StageMTBs[3];
            if (severity >= SeriousThreshold) return StageMTBs[2];
            if (severity >= ModerateThreshold) return StageMTBs[1];
            return StageMTBs[0];
        }

        private float CalculateDailyRisk(float mtbDays)
        {
            if (mtbDays <= 0) return 0f;
            // Daily probability = 1 - (1 - 1/MTB)^1 ≈ 1/MTB for small probabilities
            // More accurate: 1 - e^(-1/MTB)
            return (1f - Mathf.Exp(-1f / mtbDays)) * 100f;
        }

        private Color GetRiskColor(float dailyRiskPercent)
        {
            if (dailyRiskPercent > 5f) return DangerColor;
            if (dailyRiskPercent > 1f) return WarningColor;
            return AxisColor;
        }

        private void DrawVerdict(Rect rect, bool isSafe, ExposureFlags exposure)
        {
            Text.Font = GameFont.Small;

            string verdictText;
            Color verdictColor;

            float severity = hediff.Severity;

            if (isSafe)
            {
                if (severity < 0.1f)
                {
                    verdictText = "DIPT_TXW_VerdictNearlyCleared".Translate();
                    verdictColor = SafeColor;
                }
                else
                {
                    float daysToRecover = severity / (-RecoveryRatePerDay);
                    verdictText = "DIPT_TXW_VerdictClearInDays".Translate($"{daysToRecover:0.#}");
                    verdictColor = SafeColor;
                }
            }
            else
            {
                // Get contextual advice based on exposure source
                string advice = GetExposureAdvice(exposure, severity);
                verdictText = advice;
                verdictColor = severity >= ExtremeThreshold ? DangerColor : WarningColor;
            }

            GUI.color = verdictColor;
            Widgets.Label(rect, verdictText);
            GUI.color = Color.white;
        }

        private string GetExposureAdvice(ExposureFlags exposure, float severity)
        {
            // Tox gas is the most urgent - always prioritize this advice
            if ((exposure & ExposureFlags.InToxGas) != 0)
            {
                if (severity >= ExtremeThreshold)
                    return "DIPT_TXW_VerdictCriticalToxGas".Translate();
                if (severity >= SeriousThreshold)
                    return "DIPT_TXW_VerdictWarnToxGasRising".Translate();
                return "DIPT_TXW_VerdictToxGasBeginRecovery".Translate();
            }

            // Pollution
            if ((exposure & ExposureFlags.InPollution) != 0)
            {
                if (severity >= ExtremeThreshold)
                    return "DIPT_TXW_VerdictCriticalPollution".Translate();
                if (severity >= SeriousThreshold)
                    return "DIPT_TXW_VerdictWarnPollutionRising".Translate();
                return "DIPT_TXW_VerdictPollutionBeginRecovery".Translate();
            }

            // Toxic fallout (unroofed)
            if ((exposure & ExposureFlags.ToxicFalloutActive) != 0 && (exposure & ExposureFlags.UnderRoof) == 0)
            {
                if (severity >= ExtremeThreshold)
                    return "DIPT_TXW_VerdictCriticalFallout".Translate();
                if (severity >= SeriousThreshold)
                    return "DIPT_TXW_VerdictWarnFalloutRising".Translate();
                return "DIPT_TXW_VerdictFalloutBeginRecovery".Translate();
            }

            // Fallback for unknown exposure
            if (severity >= ExtremeThreshold)
                return "DIPT_TXW_VerdictCriticalGeneral".Translate();
            return "DIPT_TXW_VerdictGeneralBeginRecovery".Translate();
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            // Close if the hediff we're tracking is no longer active in the tooltip
            bool tooltipActive = Patches.ToxicBuildupPatch.IsTooltipActiveFor(hediff);
            bool mouseOverWindow = Mouse.IsOver(windowRect);

            if (!tooltipActive && !mouseOverWindow)
            {
                Close(false);
            }
        }

        public override void PreClose()
        {
            base.PreClose();
            CompanionWindowManager.UnregisterWindow(this);
        }

        /// <summary>
        /// Check if a window is already open for the given hediff.
        /// </summary>
        public static bool IsOpenFor(Hediff hediff)
        {
            if (hediff == null) return false;

            foreach (var window in Find.WindowStack.Windows)
            {
                if (window is ToxicBuildupWindow tbWindow && tbWindow.hediff == hediff)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
