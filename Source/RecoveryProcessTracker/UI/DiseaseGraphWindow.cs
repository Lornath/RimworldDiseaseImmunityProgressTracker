using System.Collections.Generic;
using UnityEngine;
using Verse;
using RecoveryProcessTracker.Core;

namespace RecoveryProcessTracker.UI
{
    /// <summary>
    /// A tooltip companion window that displays a graphical timeline of disease progression vs immunity gain.
    /// Opens automatically when hovering over an immunizable disease in the health tab.
    /// </summary>
    public class DiseaseGraphWindow : Window
    {
        private readonly Hediff hediff;
        private Vector2 openedAtMousePos;

        // Cached prognosis (recalculated each frame)
        private PrognosisCalculator.PrognosisResult prognosis;

        // Graph layout constants
        private const float GraphPadding = 10f;
        private const float AxisLabelWidth = 30f;
        private const float AxisLabelHeight = 20f;
        private const float TitleHeight = 20f;
        private const float VerdictHeight = 18f;

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

        public override Vector2 InitialSize => new Vector2(320f, 240f);

        public DiseaseGraphWindow(Hediff hediff)
        {
            this.hediff = hediff;
            this.openedAtMousePos = Event.current?.mousePosition ?? Vector2.zero;

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
        }

        protected override void SetInitialSizeAndPosition()
        {
            // Position the window above and to the right of the mouse
            // Our bottom-left corner anchors near the mouse position
            // This places our graph above the standard tooltip (which anchors its top-left to mouse)
            float xPos = openedAtMousePos.x + 15f;
            float yPos = openedAtMousePos.y - InitialSize.y - 5f; // 5px gap above mouse

            // Clamp to screen bounds
            if (xPos + InitialSize.x > Verse.UI.screenWidth)
            {
                xPos = openedAtMousePos.x - InitialSize.x - 15f;
            }
            if (yPos < 0)
            {
                yPos = 0;
            }
            if (yPos + InitialSize.y > Verse.UI.screenHeight)
            {
                yPos = Verse.UI.screenHeight - InitialSize.y;
            }

            windowRect = new Rect(xPos, yPos, InitialSize.x, InitialSize.y);
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Get history and calculate prognosis using observed rates if available
            var tracker = DiseaseTracker.Instance;
            var history = tracker?.GetOrCreateHistory(hediff);
            prognosis = PrognosisCalculator.Calculate(hediff, history);

            // Draw title
            Text.Font = GameFont.Small;
            string title = hediff?.Label?.CapitalizeFirst() ?? "Disease";
            Widgets.Label(new Rect(0, 0, inRect.width, TitleHeight), title);

            // Draw verdict
            DrawVerdict(new Rect(0, TitleHeight, inRect.width, VerdictHeight));

            // Calculate graph area (below title and verdict, with padding for axis labels)
            float graphTop = TitleHeight + VerdictHeight + 5f;
            Rect graphArea = new Rect(
                AxisLabelWidth,
                graphTop,
                inRect.width - AxisLabelWidth - GraphPadding,
                inRect.height - graphTop - AxisLabelHeight - GraphPadding
            );

            // Draw graph background
            Widgets.DrawBoxSolid(graphArea, BackgroundColor);

            // Calculate time window
            float futureDays = CalculateFutureDays();
            float pastDays = DefaultPastDays;

            // Draw grid lines
            DrawGridLines(graphArea);

            // Draw axes
            DrawAxes(graphArea);

            // Draw axis labels with time info
            DrawAxisLabels(graphArea, pastDays, futureDays);

            // Draw trend lines with real data
            DrawTrendLines(graphArea, pastDays, futureDays);

            // Draw legend
            DrawLegend(new Rect(graphArea.x + 5f, graphArea.y + 5f, 80f, 30f));
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
                verdictText = "Immune";
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
                    marginInfo = $" ({FormatDays(prognosis.MarginDays)}/{marginPercent:P0} margin)";
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
                    shortfallInfo = $" ({FormatDays(shortfallDays)}/{shortfallPercent:P0} short)";
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
            Widgets.Label(new Rect(graphArea.xMax - 20f, graphArea.yMax + 2f, 40f, 16f), $"+{futureDays:0.#}d");

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

            // Dimmed color for projections
            Color immProjectionColor = ImmunityColor * ProjectionDimFactor;
            Color sevProjectionColor = SeverityColor * ProjectionDimFactor;

            // Y position for 100%
            float y100 = graphArea.y; // Top of graph = 100%

            // Draw immunity projection line
            DrawProjectionLine(graphArea, immNow, prognosis.CurrentImmunity, prognosis.ImmunityPerDay,
                futureDays, totalDays, pastDays, immProjectionColor);

            // Draw severity projection line
            DrawProjectionLine(graphArea, sevNow, prognosis.CurrentSeverity, prognosis.SeverityPerDay,
                futureDays, totalDays, pastDays, sevProjectionColor);

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
                // Will hit 100% - draw line to 100%, then horizontal at 100%
                float nowRatio = pastDays / totalDays;
                float hit100Ratio = nowRatio + (daysTo100 / totalDays);
                float hit100X = graphArea.x + graphArea.width * hit100Ratio;

                // Line from current to 100%
                Vector2 hit100Point = new Vector2(hit100X, graphArea.y); // graphArea.y is top = 100%
                Widgets.DrawLine(startPoint, hit100Point, color, 2f);

                // Horizontal line at 100% to end of graph
                if (hit100X < graphArea.xMax)
                {
                    Vector2 endPoint = new Vector2(graphArea.xMax, graphArea.y);
                    Widgets.DrawLine(hit100Point, endPoint, color, 2f);
                }
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

            if (prognosis.IsValid)
            {
                // Immunity legend with current value and rate
                string immRate = prognosis.ImmunityPerDay > 0 ? $"+{prognosis.ImmunityPerDay:P0}/d" : $"{prognosis.ImmunityPerDay:P0}/d";
                string immText = $"Imm: {prognosis.CurrentImmunity:P0} ({immRate})";
                Widgets.DrawBoxSolid(new Rect(legendArea.x, legendArea.y + 2f, 12f, 3f), ImmunityColor);
                Widgets.Label(new Rect(legendArea.x + 15f, legendArea.y - 2f, 140f, 16f), immText);

                // Severity legend with current value and rate
                string sevRate = prognosis.SeverityPerDay > 0 ? $"+{prognosis.SeverityPerDay:P0}/d" : $"{prognosis.SeverityPerDay:P0}/d";
                string sevText = $"Sev: {prognosis.CurrentSeverity:P0} ({sevRate})";
                Widgets.DrawBoxSolid(new Rect(legendArea.x, legendArea.y + 16f, 12f, 3f), SeverityColor);
                Widgets.Label(new Rect(legendArea.x + 15f, legendArea.y + 12f, 140f, 16f), sevText);

                // Show data source indicator
                if (!prognosis.UsingObservedRates)
                {
                    GUI.color = AxisColor;
                    Widgets.Label(new Rect(legendArea.x, legendArea.y + 30f, 160f, 16f), "(est. - gathering data)");
                    GUI.color = Color.white;
                }
            }
            else
            {
                Widgets.DrawBoxSolid(new Rect(legendArea.x, legendArea.y + 2f, 12f, 3f), ImmunityColor);
                Widgets.Label(new Rect(legendArea.x + 15f, legendArea.y - 2f, 70f, 16f), "Immunity");

                Widgets.DrawBoxSolid(new Rect(legendArea.x, legendArea.y + 16f, 12f, 3f), SeverityColor);
                Widgets.Label(new Rect(legendArea.x + 15f, legendArea.y + 12f, 70f, 16f), "Severity");
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
            if (!Patches.TooltipCompanionPatch.IsTooltipActiveFor(hediff))
            {
                Close(false);
            }
        }

        /// <summary>
        /// Check if a window is already open for the given hediff.
        /// </summary>
        public static bool IsOpenFor(Hediff hediff)
        {
            if (hediff == null) return false;

            foreach (var window in Find.WindowStack.Windows)
            {
                if (window is DiseaseGraphWindow graphWindow && graphWindow.hediff == hediff)
                {
                    return true;
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
