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
    /// A tooltip companion window that displays Type 5 (chronic) disease progress like Asthma.
    /// These diseases have no immunity gain; severity is managed through treatment quality.
    ///
    /// Treatment mechanics:
    /// - Untreated: severity increases at base rate (e.g., +0.25/day for asthma)
    /// - Tended: severity change = base rate + (tendEffect * tendQuality)
    /// - At threshold quality: severity is stable
    /// - Above threshold: severity regresses
    /// - Below threshold: severity still increases, but slower
    /// </summary>
    [StaticConstructorOnStartup]
    public class ChronicDiseaseWindow : Window, ICompanionWindow
    {
        private readonly Hediff hediff;
        public Hediff Hediff => hediff;

        private readonly HediffComp_Immunizable immunizableComp;
        private readonly HediffComp_TendDuration tendComp;

        // Layout constants
        private const float Padding = 10f;
        private const float ProgressBarHeight = 22f;
        private const float GraphHeight = 110f;
        private const int MaxTendsToShow = 3;

        // Text heights - measured from actual font metrics
        private static float SmallFontHeight => Text.LineHeightOf(GameFont.Small);
        private static float TinyFontHeight => Text.LineHeightOf(GameFont.Tiny);

        // Time constants
        private const float TicksPerDay = 60000f;
        private const float TicksPerHour = 2500f;

        // Colors
        private static readonly Color BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        private static readonly Color ProgressBarBgColor = new Color(0.2f, 0.2f, 0.2f);
        private static readonly Color ProgressBarFillColor = new Color(0.8f, 0.2f, 0.2f); // Red for severity
        private static readonly Color SeverityColor = new Color(0.8f, 0.2f, 0.2f); // Red
        private static readonly Color SeverityProjectionColor = new Color(0.8f, 0.2f, 0.2f, 0.5f);
        private static readonly Color AxisColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color GridColor = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color NowLineColor = new Color(1f, 1f, 1f, 0.5f);
        private static readonly Color SafeColor = new Color(0.4f, 1f, 0.4f);
        private static readonly Color WarningColor = new Color(1f, 0.9f, 0.2f);
        private static readonly Color DangerColor = new Color(1f, 0.6f, 0.2f);
        private static readonly Color TendedColor = new Color(0.3f, 0.8f, 0.3f);
        private static readonly Color NeedsTendingColor = new Color(0.9f, 0.6f, 0.2f);
        private static readonly Color ThresholdLineColorYellow = new Color(1f, 0.9f, 0.2f, 0.6f);
        private static readonly Color ThresholdLineColorOrange = new Color(1f, 0.6f, 0.2f, 0.6f);

        // Fallback icon for tending without medicine
        private static readonly Texture2D NoMedsIcon = ContentFinder<Texture2D>.Get("UI/Icons/Medical/NoMeds");

        public override Vector2 InitialSize => new Vector2(340f, CalculateWindowHeight());

        /// <summary>
        /// Calculate window height based on actual font metrics.
        /// </summary>
        private static float CalculateWindowHeight()
        {
            return Padding * 2                          // Top and bottom padding
                 + SmallFontHeight + 4f                 // Title + gap
                 + SmallFontHeight + 4f                 // Treatment status + gap
                 + SmallFontHeight + 4f                 // Severity display + gap
                 + ProgressBarHeight + 6f              // Severity bar + gap
                 + GraphHeight + 6f                     // Severity graph + gap
                 + TinyFontHeight + 2f                  // "Recent tends:" label + gap
                 + TinyFontHeight * MaxTendsToShow + 8f // Tend entries + gap
                 + SmallFontHeight * 2;                 // Verdict (2 lines for wrapped text)
        }

        protected override float Margin => 0f;

        public ChronicDiseaseWindow(Hediff hediff)
        {
            this.hediff = hediff;
            this.immunizableComp = hediff?.TryGetComp<HediffComp_Immunizable>();
            this.tendComp = hediff?.TryGetComp<HediffComp_TendDuration>();

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
            string title = hediff?.Label?.CapitalizeFirst() ?? "DIPT_CDW_FallbackTitle".Translate();
            Widgets.Label(new Rect(contentRect.x, yOffset, contentWidth, smallHeight), title);
            yOffset += smallHeight + 4f;

            // Draw treatment status
            DrawTreatmentStatus(new Rect(contentRect.x, yOffset, contentWidth, smallHeight), prognosis);
            yOffset += smallHeight + 4f;

            // Draw severity section
            DrawSeverityDisplay(new Rect(contentRect.x, yOffset, contentWidth, smallHeight));
            yOffset += smallHeight + 4f;

            // Draw severity bar
            DrawSeverityBar(new Rect(contentRect.x, yOffset, contentWidth, ProgressBarHeight));
            yOffset += ProgressBarHeight + 6f;

            // Draw severity graph
            DrawSeverityGraph(new Rect(contentRect.x, yOffset, contentWidth, GraphHeight), history, prognosis);
            yOffset += GraphHeight + 6f;

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

        private void DrawTreatmentStatus(Rect rect, ChronicPrognosis prognosis)
        {
            Text.Font = GameFont.Small;

            bool isTended = tendComp != null && tendComp.IsTended;

            if (!isTended)
            {
                // Not tended
                GUI.color = NeedsTendingColor;
                Widgets.Label(rect, "DIPT_CDW_NotTreatedProgressing".Translate());
                GUI.color = Color.white;
            }
            else
            {
                // Tended - show quality and effect
                float tendQuality = tendComp.tendQuality;
                int qualityPct = (int)(tendQuality * 100);

                string effectText;
                Color effectColor;

                if (prognosis.SeverityChangePerDay <= -0.01f)
                {
                    effectText = "DIPT_CDW_EffectRegressing".Translate();
                    effectColor = SafeColor;
                }
                else if (prognosis.SeverityChangePerDay >= 0.01f)
                {
                    effectText = "DIPT_CDW_EffectSlowing".Translate();
                    effectColor = WarningColor;
                }
                else
                {
                    effectText = "DIPT_CDW_EffectStable".Translate();
                    effectColor = TendedColor;
                }

                // Show quality and effect, with time remaining
                string timeText = "";
                if (tendComp.tendTicksLeft > 0)
                {
                    float hoursLeft = tendComp.tendTicksLeft / TicksPerHour;
                    float daysLeft = tendComp.tendTicksLeft / TicksPerDay;
                    if (daysLeft >= 1f)
                    {
                        timeText = $" ({daysLeft:0.#}d left)";
                    }
                    else
                    {
                        timeText = $" ({hoursLeft:0}h left)";
                    }
                }

                GUI.color = effectColor;
                Widgets.Label(rect, "DIPT_CDW_Treatment".Translate($"{qualityPct}", effectText, timeText));
                GUI.color = Color.white;
            }
        }

        private void DrawSeverityDisplay(Rect rect)
        {
            Text.Font = GameFont.Small;
            float maxSeverity = hediff.def.maxSeverity > 0 ? hediff.def.maxSeverity : 1f;
            int severityPct = (int)(hediff.Severity * 100);
            int maxPct = (int)(maxSeverity * 100);

            Color severityColor = GetSeverityColor(hediff.Severity, maxSeverity);
            GUI.color = severityColor;
            Widgets.Label(rect, "DIPT_CDW_SeverityWithMax".Translate($"{severityPct}", $"{maxPct}"));
            GUI.color = Color.white;
        }

        private void DrawSeverityBar(Rect rect)
        {
            float maxSeverity = hediff.def.maxSeverity > 0 ? hediff.def.maxSeverity : 1f;

            // Background
            Widgets.DrawBoxSolid(rect, ProgressBarBgColor);

            // Fill based on severity relative to max
            float fillRatio = Mathf.Clamp01(hediff.Severity / maxSeverity);
            float fillWidth = rect.width * fillRatio;
            if (fillWidth > 0)
            {
                Color fillColor = GetSeverityColor(hediff.Severity, maxSeverity);
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, fillWidth, rect.height), fillColor);
            }

            // Draw stage threshold markers (for asthma: 30% and 45%)
            // Check hediff stages for threshold values
            if (hediff.def.stages != null && hediff.def.stages.Count > 1)
            {
                for (int i = 1; i < hediff.def.stages.Count; i++)
                {
                    float threshold = hediff.def.stages[i].minSeverity;
                    if (threshold > 0 && threshold < maxSeverity)
                    {
                        float thresholdX = rect.x + rect.width * (threshold / maxSeverity);
                        // Draw thin vertical line
                        GUI.color = (i == 1) ? ThresholdLineColorYellow : ThresholdLineColorOrange;
                        Widgets.DrawLineVertical(thresholdX, rect.y, rect.height);
                        GUI.color = Color.white;
                    }
                }
            }

            // Border
            Widgets.DrawBox(rect);

            // Percentage text centered
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            int severityPct = (int)(hediff.Severity * 100);
            Widgets.Label(rect, "DIPT_CDW_PercentComplete".Translate($"{severityPct}"));
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawSeverityGraph(Rect graphArea, DiseaseHistory history, ChronicPrognosis prognosis)
        {
            float maxSeverity = hediff.def.maxSeverity > 0 ? hediff.def.maxSeverity : 1f;
            // Use a display scale 1.5x the max severity (e.g., 75% for asthma's 50% max)
            // This gives more room for threshold labels and avoids overlap
            float displayMaxSeverity = Mathf.Min(maxSeverity * 1.5f, 1f);

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

            // Draw axes
            GUI.color = AxisColor;
            Widgets.DrawLineVertical(plotArea.x, plotArea.y, plotArea.height);
            Widgets.DrawLineHorizontal(plotArea.x, plotArea.yMax, plotArea.width);
            GUI.color = Color.white;

            // Draw stage threshold lines (horizontal)
            if (hediff.def.stages != null && hediff.def.stages.Count > 1)
            {
                for (int i = 1; i < hediff.def.stages.Count; i++)
                {
                    float threshold = hediff.def.stages[i].minSeverity;
                    if (threshold > 0 && threshold < displayMaxSeverity)
                    {
                        float thresholdY = plotArea.yMax - plotArea.height * (threshold / displayMaxSeverity);
                        Color lineColor = (i == 1) ? ThresholdLineColorYellow : ThresholdLineColorOrange;
                        GUI.color = lineColor;
                        Widgets.DrawLineHorizontal(plotArea.x, thresholdY, plotArea.width);

                        // Label for the threshold
                        Text.Font = GameFont.Tiny;
                        Text.Anchor = TextAnchor.MiddleRight;
                        int thresholdPct = (int)(threshold * 100);
                        Widgets.Label(new Rect(graphArea.x, thresholdY - 8f, leftMargin - 4f, 18f), $"{thresholdPct}%");
                        Text.Anchor = TextAnchor.UpperLeft;
                        GUI.color = Color.white;
                    }
                }
            }

            // Time window: show last 2 days and project 2 days ahead
            const float pastDays = 2f;
            const float futureDays = 2f;
            const float totalDays = pastDays + futureDays;
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
                            plotArea.yMax - plotArea.height * (p1.Severity / displayMaxSeverity)
                        );
                        Vector2 end = new Vector2(
                            plotArea.x + plotArea.width * xRatio2,
                            plotArea.yMax - plotArea.height * (p2.Severity / displayMaxSeverity)
                        );
                        Widgets.DrawLine(start, end, SeverityColor, 2f);
                    }
                }
            }

            // Draw current severity point
            float sevNowY = plotArea.yMax - plotArea.height * (hediff.Severity / displayMaxSeverity);
            Vector2 sevNow = new Vector2(nowX, sevNowY);
            DrawPointMarker(sevNow, SeverityColor);

            // Draw projection line
            if (prognosis.IsValid)
            {
                float projectedSeverity = hediff.Severity + (prognosis.SeverityChangePerDay * futureDays);
                projectedSeverity = Mathf.Clamp(projectedSeverity, hediff.def.minSeverity, maxSeverity);

                float endY = plotArea.yMax - plotArea.height * (projectedSeverity / displayMaxSeverity);
                Vector2 endPoint = new Vector2(plotArea.xMax, endY);
                Widgets.DrawLine(sevNow, endPoint, SeverityProjectionColor, 2f);

                // Show projected value at end
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = AxisColor;
                int projectedPct = (int)(projectedSeverity * 100);
                Widgets.Label(new Rect(plotArea.xMax + 3f, endY - 8f, 36f, 18f), $"{projectedPct}%");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }

            // Y-axis labels - show display max (e.g., 75%) at top, 0% at bottom
            Text.Font = GameFont.Tiny;
            GUI.color = AxisColor;
            Text.Anchor = TextAnchor.MiddleRight;
            int displayMaxPct = (int)(displayMaxSeverity * 100);
            Widgets.Label(new Rect(graphArea.x, plotArea.y - 2f, leftMargin - 4f, 16f), $"{displayMaxPct}%");
            Widgets.Label(new Rect(graphArea.x, plotArea.yMax - 14f, leftMargin - 4f, 16f), "0%");
            Text.Anchor = TextAnchor.UpperLeft;

            // X-axis labels
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(plotArea.x - 15f, plotArea.yMax + 1f, 30f, 16f), "DIPT_Shared_AxisPast".Translate("2"));
            Widgets.Label(new Rect(nowX - 15f, plotArea.yMax + 1f, 30f, 16f), "DIPT_Shared_Now".Translate());
            Widgets.Label(new Rect(plotArea.xMax - 15f, plotArea.yMax + 1f, 30f, 16f), "DIPT_Shared_AxisFuture".Translate("2"));
            Text.Anchor = TextAnchor.UpperLeft;

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawPointMarker(Vector2 center, Color color)
        {
            Rect marker = new Rect(center.x - 3f, center.y - 3f, 6f, 6f);
            Widgets.DrawBoxSolid(marker, color);
        }

        private Color GetSeverityColor(float severity, float maxSeverity)
        {
            float ratio = severity / maxSeverity;
            if (ratio < 0.5f) return SafeColor;
            if (ratio < 0.8f) return WarningColor;
            return DangerColor;
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

            // Calculate the regression threshold for this disease
            float regressionThreshold = CalculateRegressionThreshold();
            int qualityPct = (int)(tend.Quality * 100);
            int thresholdPct = (int)(regressionThreshold * 100);

            // Show effect relative to threshold
            string effectHint = "";
            if (tend.Quality >= regressionThreshold)
            {
                effectHint = "DIPT_CDW_HintRegress".Translate();
            }
            else if (tend.Quality >= regressionThreshold * 0.5f)
            {
                effectHint = "DIPT_CDW_HintSlow".Translate();
            }
            else
            {
                effectHint = "DIPT_CDW_HintMinimal".Translate();
            }

            string doctorInfo = tend.DoctorName;
            if (doctorInfo == "Self/Unknown" || string.IsNullOrEmpty(doctorInfo))
            {
                doctorInfo = "DIPT_Shared_SelfTend".Translate();
            }

            string text = "DIPT_CDW_TendEntry".Translate($"{qualityPct}", effectHint, doctorInfo);

            Rect textRect = new Rect(iconRect.xMax + iconPadding, rect.y, rect.width - iconSize - iconPadding - 4f, rect.height);
            Widgets.Label(textRect, text);
        }

        private void DrawVerdict(Rect rect, ChronicPrognosis prognosis)
        {
            Text.Font = GameFont.Small;

            string verdictText;
            Color verdictColor;

            if (!prognosis.IsValid)
            {
                verdictText = "DIPT_Shared_Calculating".Translate();
                verdictColor = AxisColor;
            }
            else
            {
                float regressionThreshold = CalculateRegressionThreshold();
                int thresholdPct = (int)(regressionThreshold * 100);

                if (prognosis.SeverityChangePerDay <= -0.1f)
                {
                    verdictText = "DIPT_CDW_VerdictRegressing".Translate($"{prognosis.SeverityChangePerDay * 100:+0;-0}");
                    verdictColor = SafeColor;
                }
                else if (prognosis.SeverityChangePerDay <= -0.01f)
                {
                    verdictText = "DIPT_CDW_VerdictImproving".Translate($"{prognosis.SeverityChangePerDay * 100:+0;-0}");
                    verdictColor = SafeColor;
                }
                else if (prognosis.SeverityChangePerDay < 0.01f)
                {
                    verdictText = "DIPT_CDW_VerdictStable".Translate($"{thresholdPct}");
                    verdictColor = TendedColor;
                }
                else if (tendComp != null && tendComp.IsTended)
                {
                    verdictText = "DIPT_CDW_VerdictSlowing".Translate($"{thresholdPct}");
                    verdictColor = WarningColor;
                }
                else
                {
                    verdictText = "DIPT_CDW_VerdictProgressing".Translate($"{thresholdPct}");
                    verdictColor = DangerColor;
                }
            }

            GUI.color = verdictColor;
            // Use a taller rect to allow text wrapping
            Rect verdictRect = new Rect(rect.x, rect.y, rect.width, SmallFontHeight * 2);
            Widgets.Label(verdictRect, verdictText);
            GUI.color = Color.white;
        }

        /// <summary>
        /// Calculate the treatment quality threshold at which severity becomes stable.
        /// For asthma: severityPerDayNotImmune = 0.25, severityPerDayTended = -0.8
        /// Threshold = -severityPerDayNotImmune / severityPerDayTended = 0.25 / 0.8 = 0.3125 (32%)
        /// </summary>
        private float CalculateRegressionThreshold()
        {
            if (immunizableComp == null || tendComp == null) return 0.32f; // Default

            float severityPerDayNotImmune = immunizableComp.Props.severityPerDayNotImmune;
            float severityPerDayTended = tendComp.TProps.severityPerDayTended;

            if (severityPerDayTended >= 0) return 1f; // Can't regress if tending doesn't reduce severity

            // Threshold = -severityPerDayNotImmune / severityPerDayTended
            float threshold = -severityPerDayNotImmune / severityPerDayTended;
            return Mathf.Clamp01(threshold);
        }

        private struct ChronicPrognosis
        {
            public bool IsValid;
            public float CurrentSeverity;
            public float SeverityChangePerDay;
            public bool IsTended;
            public float TendQuality;
        }

        private ChronicPrognosis CalculatePrognosis(DiseaseHistory history)
        {
            var prognosis = new ChronicPrognosis { IsValid = false };

            if (hediff == null || immunizableComp == null) return prognosis;

            prognosis.CurrentSeverity = hediff.Severity;
            prognosis.IsTended = tendComp != null && tendComp.IsTended;
            prognosis.TendQuality = prognosis.IsTended ? tendComp.tendQuality : 0f;

            // Calculate severity change rate
            // Try observed rate from history first
            prognosis.SeverityChangePerDay = CalculateSeverityRate(history);

            prognosis.IsValid = true;
            return prognosis;
        }

        private float CalculateSeverityRate(DiseaseHistory history)
        {
            // Try to calculate from historical data first
            if (history != null && history.DataPoints.Count >= 2)
            {
                var points = history.DataPoints;
                var latest = points[points.Count - 1];

                const int minTicksForCalculation = 2500; // ~1 hour
                DiseaseDataPoint earlier = null;

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

            // Fall back to calculated rate from components
            float rate = 0f;

            // Base rate from HediffComp_Immunizable
            if (immunizableComp != null)
            {
                rate += immunizableComp.SeverityChangePerDay();
            }

            // Tending effect from HediffComp_TendDuration
            if (tendComp != null)
            {
                rate += tendComp.SeverityChangePerDay();
            }

            return rate;
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            // Close if the hediff we're tracking is no longer active in the tooltip
            bool tooltipActive = Patches.ChronicDiseasePatch.IsTooltipActiveFor(hediff);
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
                if (window is ChronicDiseaseWindow cdWindow && cdWindow.hediff == hediff)
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
                if (window is ChronicDiseaseWindow cdWindow && cdWindow.hediff != exceptFor)
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
