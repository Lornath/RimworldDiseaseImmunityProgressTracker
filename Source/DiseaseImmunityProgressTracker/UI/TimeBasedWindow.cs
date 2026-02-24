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
    /// A tooltip companion window that displays Type 3 (time-based) disease progress.
    /// These diseases cure when a countdown timer expires; treatment manages severity while waiting.
    ///
    /// Handles both subtypes:
    /// - Type 3a (Mechanites): Non-fatal, severity controls pain (20% mild / 60% intense at 0.5 threshold)
    /// - Type 3b (Fatal Rots): Potentially fatal, severity can kill (Lung Rot, Blood Rot)
    /// </summary>
    [StaticConstructorOnStartup]
    public class TimeBasedWindow : Window, ICompanionWindow
    {
        private readonly Hediff hediff;
        public Hediff Hediff => hediff;

        private readonly HediffComp_Disappears disappearsComp;
        private readonly HediffComp_TendDuration tendComp;
        private readonly bool isMechanite;

        // Layout constants
        private const float Padding = 10f;
        private const float ProgressBarHeight = 22f;
        private const float GraphHeight = 95f;
        private const int MaxTendsToShow = 3;

        // Text heights - measured from actual font metrics
        private static float SmallFontHeight => Text.LineHeightOf(GameFont.Small);
        private static float TinyFontHeight => Text.LineHeightOf(GameFont.Tiny);

        // Time constants
        private const float TicksPerDay = 60000f;
        private const float TicksPerHour = 2500f;

        // Mechanite pain threshold (severity >= 0.5 = intense pain)
        private const float MechanitePainThreshold = 0.5f;

        // Colors
        private static readonly Color BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        private static readonly Color ProgressBarBgColor = new Color(0.2f, 0.2f, 0.2f);
        private static readonly Color ProgressBarFillColor = new Color(0.3f, 0.6f, 0.9f); // Blue for time progress
        private static readonly Color SeverityColor = new Color(0.8f, 0.2f, 0.2f); // Red
        private static readonly Color SeverityProjectionColor = new Color(0.8f, 0.2f, 0.2f, 0.5f);
        private static readonly Color AxisColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color GridColor = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color NowLineColor = new Color(1f, 1f, 1f, 0.5f);
        private static readonly Color SafeColor = new Color(0.4f, 1f, 0.4f);
        private static readonly Color WarningColor = new Color(1f, 0.9f, 0.2f);
        private static readonly Color DangerColor = new Color(1f, 0.4f, 0.4f);
        private static readonly Color TendedColor = new Color(0.3f, 0.8f, 0.3f);
        private static readonly Color NeedsTendingColor = new Color(0.9f, 0.6f, 0.2f);
        private static readonly Color PainThresholdColor = new Color(1f, 0.6f, 0.2f, 0.6f); // Orange for pain threshold

        // Fallback icon for tending without medicine
        private static readonly Texture2D NoMedsIcon = ContentFinder<Texture2D>.Get("UI/Icons/Medical/NoMeds");

        public override Vector2 InitialSize => new Vector2(320f, CalculateWindowHeight());

        /// <summary>
        /// Calculate window height based on actual font metrics.
        /// </summary>
        private static float CalculateWindowHeight()
        {
            return Padding * 2                          // Top and bottom padding
                 + SmallFontHeight + 4f                 // Title + gap
                 + SmallFontHeight + 4f                 // Time remaining + gap
                 + ProgressBarHeight + 6f              // Progress bar + gap
                 + SmallFontHeight + 4f                 // Severity display + gap
                 + GraphHeight + 6f                     // Severity graph + gap
                 + TinyFontHeight + 2f                  // "Recent tends:" label + gap
                 + TinyFontHeight * MaxTendsToShow + 8f // Tend entries + gap
                 + SmallFontHeight;                     // Verdict
        }

        protected override float Margin => 0f;

        public TimeBasedWindow(Hediff hediff, HediffComp_Disappears disappearsComp)
        {
            this.hediff = hediff;
            this.disappearsComp = disappearsComp;
            this.tendComp = hediff?.TryGetComp<HediffComp_TendDuration>();
            this.isMechanite = DiseaseTracker.IsMechaniteDisease(hediff);

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
            // Use stacked positioning to avoid overlap with other companion windows
            windowRect = CompanionWindowManager.CalculateStackedWindowRect(InitialSize, this);
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Dynamically update position to follow the tooltip as the mouse moves
            // (Done here in DoWindowContents where Event.current is valid)
            Rect newRect = CompanionWindowManager.CalculateStackedWindowRect(InitialSize, this);
            windowRect.x = newRect.x;
            windowRect.y = newRect.y;

            // Get history
            var tracker = DiseaseTracker.Instance;
            var history = tracker?.GetOrCreateHistory(hediff);

            // Calculate prognosis
            var prognosis = CalculatePrognosis(history);

            // Cache font heights
            float smallHeight = SmallFontHeight;
            float tinyHeight = TinyFontHeight;

            // Apply padding to create content area
            Rect contentRect = inRect.ContractedBy(Padding);
            float yOffset = contentRect.y;
            float contentWidth = contentRect.width;

            // Draw title
            Text.Font = GameFont.Small;
            string title = hediff?.Label?.CapitalizeFirst() ?? "Disease";
            Widgets.Label(new Rect(contentRect.x, yOffset, contentWidth, smallHeight), title);
            yOffset += smallHeight + 4f;

            // Draw time remaining
            DrawTimeRemaining(new Rect(contentRect.x, yOffset, contentWidth, smallHeight));
            yOffset += smallHeight + 4f;

            // Draw progress bar for cure countdown
            Rect progressBarRect = new Rect(contentRect.x, yOffset, contentWidth, ProgressBarHeight);
            DrawProgressBar(progressBarRect);
            yOffset += ProgressBarHeight + 6f;

            // Draw severity section with mini-graph
            DrawSeveritySection(new Rect(contentRect.x, yOffset, contentWidth, GraphHeight + smallHeight + 4f), history, prognosis);
            yOffset += GraphHeight + smallHeight + 10f;

            // Draw recent tends section
            Text.Font = GameFont.Tiny;
            GUI.color = AxisColor;
            Widgets.Label(new Rect(contentRect.x, yOffset, contentWidth, tinyHeight), "DIPT_Shared_RecentTends".Translate());
            GUI.color = Color.white;
            yOffset += tinyHeight + 2f;

            DrawRecentTends(new Rect(contentRect.x, yOffset, contentWidth, MaxTendsToShow * tinyHeight), history);
            yOffset += MaxTendsToShow * tinyHeight + 8f;

            // Draw verdict
            DrawVerdict(new Rect(contentRect.x, yOffset, contentWidth, smallHeight), prognosis);

            Text.Font = GameFont.Small;
        }

        private void DrawTimeRemaining(Rect rect)
        {
            if (disappearsComp == null) return;

            float daysRemaining = disappearsComp.ticksToDisappear / TicksPerDay;
            string timeText;

            if (daysRemaining < 1f)
            {
                float hoursRemaining = disappearsComp.ticksToDisappear / TicksPerHour;
                timeText = "DIPT_TBW_TimeRemainingHours".Translate($"{hoursRemaining:0.#}");
            }
            else
            {
                timeText = "DIPT_TBW_TimeRemainingDays".Translate($"{daysRemaining:0.#}");
            }

            // Show tending status on the same line
            bool isTended = tendComp != null && tendComp.IsTended;
            string tendStatus = isTended ? (string)"DIPT_TBW_Tended".Translate() : (string)"DIPT_TBW_NeedsTending".Translate();
            Color tendColor = isTended ? TendedColor : NeedsTendingColor;

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width - 90f, rect.height), timeText);

            GUI.color = tendColor;
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, rect.height), tendStatus);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private void DrawProgressBar(Rect rect)
        {
            if (disappearsComp == null) return;

            float progress = disappearsComp.Progress; // 0 = just started, 1 = about to cure

            // Background
            Widgets.DrawBoxSolid(rect, ProgressBarBgColor);

            // Fill
            float fillWidth = rect.width * Mathf.Clamp01(progress);
            if (fillWidth > 0)
            {
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, fillWidth, rect.height), ProgressBarFillColor);
            }

            // Border
            Widgets.DrawBox(rect);

            // Percentage text centered
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            int progressPct = (int)(progress * 100);
            Widgets.Label(rect, "DIPT_TBW_PercentComplete".Translate($"{progressPct}"));
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawSeveritySection(Rect rect, DiseaseHistory history, TimeBasedPrognosis prognosis)
        {
            // Severity label with current value
            Text.Font = GameFont.Small;
            int severityPct = (int)(hediff.Severity * 100);
            Color severityLabelColor = GetSeverityColor(hediff.Severity);
            GUI.color = severityLabelColor;

            if (isMechanite)
            {
                // For mechanites, show pain level instead of just severity
                string painLevel = hediff.Severity >= MechanitePainThreshold ? "Intense pain" : "Mild pain";
                int painPct = hediff.Severity >= MechanitePainThreshold ? 60 : 20;
                Widgets.Label(new Rect(rect.x, rect.y, rect.width, 20f), "DIPT_TBW_SeverityWithPain".Translate(severityPct, painLevel, painPct));
            }
            else
            {
                Widgets.Label(new Rect(rect.x, rect.y, rect.width, 20f), "DIPT_TBW_Severity".Translate($"{severityPct}"));
            }
            GUI.color = Color.white;

            // Mini severity graph
            Rect graphRect = new Rect(rect.x, rect.y + 22f, rect.width, GraphHeight);
            DrawSeverityGraph(graphRect, history, prognosis);
        }

        private void DrawSeverityGraph(Rect graphArea, DiseaseHistory history, TimeBasedPrognosis prognosis)
        {
            // Reserve space for axis labels
            const float leftMargin = 38f;   // Space for "100%" and "50%" labels
            const float rightMargin = 28f;  // Space for projected % label
            const float topMargin = 2f;     // Small top padding
            const float bottomMargin = 18f; // Space for X-axis labels

            // The actual drawable plot area (inside margins)
            Rect plotArea = new Rect(
                graphArea.x + leftMargin,
                graphArea.y + topMargin,
                graphArea.width - leftMargin - rightMargin,
                graphArea.height - topMargin - bottomMargin
            );

            // Background for the plot area
            Widgets.DrawBoxSolid(plotArea, BackgroundColor);

            // Draw axes
            GUI.color = AxisColor;
            // Y-axis (left edge of plot)
            Widgets.DrawLineVertical(plotArea.x, plotArea.y, plotArea.height);
            // X-axis (bottom edge of plot)
            Widgets.DrawLineHorizontal(plotArea.x, plotArea.yMax, plotArea.width);
            GUI.color = Color.white;

            // 50% grid line
            GUI.color = GridColor;
            float midY = plotArea.y + plotArea.height / 2f;
            Widgets.DrawLineHorizontal(plotArea.x, midY, plotArea.width);
            GUI.color = Color.white;

            // For mechanites, draw the pain threshold line at 50% severity
            if (isMechanite)
            {
                float thresholdY = plotArea.yMax - plotArea.height * MechanitePainThreshold;
                GUI.color = PainThresholdColor;
                Widgets.DrawLineHorizontal(plotArea.x, thresholdY, plotArea.width);
                // Label for the threshold (50% is where pain intensifies)
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(graphArea.x, thresholdY - 8f, leftMargin - 4f, 18f), "50%");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }

            // X-axis represents disease progress from 0% (start) to 100% (cure)
            // This matches the progress bar above the graph
            float currentProgress = disappearsComp.Progress; // 0 = just started, 1 = about to cure
            float nowX = plotArea.x + plotArea.width * currentProgress;

            // Draw "now" vertical line
            GUI.color = NowLineColor;
            Widgets.DrawLineVertical(nowX, plotArea.y, plotArea.height);
            GUI.color = Color.white;

            // Draw historical severity data
            if (history != null && history.TimeBasedDataPoints.Count >= 2)
            {
                // Filter to points that have valid duration data
                var relevantPoints = history.TimeBasedDataPoints
                    .Where(p => p.TotalDuration > 0)
                    .OrderBy(p => p.Tick)
                    .ToList();

                if (relevantPoints.Count >= 2)
                {
                    for (int i = 0; i < relevantPoints.Count - 1; i++)
                    {
                        var p1 = relevantPoints[i];
                        var p2 = relevantPoints[i + 1];

                        // Calculate progress for each point: how far through the disease was it?
                        float progress1 = 1f - ((float)p1.TicksRemaining / p1.TotalDuration);
                        float progress2 = 1f - ((float)p2.TicksRemaining / p2.TotalDuration);

                        Vector2 start = ProgressToGraphPoint(plotArea, progress1, p1.Severity);
                        Vector2 end = ProgressToGraphPoint(plotArea, progress2, p2.Severity);
                        Widgets.DrawLine(start, end, SeverityColor, 2f);
                    }
                }
            }

            // Draw current severity point
            float sevNowY = plotArea.yMax - plotArea.height * hediff.Severity;
            Vector2 sevNow = new Vector2(nowX, sevNowY);
            DrawPointMarker(sevNow, SeverityColor);

            // Draw projection line to end (at x = 100% progress)
            if (prognosis.IsValid && !float.IsNaN(prognosis.ProjectedFinalSeverity))
            {
                float endY = plotArea.yMax - plotArea.height * Mathf.Clamp01(prognosis.ProjectedFinalSeverity);
                Vector2 endPoint = new Vector2(plotArea.xMax, endY);
                Widgets.DrawLine(sevNow, endPoint, SeverityProjectionColor, 2f);

                // Show projected value at end (in the right margin area)
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = AxisColor;
                int projectedPct = (int)(prognosis.ProjectedFinalSeverity * 100);
                Widgets.Label(new Rect(plotArea.xMax + 3f, endY - 8f, 36f, 18f), $"{projectedPct}%");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }

            // Y-axis labels (positioned to align with plot area edges)
            Text.Font = GameFont.Tiny;
            GUI.color = AxisColor;
            Text.Anchor = TextAnchor.MiddleRight;
            // "100%" at top of plot area
            Widgets.Label(new Rect(graphArea.x, plotArea.y - 2f, leftMargin - 4f, 16f), "100%");
            // "0%" at bottom of plot area
            Widgets.Label(new Rect(graphArea.x, plotArea.yMax - 14f, leftMargin - 4f, 16f), "0%");
            Text.Anchor = TextAnchor.UpperLeft;

            // X-axis labels - show progress from start to cure
            Text.Anchor = TextAnchor.UpperCenter;
            // "Start" label at left edge
            Widgets.Label(new Rect(plotArea.x - 20f, plotArea.yMax + 1f, 40f, 16f), "DIPT_TBW_AxisStart".Translate());
            // "Cure" label at right end
            Widgets.Label(new Rect(plotArea.xMax - 20f, plotArea.yMax + 1f, 40f, 16f), "DIPT_TBW_AxisCure".Translate());
            // "Now" label under the now line (only if not too close to edges to avoid overlap)
            if (currentProgress > 0.15f && currentProgress < 0.85f)
            {
                Widgets.Label(new Rect(nowX - 20f, plotArea.yMax + 1f, 40f, 16f), "DIPT_Shared_Now".Translate());
            }
            Text.Anchor = TextAnchor.UpperLeft;

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private Vector2 ProgressToGraphPoint(Rect plotArea, float progress, float severity)
        {
            float x = plotArea.x + plotArea.width * Mathf.Clamp01(progress);
            float y = plotArea.yMax - plotArea.height * Mathf.Clamp01(severity);
            return new Vector2(x, y);
        }

        private void DrawPointMarker(Vector2 center, Color color)
        {
            Rect marker = new Rect(center.x - 3f, center.y - 3f, 6f, 6f);
            Widgets.DrawBoxSolid(marker, color);
        }

        private Color GetSeverityColor(float severity)
        {
            if (isMechanite)
            {
                // For mechanites, the key threshold is 0.5 (pain jumps from 20% to 60%)
                if (severity < 0.4f) return SafeColor;          // Well below pain threshold
                if (severity < MechanitePainThreshold) return WarningColor;  // Approaching threshold
                return DangerColor;                              // Above threshold (intense pain)
            }
            else
            {
                // For lethal diseases, color based on danger to life
                if (severity < 0.4f) return SafeColor;
                if (severity < 0.7f) return WarningColor;
                return DangerColor;
            }
        }

        private void DrawRecentTends(Rect rect, DiseaseHistory history)
        {
            Text.Font = GameFont.Tiny;
            float entryHeight = TinyFontHeight;

            if (history == null || history.TendingEvents.Count == 0)
            {
                GUI.color = AxisColor;
                Widgets.Label(rect, "DIPT_Shared_NoTendsRecorded".Translate());
                GUI.color = Color.white;
                return;
            }

            var recentTends = history.TendingEvents
                .OrderByDescending(t => t.Tick)
                .Take(MaxTendsToShow)
                .ToList();

            float yOffset = 0f;
            foreach (var tend in recentTends)
            {
                Rect entryRect = new Rect(rect.x, rect.y + yOffset, rect.width, entryHeight);
                DrawTendEntry(entryRect, tend);
                yOffset += entryHeight;
            }
        }

        private void DrawTendEntry(Rect rect, TendingEvent tend)
        {
            const float iconSize = 16f;
            const float iconPadding = 4f;

            // Draw medicine icon
            Rect iconRect = new Rect(rect.x + 2f, rect.y + (rect.height - iconSize) / 2f, iconSize, iconSize);
            Texture2D icon = NoMedsIcon;
            if (!string.IsNullOrEmpty(tend.MedicineDefName))
            {
                ThingDef medicineDef = DefDatabase<ThingDef>.GetNamedSilentFail(tend.MedicineDefName);
                if (medicineDef?.uiIcon != null)
                {
                    icon = medicineDef.uiIcon;
                }
            }
            if (icon != null)
            {
                GUI.DrawTexture(iconRect, icon);
            }

            // Draw text after icon
            string doctorInfo = tend.DoctorName;
            if (doctorInfo == "Self/Unknown" || string.IsNullOrEmpty(doctorInfo))
            {
                doctorInfo = "DIPT_Shared_SelfTend".Translate();
            }

            int qualityPct = (int)(tend.Quality * 100);
            string text = "DIPT_TBW_TendEntry".Translate($"{qualityPct}", doctorInfo);

            Rect textRect = new Rect(iconRect.xMax + iconPadding, rect.y, rect.width - iconSize - iconPadding - 4f, rect.height);
            Widgets.Label(textRect, text);
        }

        private void DrawVerdict(Rect rect, TimeBasedPrognosis prognosis)
        {
            Text.Font = GameFont.Small;

            string verdictText;
            Color verdictColor;

            if (!prognosis.IsValid)
            {
                verdictText = "DIPT_Shared_Calculating".Translate();
                verdictColor = AxisColor;
            }
            else if (isMechanite)
            {
                // For mechanites, verdict is about pain management, not survival
                // Mechanites are never fatal - they just cause pain
                float projectedSeverity = prognosis.ProjectedFinalSeverity;
                bool currentlyIntense = hediff.Severity >= MechanitePainThreshold;
                bool projectedIntense = projectedSeverity >= MechanitePainThreshold;

                if (!currentlyIntense && !projectedIntense)
                {
                    verdictText = "DIPT_TBW_VerdictSafePainControlled".Translate();
                    verdictColor = SafeColor;
                }
                else if (!currentlyIntense && projectedIntense)
                {
                    verdictText = "DIPT_TBW_VerdictWarningPainIntensify".Translate();
                    verdictColor = WarningColor;
                }
                else if (currentlyIntense && prognosis.SeverityChangePerDay < 0)
                {
                    verdictText = "DIPT_TBW_VerdictImprovingPainDecreasing".Translate();
                    verdictColor = SafeColor;
                }
                else
                {
                    verdictText = "DIPT_TBW_VerdictIntensePain".Translate();
                    verdictColor = DangerColor;
                }
            }
            else if (prognosis.WillSurvive)
            {
                int projectedPct = (int)(prognosis.ProjectedFinalSeverity * 100);
                verdictText = "DIPT_TBW_VerdictSafeWillSurvive".Translate($"{projectedPct}");
                verdictColor = SafeColor;
            }
            else if (prognosis.ProjectedFinalSeverity >= 0.9f)
            {
                verdictText = "DIPT_TBW_VerdictDanger".Translate();
                verdictColor = DangerColor;
            }
            else
            {
                verdictText = "DIPT_TBW_VerdictMonitor".Translate();
                verdictColor = WarningColor;
            }

            GUI.color = verdictColor;
            Widgets.Label(rect, verdictText);
            GUI.color = Color.white;
        }

        private struct TimeBasedPrognosis
        {
            public bool IsValid;
            public float CurrentSeverity;
            public float SeverityChangePerDay;
            public float DaysRemaining;
            public float ProjectedFinalSeverity;
            public bool WillSurvive;
        }

        private TimeBasedPrognosis CalculatePrognosis(DiseaseHistory history)
        {
            var prognosis = new TimeBasedPrognosis { IsValid = false };

            if (disappearsComp == null || hediff == null) return prognosis;

            prognosis.CurrentSeverity = hediff.Severity;
            prognosis.DaysRemaining = disappearsComp.ticksToDisappear / TicksPerDay;

            // Calculate severity change rate from historical data
            prognosis.SeverityChangePerDay = CalculateSeverityRate(history);

            // Project final severity when timer expires
            prognosis.ProjectedFinalSeverity = prognosis.CurrentSeverity + (prognosis.SeverityChangePerDay * prognosis.DaysRemaining);
            prognosis.ProjectedFinalSeverity = Mathf.Clamp(prognosis.ProjectedFinalSeverity, 0f, 1f);

            // Will survive if projected severity stays below 100%
            prognosis.WillSurvive = prognosis.ProjectedFinalSeverity < 1f;

            prognosis.IsValid = true;
            return prognosis;
        }

        private float CalculateSeverityRate(DiseaseHistory history)
        {
            // Try to calculate from historical data first
            if (history != null && history.TimeBasedDataPoints.Count >= 2)
            {
                var points = history.TimeBasedDataPoints;
                var latest = points[points.Count - 1];

                // Find a point at least 1 hour back
                const int minTicksForCalculation = 2500;
                TimeBasedDataPoint earlier = null;

                for (int i = points.Count - 2; i >= 0; i--)
                {
                    if (latest.Tick - points[i].Tick >= minTicksForCalculation)
                    {
                        earlier = points[i];
                        break;
                    }
                }

                if (earlier != null)
                {
                    int ticksElapsed = latest.Tick - earlier.Tick;
                    float daysElapsed = ticksElapsed / TicksPerDay;
                    float severityChange = latest.Severity - earlier.Severity;
                    return severityChange / daysElapsed;
                }
            }

            // Fall back to calculated rate from HediffComp_TendDuration if available
            if (tendComp != null)
            {
                return tendComp.SeverityChangePerDay();
            }

            // Default: assume stable
            return 0f;
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            // Close if the hediff we're tracking is no longer active in the tooltip
            bool tooltipActive = Patches.TimeBasedDiseasePatch.IsTooltipActiveFor(hediff);
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
                if (window is TimeBasedWindow tbWindow && tbWindow.hediff == hediff)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Close any existing window for a different hediff.
        /// </summary>
        public static void CloseOtherWindows(Hediff exceptFor)
        {
            var toClose = new List<Window>();
            foreach (var window in Find.WindowStack.Windows)
            {
                if (window is TimeBasedWindow tbWindow && tbWindow.hediff != exceptFor)
                {
                    toClose.Add(window);
                }
            }
            foreach (var window in toClose)
            {
                window.Close(false);
            }
        }
    }
}
