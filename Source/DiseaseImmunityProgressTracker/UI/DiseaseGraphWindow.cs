using System.Collections.Generic;
using UnityEngine;
using Verse;
using DiseaseImmunityProgressTracker.Core;

namespace DiseaseImmunityProgressTracker.UI
{
    /// <summary>
    /// A tooltip companion window that displays a graphical timeline of disease progression vs immunity gain.
    /// Opens automatically when hovering over an immunizable disease in the health tab.
    /// </summary>
    [StaticConstructorOnStartup]
    public class DiseaseGraphWindow : Window, ICompanionWindow
    {
        private readonly Hediff hediff;
        public Hediff Hediff => hediff;

        // Cached prognosis (recalculated each frame)
        private PrognosisCalculator.PrognosisResult prognosis;

        // Graph layout constants
        private const float GraphPadding = 10f;
        private const float AxisLabelWidth = 36f;
        private const float TendInfoHeight = 24f; // Space below x-axis for medicine icons and quality

        // Text heights - measured from actual font metrics
        private static float SmallFontHeight => Text.LineHeightOf(GameFont.Small);
        private static float TinyFontHeight => Text.LineHeightOf(GameFont.Tiny);

        // Time window for the graph (in days)
        private const float DefaultPastDays = 1f;
        private const float DefaultFutureDays = 2f;
        private const float MaxFutureDays = 5f;

        // Colors
        private static readonly Color BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        private static readonly Color AxisColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color GridColor = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color ImmunityColor = new Color(0.2f, 0.8f, 0.2f); // Green
        private static readonly Color SeverityColor = new Color(0.8f, 0.2f, 0.2f); // Red
        private static readonly Color NowLineColor = new Color(1f, 1f, 1f, 0.5f);
        private static readonly Color ProjectionDimFactor = new Color(0.7f, 0.7f, 0.7f, 0.7f);
        private static readonly Color SurviveColor = new Color(0.4f, 1f, 0.4f);
        private static readonly Color RiskColor = new Color(1f, 0.4f, 0.4f);
        // Bed rest colors - base (sleeping spot, factor=1.0) to enhanced (hospital bed + vitals, factor~1.15)
        private static readonly Color BedRestColorMin = new Color(0.2f, 0.3f, 0.5f, 0.15f); // Dim blue for no bonus
        private static readonly Color BedRestColorMax = new Color(0.3f, 0.5f, 1.0f, 0.35f); // Bright blue for good bonus
        private static readonly Color TendingLineColor = new Color(1f, 0.9f, 0.2f, 0.7f); // Yellowish

        // Fallback icon for tending without medicine
        private static readonly Texture2D NoMedsIcon = ContentFinder<Texture2D>.Get("UI/Icons/Medical/NoMeds");

        public override Vector2 InitialSize => new Vector2(380f, CalculateWindowHeight());

        /// <summary>
        /// Calculate window height based on actual font metrics.
        /// </summary>
        private static float CalculateWindowHeight()
        {
            const float graphHeight = 140f; // Fixed graph area height
            return SmallFontHeight                     // Title
                 + TinyFontHeight + 5f                 // Verdict + gap
                 + graphHeight                         // Graph area
                 + TinyFontHeight                      // X-axis labels
                 + TendInfoHeight                      // Medicine icons and quality
                 + GraphPadding;                       // Bottom padding
        }

        public DiseaseGraphWindow(Hediff hediff)
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
            // Use stacked positioning to avoid overlap with other companion windows
            windowRect = CompanionWindowManager.CalculateStackedWindowRect(InitialSize, this);
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Position/Size update
            Rect newRect = CompanionWindowManager.CalculateStackedWindowRect(InitialSize, this);
            windowRect.x = newRect.x;
            windowRect.y = newRect.y;

            // Get history and calculate prognosis using observed rates if available
            var tracker = DiseaseTracker.Instance;
            var history = tracker?.GetOrCreateHistory(hediff);
            prognosis = PrognosisCalculator.Calculate(hediff, history);

