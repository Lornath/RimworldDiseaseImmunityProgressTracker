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
    /// A tooltip companion window that displays Artery Blockage (Type 6) disease progress.
    /// Shows severity, progression rate, heart attack risk, and cure options.
    ///
    /// Artery Blockage is unique:
    /// - Very slow progression (0.0007/day × 0.5-3 random factor)
    /// - No immunity racing - just severity that increases over time
    /// - NOT tendable - cannot be treated medically
    /// - Causes heart attacks with escalating MTB at each stage
    /// - Only curable by: heart replacement, healer serum, luciferium, biosculpter
    /// </summary>
    [StaticConstructorOnStartup]
    public class ArteryBlockageWindow : Window, ICompanionWindow
    {
        private readonly Hediff hediff;
        public Hediff Hediff => hediff;

        // Layout constants
        private const float Padding = 10f;
        private const float ProgressBarHeight = 22f;
        private const float GraphHeight = 100f;

        // Text heights - measured from actual font metrics
        private static float SmallFontHeight => Text.LineHeightOf(GameFont.Small);
        private static float TinyFontHeight => Text.LineHeightOf(GameFont.Tiny);

        // Time constants
        private const float TicksPerDay = 60000f;

        // Severity thresholds for disease stages (from XML def)
        private const float Stage1Threshold = 0.20f;  // minor -> minor (MTB 300->200)
        private const float Stage2Threshold = 0.40f;  // minor -> major (MTB 200->100)
        private const float Stage3Threshold = 0.60f;  // major -> major (MTB 100->60)
        private const float Stage4Threshold = 0.90f;  // major -> extreme (MTB 60->30)

        // Heart attack MTB by stage (from XML def)
        private static readonly (float threshold, int mtbDays, string label)[] StageInfo = {
            (0.00f, 300, "Minor"),
            (0.20f, 200, "Minor"),
            (0.40f, 100, "Major"),
            (0.60f, 60, "Major"),
            (0.90f, 30, "Extreme")
        };

        // Progression rate (from XML: severityPerDayNotImmune = 0.0007)
        private const float BaseProgressionPerDay = 0.0007f;

        // Colors
        private static readonly Color BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        private static readonly Color ProgressBarBgColor = new Color(0.2f, 0.2f, 0.2f);
        private static readonly Color SeverityColor = new Color(0.8f, 0.2f, 0.2f);
        private static readonly Color SeverityProjectionColor = new Color(0.8f, 0.2f, 0.2f, 0.5f);
        private static readonly Color AxisColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color GridColor = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color NowLineColor = new Color(1f, 1f, 1f, 0.5f);
        private static readonly Color SafeColor = new Color(0.4f, 1f, 0.4f);
        private static readonly Color WarningColor = new Color(1f, 0.9f, 0.2f);
        private static readonly Color DangerColor = new Color(1f, 0.4f, 0.4f);

        // Stage threshold line colors
        private static readonly Color ThresholdLineColor = new Color(1f, 0.6f, 0.2f, 0.6f);

        public override Vector2 InitialSize => new Vector2(340f, CalculateWindowHeight());

        /// <summary>
        /// Calculate window height based on actual font metrics.
        /// </summary>
        private static float CalculateWindowHeight()
        {
            return Padding * 2                          // Top and bottom padding
                 + SmallFontHeight + 4f                 // Title + gap
                 + SmallFontHeight + 4f                 // Severity display + gap
                 + ProgressBarHeight + 6f              // Severity bar + gap
                 + TinyFontHeight * 2 + 6f             // Progression info (2 lines) + gap
                 + TinyFontHeight * 3 + 6f             // Heart attack risk (3 lines) + gap
                 + GraphHeight + 6f                     // Graph + gap
                 + TinyFontHeight * 2 + 4f             // Cure options (2 lines) + gap
                 + SmallFontHeight * 2;                 // Verdict (2 lines for wrapping)
        }

        protected override float Margin => 0f;

        public ArteryBlockageWindow(Hediff hediff)
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

            // Cache font heights
            float smallHeight = SmallFontHeight;
            float tinyHeight = TinyFontHeight;

            // Apply padding to create content area
            Rect contentRect = inRect.ContractedBy(Padding);
            float yOffset = contentRect.y;
            float contentWidth = contentRect.width;

            // Draw title with stage
            DrawTitle(new Rect(contentRect.x, yOffset, contentWidth, smallHeight));
            yOffset += smallHeight + 4f;

            // Draw severity section
            DrawSeverityDisplay(new Rect(contentRect.x, yOffset, contentWidth, smallHeight));
            yOffset += smallHeight + 4f;

            // Draw severity bar
            DrawSeverityBar(new Rect(contentRect.x, yOffset, contentWidth, ProgressBarHeight));
            yOffset += ProgressBarHeight + 6f;

            // Draw progression info
            DrawProgressionInfo(new Rect(contentRect.x, yOffset, contentWidth, tinyHeight * 2), history);
            yOffset += tinyHeight * 2 + 6f;

            // Draw heart attack risk
            DrawHeartAttackRisk(new Rect(contentRect.x, yOffset, contentWidth, tinyHeight * 3));
            yOffset += tinyHeight * 3 + 6f;

            // Draw severity graph
            DrawSeverityGraph(new Rect(contentRect.x, yOffset, contentWidth, GraphHeight), history);
            yOffset += GraphHeight + 6f;

            // Draw cure options
            DrawCureOptions(new Rect(contentRect.x, yOffset, contentWidth, tinyHeight * 2));
            yOffset += tinyHeight * 2 + 4f;

            // Draw verdict (2 lines to allow wrapping)
            DrawVerdict(new Rect(contentRect.x, yOffset, contentWidth, smallHeight * 2));

            Text.Font = GameFont.Small;
        }

        private void DrawTitle(Rect rect)
        {
            Text.Font = GameFont.Small;
            string stageName = GetCurrentStageName();
            string title = $"Artery Blockage ({stageName})";
            Widgets.Label(rect, title);
        }

        private string GetCurrentStageName()
        {
            float severity = hediff.Severity;
            for (int i = StageInfo.Length - 1; i >= 0; i--)
            {
                if (severity >= StageInfo[i].threshold)
                {
                    return StageInfo[i].label;
                }
            }
            return "Minor";
        }

        private int GetCurrentStageMTB()
        {
            float severity = hediff.Severity;
            for (int i = StageInfo.Length - 1; i >= 0; i--)
            {
                if (severity >= StageInfo[i].threshold)
                {
                    return StageInfo[i].mtbDays;
                }
            }
            return StageInfo[0].mtbDays;
        }

        private void DrawSeverityDisplay(Rect rect)
        {
            Text.Font = GameFont.Small;
            int severityPct = (int)(hediff.Severity * 100);

            Color severityColor = GetSeverityColor(hediff.Severity);
            GUI.color = severityColor;
            Widgets.Label(rect, $"Severity: {severityPct}%");
            GUI.color = Color.white;
        }

        private void DrawSeverityBar(Rect rect)
        {
            // Background
            Widgets.DrawBoxSolid(rect, ProgressBarBgColor);

            // Fill based on severity
            float fillWidth = rect.width * Mathf.Clamp01(hediff.Severity);
            if (fillWidth > 0)
            {
                Color fillColor = GetSeverityColor(hediff.Severity);
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, fillWidth, rect.height), fillColor);
            }

            // Draw stage threshold markers
            DrawThresholdMarker(rect, Stage1Threshold, "20%");
            DrawThresholdMarker(rect, Stage2Threshold, "40%");
            DrawThresholdMarker(rect, Stage3Threshold, "60%");
            DrawThresholdMarker(rect, Stage4Threshold, "90%");

            // Border
            Widgets.DrawBox(rect);

            // Percentage text centered
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            int severityPct = (int)(hediff.Severity * 100);
            Widgets.Label(rect, $"{severityPct}%");
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawThresholdMarker(Rect barRect, float threshold, string label)
        {
            float x = barRect.x + barRect.width * threshold;
            GUI.color = ThresholdLineColor;
            Widgets.DrawLineVertical(x, barRect.y, barRect.height);
            GUI.color = Color.white;
        }

        private void DrawProgressionInfo(Rect rect, DiseaseHistory history)
        {
            Text.Font = GameFont.Tiny;
            float rowHeight = TinyFontHeight;

            // Calculate observed or theoretical progression rate
            float progressionRate = CalculateProgressionRate(history);
            float progressionPctPerDay = progressionRate * 100f;

            GUI.color = AxisColor;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, rowHeight),
                $"Progression: ~{progressionPctPerDay:0.0#}%/day");

            // Calculate time to next stage and fatal
            float severity = hediff.Severity;
            float nextStageThreshold = GetNextStageThreshold(severity);

            string nextStageText;
            if (nextStageThreshold > severity && progressionRate > 0)
            {
                float daysToNextStage = (nextStageThreshold - severity) / progressionRate;
                string nextStageLabel = GetStageNameAt(nextStageThreshold);
                int nextStagePct = (int)(nextStageThreshold * 100);
                nextStageText = $"Next stage ({nextStageLabel} {nextStagePct}%): ~{FormatDays(daysToNextStage)}";
            }
            else if (progressionRate > 0)
            {
                float daysToFatal = (1f - severity) / progressionRate;
                nextStageText = $"Fatal (100%): ~{FormatDays(daysToFatal)}";
            }
            else
            {
                nextStageText = "Progression stalled";
            }

            Widgets.Label(new Rect(rect.x, rect.y + rowHeight, rect.width, rowHeight), nextStageText);
            GUI.color = Color.white;
        }

        private float GetNextStageThreshold(float currentSeverity)
        {
            foreach (var stage in StageInfo)
            {
                if (stage.threshold > currentSeverity)
                {
                    return stage.threshold;
                }
            }
            return 1f; // Fatal
        }

        private string GetStageNameAt(float threshold)
        {
            for (int i = StageInfo.Length - 1; i >= 0; i--)
            {
                if (threshold >= StageInfo[i].threshold)
                {
                    return StageInfo[i].label;
                }
            }
            return "Minor";
        }

        private string FormatDays(float days)
        {
            if (days >= 365)
            {
                float years = days / 365f;
                return $"{years:0.#} years";
            }
            else if (days >= 60)
            {
                // RimWorld uses 60-day quadrums
                float quadrums = days / 60f;
                return $"{quadrums:0.#} quadrums";
            }
            else
            {
                return $"{days:0} days";
            }
        }

        private void DrawHeartAttackRisk(Rect rect)
        {
            Text.Font = GameFont.Tiny;
            float rowHeight = TinyFontHeight;

            // Row 1: Header
            GUI.color = DangerColor;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, rowHeight), "Heart Attack Risk");
            GUI.color = Color.white;

            // Current MTB and daily risk
            int mtbDays = GetCurrentStageMTB();
            float dailyRisk = CalculateDailyRisk(mtbDays);

            // Row 2: Current risk
            GUI.color = GetRiskColor(dailyRisk);
            Widgets.Label(new Rect(rect.x, rect.y + rowHeight, rect.width, rowHeight),
                $"Current: {dailyRisk:0.#}%/day (MTB {mtbDays}d)");

            // Row 3: Expected attacks before next stage
            float severity = hediff.Severity;
            float nextStageThreshold = GetNextStageThreshold(severity);
            float progressionRate = CalculateProgressionRate(null); // Use base rate for this

            if (nextStageThreshold > severity && progressionRate > 0)
            {
                float expectedAttacks = CalculateExpectedAttacks(severity, nextStageThreshold, progressionRate);
                Widgets.Label(new Rect(rect.x, rect.y + rowHeight * 2, rect.width, rowHeight),
                    $"~{expectedAttacks:0.#} attacks expected before next stage");
            }
            GUI.color = Color.white;
        }

        private float CalculateDailyRisk(int mtbDays)
        {
            if (mtbDays <= 0) return 0f;
            // Daily probability = 1 - e^(-1/MTB)
            return (1f - Mathf.Exp(-1f / mtbDays)) * 100f;
        }

        /// <summary>
        /// Calculate expected number of heart attacks while severity progresses from start to end.
        /// This uses the fact that expected attacks = integral of (1/MTB) over time.
        /// </summary>
        private float CalculateExpectedAttacks(float startSeverity, float endSeverity, float progressionRate)
        {
            // Simplified calculation: use average MTB over the range
            int startMTB = GetMTBAtSeverity(startSeverity);
            int endMTB = GetMTBAtSeverity(endSeverity - 0.001f); // Just before threshold
            float avgMTB = (startMTB + endMTB) / 2f;
            float days = (endSeverity - startSeverity) / progressionRate;
            return days / avgMTB;
        }

        private int GetMTBAtSeverity(float severity)
        {
            for (int i = StageInfo.Length - 1; i >= 0; i--)
            {
                if (severity >= StageInfo[i].threshold)
                {
                    return StageInfo[i].mtbDays;
                }
            }
            return StageInfo[0].mtbDays;
        }

        private Color GetRiskColor(float dailyRiskPercent)
        {
            if (dailyRiskPercent > 2f) return DangerColor;
            if (dailyRiskPercent > 0.5f) return WarningColor;
            return AxisColor;
        }

        private void DrawSeverityGraph(Rect graphArea, DiseaseHistory history)
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

            // Background
            Widgets.DrawBoxSolid(plotArea, BackgroundColor);

            // Draw stage threshold lines
            DrawGraphThresholdLine(plotArea, Stage1Threshold);
            DrawGraphThresholdLine(plotArea, Stage2Threshold);
            DrawGraphThresholdLine(plotArea, Stage3Threshold);
            DrawGraphThresholdLine(plotArea, Stage4Threshold);

            // Draw axes
            GUI.color = AxisColor;
            Widgets.DrawLineVertical(plotArea.x, plotArea.y, plotArea.height);
            Widgets.DrawLineHorizontal(plotArea.x, plotArea.yMax, plotArea.width);
            GUI.color = Color.white;

            // Calculate time window
            // For artery blockage, show longer time scale - past 2 quadrums (120 days), future 1 quadrum (60 days)
            const float pastDays = 120f;  // 2 quadrums
            float futureDays = CalculateFutureDays();
            const float totalDays = pastDays + 60f; // Fixed scale for consistency
            float nowRatio = pastDays / totalDays;
            float nowX = plotArea.x + plotArea.width * nowRatio;

            // Draw "now" vertical line
            GUI.color = NowLineColor;
            Widgets.DrawLineVertical(nowX, plotArea.y, plotArea.height);
            GUI.color = Color.white;

            // Draw historical severity data
            int currentTick = Find.TickManager.TicksGame;
            if (history != null && history.DataPoints.Count >= 2)
            {
                int pastTicks = (int)(pastDays * TicksPerDay);
                int windowStart = currentTick - pastTicks;

                var relevantPoints = history.DataPoints
                    .Where(p => p.Tick >= windowStart && p.Tick <= currentTick)
                    .OrderBy(p => p.Tick)
                    .ToList();

                if (relevantPoints.Count >= 2)
                {
                    for (int i = 0; i < relevantPoints.Count - 1; i++)
                    {
                        var p1 = relevantPoints[i];
                        var p2 = relevantPoints[i + 1];

                        float dayOffset1 = (p1.Tick - currentTick) / TicksPerDay;
                        float dayOffset2 = (p2.Tick - currentTick) / TicksPerDay;
                        float xRatio1 = (pastDays + dayOffset1) / totalDays;
                        float xRatio2 = (pastDays + dayOffset2) / totalDays;

                        Vector2 start = new Vector2(
                            plotArea.x + plotArea.width * xRatio1,
                            plotArea.yMax - plotArea.height * p1.Severity
                        );
                        Vector2 end = new Vector2(
                            plotArea.x + plotArea.width * xRatio2,
                            plotArea.yMax - plotArea.height * p2.Severity
                        );
                        Widgets.DrawLine(start, end, SeverityColor, 2f);
                    }
                }
            }

            // Draw current severity point
            float sevNowY = plotArea.yMax - plotArea.height * hediff.Severity;
            Vector2 sevNow = new Vector2(nowX, sevNowY);
            DrawPointMarker(sevNow, SeverityColor);

            // Draw projection line
            float progressionRate = CalculateProgressionRate(history);
            float projectedSeverity = hediff.Severity + (progressionRate * 60f); // Project 1 quadrum
            projectedSeverity = Mathf.Clamp01(projectedSeverity);

            float endXRatio = (pastDays + 60f) / totalDays;
            float endY = plotArea.yMax - plotArea.height * projectedSeverity;
            Vector2 endPoint = new Vector2(plotArea.x + plotArea.width * endXRatio, endY);
            Widgets.DrawLine(sevNow, endPoint, SeverityProjectionColor, 2f);

            // Show projected value at end
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = AxisColor;
            int projectedPct = (int)(projectedSeverity * 100);
            Widgets.Label(new Rect(endPoint.x + 3f, endY - 8f, 36f, 18f), $"{projectedPct}%");
            Text.Anchor = TextAnchor.UpperLeft;

            // Y-axis labels
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(graphArea.x, plotArea.y - 2f, leftMargin - 4f, 16f), "100%");
            Widgets.Label(new Rect(graphArea.x, plotArea.yMax - 14f, leftMargin - 4f, 16f), "0%");
            Text.Anchor = TextAnchor.UpperLeft;

            // X-axis labels
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(plotArea.x - 15f, plotArea.yMax + 1f, 40f, 16f), "-2Q");
            Widgets.Label(new Rect(nowX - 15f, plotArea.yMax + 1f, 30f, 16f), "Now");
            Widgets.Label(new Rect(plotArea.xMax - 15f, plotArea.yMax + 1f, 30f, 16f), "+1Q");
            Text.Anchor = TextAnchor.UpperLeft;

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawGraphThresholdLine(Rect plotArea, float threshold)
        {
            float y = plotArea.yMax - plotArea.height * threshold;
            GUI.color = ThresholdLineColor;
            Widgets.DrawLineHorizontal(plotArea.x, y, plotArea.width);
            GUI.color = Color.white;
        }

        private float CalculateFutureDays()
        {
            // Show at least 1 quadrum (60 days) into the future
            return 60f;
        }

        private void DrawPointMarker(Vector2 center, Color color)
        {
            Rect marker = new Rect(center.x - 3f, center.y - 3f, 6f, 6f);
            Widgets.DrawBoxSolid(marker, color);
        }

        private float CalculateProgressionRate(DiseaseHistory history)
        {
            // Try to calculate from historical data first
            if (history != null && history.DataPoints.Count >= 2)
            {
                var points = history.DataPoints;
                var latest = points[points.Count - 1];

                // For artery blockage, use longer lookback (1-4 days) due to slow progression
                const int minTicksForCalculation = (int)(TicksPerDay * 1);   // 1 day minimum
                const int maxTicksForCalculation = (int)(TicksPerDay * 30);  // 30 days max
                DiseaseDataPoint earlier = null;

                for (int i = points.Count - 2; i >= 0; i--)
                {
                    int tickDiff = latest.Tick - points[i].Tick;
                    if (tickDiff >= minTicksForCalculation)
                    {
                        earlier = points[i];
                        if (tickDiff >= maxTicksForCalculation)
                            break;
                    }
                }

                if (earlier != null)
                {
                    int ticksElapsed = latest.Tick - earlier.Tick;
                    float daysElapsed = ticksElapsed / TicksPerDay;
                    float severityChange = latest.Severity - earlier.Severity;
                    if (daysElapsed > 0)
                    {
                        return severityChange / daysElapsed;
                    }
                }
            }

            // Fall back to base rate with middle of random factor (0.5-3 -> ~1.5 average)
            return BaseProgressionPerDay * 1.5f;
        }

        private void DrawCureOptions(Rect rect)
        {
            Text.Font = GameFont.Tiny;
            float rowHeight = TinyFontHeight;

            GUI.color = SafeColor;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, rowHeight), "Cures:");
            GUI.color = AxisColor;
            Widgets.Label(new Rect(rect.x, rect.y + rowHeight, rect.width, rowHeight),
                "Replace heart, healer serum, luciferium, biosculpter");
            GUI.color = Color.white;
        }

        private void DrawVerdict(Rect rect)
        {
            Text.Font = GameFont.Small;

            string verdictText;
            Color verdictColor;

            float severity = hediff.Severity;
            int mtbDays = GetCurrentStageMTB();

            if (severity >= Stage4Threshold)
            {
                verdictText = $"CRITICAL - Extreme heart attack risk (MTB {mtbDays}d)";
                verdictColor = DangerColor;
            }
            else if (severity >= Stage3Threshold)
            {
                verdictText = $"SERIOUS - High heart attack risk (MTB {mtbDays}d)";
                verdictColor = DangerColor;
            }
            else if (severity >= Stage2Threshold)
            {
                verdictText = $"MODERATE - Elevated heart attack risk (MTB {mtbDays}d)";
                verdictColor = WarningColor;
            }
            else if (severity >= Stage1Threshold)
            {
                verdictText = $"MILD - Low heart attack risk (MTB {mtbDays}d)";
                verdictColor = WarningColor;
            }
            else
            {
                verdictText = $"EARLY - Very low heart attack risk (MTB {mtbDays}d)";
                verdictColor = SafeColor;
            }

            GUI.color = verdictColor;
            Widgets.Label(rect, verdictText);
            GUI.color = Color.white;
        }

        private Color GetSeverityColor(float severity)
        {
            if (severity >= Stage4Threshold) return DangerColor;
            if (severity >= Stage2Threshold) return WarningColor;
            return SafeColor;
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            // Close if the hediff we're tracking is no longer active in the tooltip
            bool tooltipActive = Patches.ArteryBlockagePatch.IsTooltipActiveFor(hediff);
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
                if (window is ArteryBlockageWindow abWindow && abWindow.hediff == hediff)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
