using UnityEngine;
using Verse;

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

        // Graph layout constants
        private const float GraphPadding = 10f;
        private const float AxisLabelWidth = 30f;
        private const float AxisLabelHeight = 20f;

        // Colors
        private static readonly Color BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        private static readonly Color AxisColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color GridColor = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color ImmunityColor = new Color(0.2f, 0.8f, 0.2f); // Green
        private static readonly Color SeverityColor = new Color(0.8f, 0.2f, 0.2f); // Red
        private static readonly Color NowLineColor = new Color(1f, 1f, 1f, 0.5f);

        public override Vector2 InitialSize => new Vector2(320f, 220f);

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
            // Draw title
            Text.Font = GameFont.Small;
            string title = hediff?.Label ?? "Disease";
            Widgets.Label(new Rect(0, 0, inRect.width, 20f), title);

            // Calculate graph area (below title, with padding for axis labels)
            Rect graphArea = new Rect(
                AxisLabelWidth,
                25f,
                inRect.width - AxisLabelWidth - GraphPadding,
                inRect.height - 25f - AxisLabelHeight - GraphPadding
            );

            // Draw graph background
            Widgets.DrawBoxSolid(graphArea, BackgroundColor);

            // Draw grid lines
            DrawGridLines(graphArea);

            // Draw axes
            DrawAxes(graphArea);

            // Draw axis labels
            DrawAxisLabels(graphArea);

            // Draw sample trend lines (Phase 0: just demo data)
            DrawSampleTrendLines(graphArea);

            // Draw legend
            DrawLegend(new Rect(graphArea.x + 5f, graphArea.y + 5f, 80f, 30f));
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

        private void DrawAxisLabels(Rect graphArea)
        {
            Text.Font = GameFont.Tiny;
            Color oldColor = GUI.color;
            GUI.color = AxisColor;

            // Y-axis labels (percentages)
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(0, graphArea.y - 8f, AxisLabelWidth - 4f, 16f), "100%");
            Widgets.Label(new Rect(0, graphArea.y + graphArea.height / 2f - 8f, AxisLabelWidth - 4f, 16f), "50%");
            Widgets.Label(new Rect(0, graphArea.yMax - 8f, AxisLabelWidth - 4f, 16f), "0%");

            // X-axis labels (time)
            Text.Anchor = TextAnchor.UpperCenter;
            float centerX = graphArea.x + graphArea.width / 2f;
            Widgets.Label(new Rect(graphArea.x - 20f, graphArea.yMax + 2f, 40f, 16f), "Past");
            Widgets.Label(new Rect(centerX - 20f, graphArea.yMax + 2f, 40f, 16f), "Now");
            Widgets.Label(new Rect(graphArea.xMax - 20f, graphArea.yMax + 2f, 40f, 16f), "Future");

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = oldColor;
            Text.Font = GameFont.Small;
        }

        private void DrawSampleTrendLines(Rect graphArea)
        {
            // For Phase 0, draw sample lines to verify drawing works
            // These will be replaced with real data in Phase 1

            float nowX = graphArea.x + graphArea.width * 0.5f;

            // Draw "now" vertical line
            Color oldColor = GUI.color;
            GUI.color = NowLineColor;
            Widgets.DrawLineVertical(nowX, graphArea.y, graphArea.height);
            GUI.color = oldColor;

            // Sample immunity line (green): starts at 10%, rises to ~80% over time
            float immStartY = graphArea.yMax - graphArea.height * 0.1f; // 10%
            float immMidY = graphArea.yMax - graphArea.height * 0.45f;  // 45% at "now"
            float immEndY = graphArea.yMax - graphArea.height * 0.95f;  // 95% at end (projected)

            Vector2 immStart = new Vector2(graphArea.x, immStartY);
            Vector2 immMid = new Vector2(nowX, immMidY);
            Vector2 immEnd = new Vector2(graphArea.xMax, immEndY);

            Widgets.DrawLine(immStart, immMid, ImmunityColor, 2f);
            Widgets.DrawLine(immMid, immEnd, ImmunityColor * 0.7f, 2f); // Dimmer for projection

            // Sample severity line (red): starts at 5%, rises more slowly
            float sevStartY = graphArea.yMax - graphArea.height * 0.05f; // 5%
            float sevMidY = graphArea.yMax - graphArea.height * 0.35f;   // 35% at "now"
            float sevEndY = graphArea.yMax - graphArea.height * 0.70f;   // 70% at end (projected)

            Vector2 sevStart = new Vector2(graphArea.x, sevStartY);
            Vector2 sevMid = new Vector2(nowX, sevMidY);
            Vector2 sevEnd = new Vector2(graphArea.xMax, sevEndY);

            Widgets.DrawLine(sevStart, sevMid, SeverityColor, 2f);
            Widgets.DrawLine(sevMid, sevEnd, SeverityColor * 0.7f, 2f); // Dimmer for projection
        }

        private void DrawLegend(Rect legendArea)
        {
            Text.Font = GameFont.Tiny;

            // Immunity legend
            Widgets.DrawBoxSolid(new Rect(legendArea.x, legendArea.y + 2f, 12f, 3f), ImmunityColor);
            Widgets.Label(new Rect(legendArea.x + 15f, legendArea.y - 2f, 60f, 16f), "Immunity");

            // Severity legend
            Widgets.DrawBoxSolid(new Rect(legendArea.x, legendArea.y + 16f, 12f, 3f), SeverityColor);
            Widgets.Label(new Rect(legendArea.x + 15f, legendArea.y + 12f, 60f, 16f), "Severity");

            Text.Font = GameFont.Small;
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
            var toClose = new System.Collections.Generic.List<Window>();
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