            // Draw title
            Text.Font = GameFont.Small;
            float smallHeight = SmallFontHeight;
            float tinyHeight = TinyFontHeight;
            string title = hediff?.Label?.CapitalizeFirst() ?? "Disease";
            Widgets.Label(new Rect(0, 0, inRect.width, smallHeight), title);

            // Draw verdict
            DrawVerdict(new Rect(0, smallHeight, inRect.width, tinyHeight));

            // Calculate graph area (below title and verdict, with padding for axis labels and tend info)
            float graphTop = smallHeight + tinyHeight + 5f;
            Rect graphArea = new Rect(
                AxisLabelWidth,
                graphTop,
                inRect.width - AxisLabelWidth - GraphPadding,
                inRect.height - graphTop - tinyHeight - TendInfoHeight - GraphPadding
            );

            // Draw graph background
            Widgets.DrawBoxSolid(graphArea, BackgroundColor);

            // Calculate time window
            float futureDays = CalculateFutureDays();
            float pastDays = DefaultPastDays;

            // Draw bed rest background (behind grid)
            DrawBedRestIntervals(graphArea, history, pastDays, futureDays);
            
            // Draw grid lines
            DrawGridLines(graphArea);

            // Draw axes
            DrawAxes(graphArea);

            // Draw axis labels with time info
            DrawAxisLabels(graphArea, pastDays, futureDays);

            // Draw trend lines with real data
            DrawTrendLines(graphArea, pastDays, futureDays);

            // Draw legend (bottom-right to avoid overlap with high trend lines)
            DrawLegend(new Rect(graphArea.xMax - 120f, graphArea.yMax - 30f, 150f, 45f));
        }

        private void DrawVerdict(Rect rect)
        {
            Text.Font = GameFont.Tiny;

            if (!prognosis.IsValid)
            {
                GUI.color = AxisColor;
                Widgets.Label(rect, "Unable to calculate prognosis");
                GUI.color = Color.white;
                return;
            }

            string verdictText;
            Color verdictColor;

            // Already immune
            if (prognosis.CurrentImmunity >= 1f)
            {
                if (prognosis.SeverityPerDay < 0f && prognosis.CurrentSeverity > 0f
                    && !float.IsInfinity(prognosis.DaysUntilSeverityCleared))
                {
                    verdictText = $"Immune - All clear in {FormatDays(prognosis.DaysUntilSeverityCleared)}";
                }
                else
                {
                    verdictText = "Immune";
                }
                verdictColor = SurviveColor;
            }
            // Severity not increasing (stable or recovering)
            else if (prognosis.SeverityPerDay <= 0f)
            {
                if (prognosis.ImmunityPerDay > 0f && !float.IsInfinity(prognosis.DaysUntilImmune))
                {
                    verdictText = $"Recovering - Immune in {FormatDays(prognosis.DaysUntilImmune)}";
                }
                else
                {
                    verdictText = "Stable";
                }
                verdictColor = SurviveColor;
            }
            // Immunity not increasing (at risk)
            else if (prognosis.ImmunityPerDay <= 0f)
            {
                if (!float.IsInfinity(prognosis.DaysUntilDeath))
                {
                    verdictText = $"At risk - Death in {FormatDays(prognosis.DaysUntilDeath)}";
                }
                else
                {
                    verdictText = "At risk";
                }
                verdictColor = RiskColor;
            }
            // Race between immunity and severity
            else if (prognosis.WillSurvive)
            {
                // Will survive - show when immune and margin (time + percent)
                // Calculate severity at the moment immunity hits 100%
                float severityAtImmunity = prognosis.CurrentSeverity + prognosis.SeverityPerDay * prognosis.DaysUntilImmune;
                float marginPercent = 1f - severityAtImmunity; // How far below 100% severity will be

                string marginInfo = "";
                if (!float.IsInfinity(prognosis.MarginDays) && prognosis.MarginDays > 0 && marginPercent > 0)
                {
                    marginInfo = $" ({FormatDays(prognosis.MarginDays)}/{marginPercent * 100:0}% margin)";
                }
                verdictText = $"Will survive - Immune in {FormatDays(prognosis.DaysUntilImmune)}{marginInfo}";
                verdictColor = SurviveColor;
            }
            else
            {
                // Will die - show when death occurs and how short they fall (time + percent)
                // Calculate immunity at the moment severity hits 100%
                float immunityAtDeath = prognosis.CurrentImmunity + prognosis.ImmunityPerDay * prognosis.DaysUntilDeath;
                float shortfallPercent = 1f - immunityAtDeath; // How far below 100% immunity will be

                float shortfallDays = prognosis.DaysUntilImmune - prognosis.DaysUntilDeath;
                string shortfallInfo = "";
                if (!float.IsInfinity(shortfallDays) && shortfallDays > 0 && shortfallPercent > 0)
                {
                    shortfallInfo = $" ({FormatDays(shortfallDays)}/{shortfallPercent * 100:0}% short)";
                }
                verdictText = $"Death in {FormatDays(prognosis.DaysUntilDeath)}{shortfallInfo}";
                verdictColor = RiskColor;
            }

            GUI.color = verdictColor;
            Widgets.Label(rect, verdictText);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private float CalculateFutureDays()
        {
            if (!prognosis.IsValid) return DefaultFutureDays;

            // Show enough future to see the crossover point
            float maxRelevant = Mathf.Max(
                float.IsInfinity(prognosis.DaysUntilImmune) ? 0 : prognosis.DaysUntilImmune,
                float.IsInfinity(prognosis.DaysUntilDeath) ? 0 : prognosis.DaysUntilDeath
            );

            // Add some padding and clamp
            float futureDays = Mathf.Max(DefaultFutureDays, maxRelevant * 1.2f);
            return Mathf.Min(futureDays, MaxFutureDays);
        }

        private void DrawGridLines(Rect graphArea)
        {
            Color oldColor = GUI.color;
            GUI.color = GridColor;

            // Horizontal grid lines at 25%, 50%, 75%
            for (int i = 1; i <= 3; i++)
            {
                float y = graphArea.yMax - (graphArea.height * i / 4f);
                Widgets.DrawLineHorizontal(graphArea.x, y, graphArea.width);
            }

            // Vertical grid lines (time markers)
            for (int i = 1; i <= 3; i++)
            {
                float x = graphArea.x + (graphArea.width * i / 4f);
                Widgets.DrawLineVertical(x, graphArea.y, graphArea.height);
            }

            GUI.color = oldColor;
        }

        private void DrawAxes(Rect graphArea)
        {
            Color oldColor = GUI.color;
            GUI.color = AxisColor;

            // X-axis (bottom)
            Widgets.DrawLineHorizontal(graphArea.x, graphArea.yMax, graphArea.width);

            // Y-axis (left)
            Widgets.DrawLineVertical(graphArea.x, graphArea.y, graphArea.height);

            GUI.color = oldColor;
        }

        private void DrawAxisLabels(Rect graphArea, float pastDays, float futureDays)
        {
            Text.Font = GameFont.Tiny;
            Color oldColor = GUI.color;
            GUI.color = AxisColor;

            // Y-axis labels (percentages)
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(0, graphArea.y - 8f, AxisLabelWidth - 4f, 16f), "100%");
            Widgets.Label(new Rect(0, graphArea.y + graphArea.height / 2f - 8f, AxisLabelWidth - 4f, 16f), "50%");
            Widgets.Label(new Rect(0, graphArea.yMax - 8f, AxisLabelWidth - 4f, 16f), "0%");

            // X-axis labels (time) - calculate "now" position
            float totalDays = pastDays + futureDays;
            float nowRatio = pastDays / totalDays;
            float nowX = graphArea.x + graphArea.width * nowRatio;

            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(graphArea.x - 15f, graphArea.yMax + 2f, 30f, 16f), $"-{pastDays:0.#}d");
            Widgets.Label(new Rect(nowX - 15f, graphArea.yMax + 2f, 30f, 16f), "Now");
            Widgets.Label(new Rect(graphArea.xMax - 35f, graphArea.yMax + 2f, 45f, 16f), $"+{futureDays:0.#}d");

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = oldColor;
            Text.Font = GameFont.Small;
        }

        private void DrawTrendLines(Rect graphArea, float pastDays, float futureDays)
        {
            if (!prognosis.IsValid)
            {
                // Fall back to showing current state only
                return;
            }

            float totalDays = pastDays + futureDays;
            float nowRatio = pastDays / totalDays;
            float nowX = graphArea.x + graphArea.width * nowRatio;

            // Draw "now" vertical line
            Color oldColor = GUI.color;
            GUI.color = NowLineColor;
            Widgets.DrawLineVertical(nowX, graphArea.y, graphArea.height);
            GUI.color = oldColor;

            // Get historical data if available
            var tracker = DiseaseTracker.Instance;
            var history = tracker?.GetOrCreateHistory(hediff);
            int currentTick = Find.TickManager.TicksGame;

            // Draw historical data (past)
            DrawHistoricalLines(graphArea, history, currentTick, pastDays, totalDays);

            // Draw current point and projection (future)
            DrawProjectionLines(graphArea, nowX, pastDays, futureDays, totalDays);

            // Draw tending markers (overlay)
            DrawTendingMarkers(graphArea, history, currentTick, pastDays, totalDays);
        }

        private void DrawBedRestIntervals(Rect graphArea, DiseaseHistory history, float pastDays, float futureDays)
        {
            if (history == null || history.BedRestIntervals.Count == 0) return;

            int currentTick = Find.TickManager.TicksGame;
            float totalDays = pastDays + futureDays;
            int pastTicks = (int)(pastDays * PrognosisCalculator.TicksPerDay);
            int windowStart = currentTick - pastTicks;

            foreach (var interval in history.BedRestIntervals)
            {
                // Skip if interval ends before window starts
                int endTick = interval.EndTick == -1 ? currentTick : interval.EndTick;
                if (endTick < windowStart) continue;

                // Skip if interval starts after current time (shouldn't happen for history)
                if (interval.StartTick > currentTick) continue;

                // Clamp to window
                int visibleStart = Mathf.Max(interval.StartTick, windowStart);
                int visibleEnd = Mathf.Min(endTick, currentTick);

                if (visibleStart >= visibleEnd) continue;

                float startX = TickToX(graphArea, visibleStart, currentTick, pastDays, totalDays);
                float endX = TickToX(graphArea, visibleEnd, currentTick, pastDays, totalDays);

                // Calculate color based on immunity gain speed factor
                // Factor of 1.0 = no bonus (dim), factor of 1.15+ = great bonus (bright)
                float factor = interval.ImmunityGainSpeedFactor;
                float t = Mathf.Clamp01((factor - 1f) / 0.15f); // 0 at factor=1.0, 1 at factor=1.15+
                Color bedColor = Color.Lerp(BedRestColorMin, BedRestColorMax, t);

                // Draw rectangle
                Rect rect = new Rect(startX, graphArea.y, endX - startX, graphArea.height);
                Widgets.DrawBoxSolid(rect, bedColor);
            }
        }

        private void DrawTendingMarkers(Rect graphArea, DiseaseHistory history, int currentTick, float pastDays, float totalDays)
        {
            if (history == null) return;

            if (history.TendingEvents.Count == 0) return;

            int pastTicks = (int)(pastDays * PrognosisCalculator.TicksPerDay);
            int windowStart = currentTick - pastTicks;

            // Y position for tend info (below x-axis labels)
            float tendInfoY = graphArea.yMax + TinyFontHeight;

            foreach (var tend in history.TendingEvents)
            {
                if (tend.Tick < windowStart || tend.Tick > currentTick) continue;

                float x = TickToX(graphArea, tend.Tick, currentTick, pastDays, totalDays);

                // Draw vertical line through the graph
                Color oldColor = GUI.color;
                GUI.color = TendingLineColor;
                Widgets.DrawLineVertical(x, graphArea.y, graphArea.height);
                GUI.color = oldColor;

                // Draw medicine icon below x-axis
                const float iconSize = 18f;
                Rect iconRect = new Rect(x - iconSize / 2f, tendInfoY, iconSize, iconSize);

                // Look up the ThingDef for the medicine icon, or use NoMeds icon as fallback
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

                // Draw quality percentage below the icon
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperCenter;
                string qualityText = $"{tend.Quality * 100:0}%";
                Rect qualityRect = new Rect(x - 20f, tendInfoY + iconSize - 2f, 40f, 16f);
                Widgets.Label(qualityRect, qualityText);
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }
        }

        private float TickToX(Rect graphArea, int tick, int currentTick, float pastDays, float totalDays)
        {
            float dayOffset = (tick - currentTick) / PrognosisCalculator.TicksPerDay;
            float xRatio = (pastDays + dayOffset) / totalDays;
            return graphArea.x + graphArea.width * xRatio;
        }

        private void DrawHistoricalLines(Rect graphArea, DiseaseHistory history, int currentTick, float pastDays, float totalDays)
        {
            if (history == null || history.DataPoints.Count < 2) return;

            int pastTicks = (int)(pastDays * PrognosisCalculator.TicksPerDay);
            int windowStart = currentTick - pastTicks;

            // Find points within our window
            var relevantPoints = new List<DiseaseDataPoint>();
            foreach (var point in history.DataPoints)
            {
                if (point.Tick >= windowStart && point.Tick <= currentTick)
                {
                    relevantPoints.Add(point);
                }
            }

            // Add a synthetic "now" point using live values so the historical line
            // connects seamlessly to the current position and projection line.
            // Without this, the last recorded point (up to ~1 hour old) leaves a gap.
            if (prognosis.IsValid)
            {
                relevantPoints.Add(new DiseaseDataPoint
                {
                    Tick = currentTick,
                    Immunity = prognosis.CurrentImmunity,
                    Severity = prognosis.CurrentSeverity
                });
            }

            if (relevantPoints.Count < 2) return;

            // Draw immunity history
            for (int i = 0; i < relevantPoints.Count - 1; i++)
            {
                var p1 = relevantPoints[i];
                var p2 = relevantPoints[i + 1];

                Vector2 start = TickToGraphPoint(graphArea, p1.Tick, p1.Immunity, currentTick, pastDays, totalDays);
                Vector2 end = TickToGraphPoint(graphArea, p2.Tick, p2.Immunity, currentTick, pastDays, totalDays);
                Widgets.DrawLine(start, end, ImmunityColor, 2f);
            }

            // Draw severity history
            for (int i = 0; i < relevantPoints.Count - 1; i++)
            {
                var p1 = relevantPoints[i];
                var p2 = relevantPoints[i + 1];

                Vector2 start = TickToGraphPoint(graphArea, p1.Tick, p1.Severity, currentTick, pastDays, totalDays);
                Vector2 end = TickToGraphPoint(graphArea, p2.Tick, p2.Severity, currentTick, pastDays, totalDays);
                Widgets.DrawLine(start, end, SeverityColor, 2f);
            }
        }

        private Vector2 TickToGraphPoint(Rect graphArea, int tick, float value, int currentTick, float pastDays, float totalDays)
        {
            float dayOffset = (tick - currentTick) / PrognosisCalculator.TicksPerDay;
            float xRatio = (pastDays + dayOffset) / totalDays;
            float x = graphArea.x + graphArea.width * xRatio;
            float y = graphArea.yMax - graphArea.height * value;
            return new Vector2(x, y);
        }

        private void DrawProjectionLines(Rect graphArea, float nowX, float pastDays, float futureDays, float totalDays)
        {
            // Current point positions
            float immNowY = graphArea.yMax - graphArea.height * prognosis.CurrentImmunity;
            float sevNowY = graphArea.yMax - graphArea.height * prognosis.CurrentSeverity;
            Vector2 immNow = new Vector2(nowX, immNowY);
            Vector2 sevNow = new Vector2(nowX, sevNowY);

            // Draw projections only while the race is still active
            // Once immune, the historical line shows the decline clearly and
            // the verdict text shows "All clear in X" — no projection needed.
            bool outcomeResolved = prognosis.CurrentImmunity >= 1f || prognosis.CurrentSeverity >= 1f;

            if (!outcomeResolved)
            {
                Color immProjectionColor = ImmunityColor * ProjectionDimFactor;
                Color sevProjectionColor = SeverityColor * ProjectionDimFactor;

                DrawProjectionLine(graphArea, immNow, prognosis.CurrentImmunity, prognosis.ImmunityPerDay,
                    futureDays, totalDays, pastDays, immProjectionColor);
                DrawProjectionLine(graphArea, sevNow, prognosis.CurrentSeverity, prognosis.SeverityPerDay,
                    futureDays, totalDays, pastDays, sevProjectionColor);
            }

            // Draw a line from past data to current (if no history, estimate from rates)
            var tracker = DiseaseTracker.Instance;
            var history = tracker?.GetHistory(hediff);

            if (history == null || history.DataPoints.Count == 0)
            {
                // No history - draw a short line from slightly in the past to now
                float pastX = graphArea.x + graphArea.width * (pastDays * 0.5f / totalDays);

                // Estimate where it was in the past based on current rates (don't clamp for drawing)
                float immPastValue = prognosis.CurrentImmunity - (prognosis.ImmunityPerDay * pastDays * 0.5f);
                float sevPastValue = prognosis.CurrentSeverity - (prognosis.SeverityPerDay * pastDays * 0.5f);

                // Clamp to 0 minimum for display
                immPastValue = Mathf.Max(0f, immPastValue);
                sevPastValue = Mathf.Max(0f, sevPastValue);

                float immPastY = graphArea.yMax - graphArea.height * immPastValue;
                float sevPastY = graphArea.yMax - graphArea.height * sevPastValue;

                Vector2 immPast = new Vector2(pastX, immPastY);
                Vector2 sevPast = new Vector2(pastX, sevPastY);

                Widgets.DrawLine(immPast, immNow, ImmunityColor, 2f);
                Widgets.DrawLine(sevPast, sevNow, SeverityColor, 2f);
            }

            // Draw current point markers (small squares)
            DrawPointMarker(immNow, ImmunityColor);
            DrawPointMarker(sevNow, SeverityColor);
        }

        private void DrawProjectionLine(Rect graphArea, Vector2 startPoint, float currentValue, float ratePerDay,
            float futureDays, float totalDays, float pastDays, Color color)
        {
            if (ratePerDay <= 0f)
            {
                // Not increasing - draw horizontal line (or slightly declining)
                float endValue = Mathf.Max(0f, currentValue + ratePerDay * futureDays);
                float endY = graphArea.yMax - graphArea.height * endValue;
                Vector2 endPoint = new Vector2(graphArea.xMax, endY);
                Widgets.DrawLine(startPoint, endPoint, color, 2f);
                return;
            }

            // Calculate when this line hits 100%
            float daysTo100 = (1f - currentValue) / ratePerDay;

            if (daysTo100 > futureDays)
            {
                // Won't hit 100% within the window - draw line to projected value at end
                float endValue = currentValue + ratePerDay * futureDays;
                float endY = graphArea.yMax - graphArea.height * endValue;
                Vector2 endPoint = new Vector2(graphArea.xMax, endY);
                Widgets.DrawLine(startPoint, endPoint, color, 2f);
            }
            else
            {
                // Will hit 100% - draw line to 100% and cap with a circle
                float nowRatio = pastDays / totalDays;
                float hit100Ratio = nowRatio + (daysTo100 / totalDays);
                float hit100X = graphArea.x + graphArea.width * hit100Ratio;

                // Line from current to 100%
                Vector2 hit100Point = new Vector2(hit100X, graphArea.y); // graphArea.y is top = 100%
                Widgets.DrawLine(startPoint, hit100Point, color, 2f);

                // Draw circle marker at 100% intercept
                DrawCircleMarker(hit100Point, color);
            }
        }

        private void DrawCircleMarker(Vector2 center, Color color, float radius = 4f)
        {
            const int segments = 12;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = 2f * Mathf.PI * i / segments;
                float angle2 = 2f * Mathf.PI * (i + 1) / segments;

                Vector2 p1 = new Vector2(center.x + radius * Mathf.Cos(angle1), center.y + radius * Mathf.Sin(angle1));
                Vector2 p2 = new Vector2(center.x + radius * Mathf.Cos(angle2), center.y + radius * Mathf.Sin(angle2));

                Widgets.DrawLine(p1, p2, color, 1.5f);
            }
        }

        private void DrawPointMarker(Vector2 center, Color color)
        {
            // Draw a small filled square as a point marker
            Rect marker = new Rect(center.x - 3f, center.y - 3f, 6f, 6f);
            Widgets.DrawBoxSolid(marker, color);
        }

        private void DrawLegend(Rect legendArea)
        {
            Text.Font = GameFont.Tiny;
            float rowHeight = TinyFontHeight;

            if (prognosis.IsValid)
            {
                // Immunity legend with current value and rate
                string immRate = prognosis.ImmunityPerDay > 0 ? $"+{prognosis.ImmunityPerDay * 100:0}%/d" : $"{prognosis.ImmunityPerDay * 100:0}%/d";
                string immText = $"Imm: {prognosis.CurrentImmunity * 100:0}% ({immRate})";
                Widgets.DrawBoxSolid(new Rect(legendArea.x, legendArea.y + 2f, 12f, 3f), ImmunityColor);
                Widgets.Label(new Rect(legendArea.x + 15f, legendArea.y - 2f, 140f, rowHeight), immText);

                // Severity legend with current value and rate
                string sevRate = prognosis.SeverityPerDay > 0 ? $"+{prognosis.SeverityPerDay * 100:0}%/d" : $"{prognosis.SeverityPerDay * 100:0}%/d";
                string sevText = $"Sev: {prognosis.CurrentSeverity * 100:0}% ({sevRate})";
                Widgets.DrawBoxSolid(new Rect(legendArea.x, legendArea.y + rowHeight, 12f, 3f), SeverityColor);
                Widgets.Label(new Rect(legendArea.x + 15f, legendArea.y + rowHeight - 4f, 140f, rowHeight), sevText);

                // Show data source indicator (above the legend entries to avoid x-axis overlap)
                if (!prognosis.UsingObservedRates)
                {
                    GUI.color = AxisColor;
                    Widgets.Label(new Rect(legendArea.x, legendArea.y - rowHeight, 160f, rowHeight), "(est. - gathering data)");
                    GUI.color = Color.white;
                }
            }
            else
            {
                Widgets.DrawBoxSolid(new Rect(legendArea.x, legendArea.y + 2f, 12f, 3f), ImmunityColor);
                Widgets.Label(new Rect(legendArea.x + 15f, legendArea.y - 2f, 70f, rowHeight), "Immunity");

                Widgets.DrawBoxSolid(new Rect(legendArea.x, legendArea.y + rowHeight, 12f, 3f), SeverityColor);
                Widgets.Label(new Rect(legendArea.x + 15f, legendArea.y + rowHeight - 4f, 70f, rowHeight), "Severity");
            }

            Text.Font = GameFont.Small;
        }

        private string FormatDays(float days)
        {
            if (days < 1f)
            {
                float hours = days * 24f;
                return $"{hours:0.#}h";
            }
            return $"{days:0.#}d";
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            // Close if the hediff we're tracking is no longer active in the tooltip
            // AND the mouse is not currently over our window (allowing interaction)
            bool tooltipActive = Patches.TooltipCompanionPatch.IsTooltipActiveFor(hediff);
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

            int graphWindowCount = 0;
            foreach (var window in Find.WindowStack.Windows)
            {
                if (window is DiseaseGraphWindow graphWindow)
                {
                    graphWindowCount++;
                    if (graphWindow.hediff == hediff)
                    {
                        return true;
                    }
                }
            }
			
            return false;
         }

        /// <summary>
        /// Close any existing graph window for a different hediff.
        /// </summary>
        public static void CloseOtherWindows(Hediff exceptFor)
        {
            var toClose = new List<Window>();
            foreach (var window in Find.WindowStack.Windows)
            {
                if (window is DiseaseGraphWindow graphWindow && graphWindow.hediff != exceptFor)
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
