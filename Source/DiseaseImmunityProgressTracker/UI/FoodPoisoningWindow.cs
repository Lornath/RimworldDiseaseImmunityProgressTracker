using System;
using UnityEngine;
using Verse;
using RimWorld;
using DiseaseImmunityProgressTracker.Core;

namespace DiseaseImmunityProgressTracker.UI
{
    /// <summary>
    /// A tooltip companion window that displays Food Poisoning (Type 7) progress.
    /// Shows countdown timer, staged progress bar, current symptoms, and verdict.
    ///
    /// Food Poisoning is a simple 24-hour ordeal:
    /// - Severity starts at 1.0 and decreases at -1/day (from HediffComp_SeverityPerDay)
    /// - Three stages: Initial (sev >= 0.80), Major (sev >= 0.20), Recovering (sev &lt; 0.20)
    /// - No treatment possible - just wait it out
    /// </summary>
    [StaticConstructorOnStartup]
    public class FoodPoisoningWindow : Window, ICompanionWindow
    {
        private readonly Hediff hediff;
        public Hediff Hediff => hediff;

        // Layout constants
        private const float Padding = 10f;
        private const float ProgressBarHeight = 28f;

        // Text heights - measured from actual font metrics
        private static float SmallFontHeight => Text.LineHeightOf(GameFont.Small);
        private static float TinyFontHeight => Text.LineHeightOf(GameFont.Tiny);

        // Time constants
        private const float TicksPerDay = 60000f;
        private const float TicksPerHour = 2500f;

        // Stage thresholds (severity decreasing from 1.0 to 0.0)
        private const float InitialThreshold = 0.80f;   // sev >= 0.80 = Initial stage
        private const float MajorThreshold = 0.20f;     // sev >= 0.20 = Major stage
        // Below 0.20 = Recovering stage

        // Progress bar segment proportions (as fraction of total bar)
        // Initial: 1.0 -> 0.80 = 20% of duration
        // Major: 0.80 -> 0.20 = 60% of duration
        // Recovering: 0.20 -> 0.00 = 20% of duration
        private const float InitialFraction = 0.20f;
        private const float MajorFraction = 0.60f;
        private const float RecoveringFraction = 0.20f;

        // Colors
        private static readonly Color InitialColor = new Color(1f, 0.6f, 0.2f, 0.8f);      // Orange
        private static readonly Color MajorColor = new Color(0.9f, 0.2f, 0.2f, 0.8f);       // Red
        private static readonly Color RecoveringColor = new Color(0.3f, 0.8f, 0.3f, 0.8f);   // Green
        private static readonly Color ProgressBarBgColor = new Color(0.2f, 0.2f, 0.2f);
        private static readonly Color MarkerColor = Color.white;
        private static readonly Color AxisColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color SafeColor = new Color(0.4f, 1f, 0.4f);
        private static readonly Color WarningColor = new Color(1f, 0.9f, 0.2f);
        private static readonly Color DangerColor = new Color(1f, 0.4f, 0.4f);

        public override Vector2 InitialSize => new Vector2(320f, CalculateWindowHeight());

        private static float CalculateWindowHeight()
        {
            return Padding * 2                          // Top and bottom padding
                 + SmallFontHeight + 4f                 // Title + gap
                 + SmallFontHeight + 6f                 // Countdown + gap
                 + ProgressBarHeight + 2f               // Progress bar + gap
                 + TinyFontHeight * 3 + 2f              // Stage labels (3 rows) + gap
                 + TinyFontHeight * 2 + 6f              // Vomiting + stage descriptor + gap
                 + SmallFontHeight;                     // Verdict
        }

        protected override float Margin => 0f;

        public FoodPoisoningWindow(Hediff hediff)
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
            onlyOneOfTypeAllowed = false;
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

            // Cache font heights
            float smallHeight = SmallFontHeight;
            float tinyHeight = TinyFontHeight;

            // Apply padding to create content area
            Rect contentRect = inRect.ContractedBy(Padding);
            float yOffset = contentRect.y;
            float contentWidth = contentRect.width;

            // Get severity rate from the comp (for mod compatibility)
            float severityRate = GetSeverityChangePerDay();
            float severity = hediff.Severity;
            float hoursRemaining = CalculateHoursRemaining(severity, severityRate);

            // Draw title with stage
            DrawTitle(new Rect(contentRect.x, yOffset, contentWidth, smallHeight));
            yOffset += smallHeight + 4f;

            // Draw countdown timer
            DrawCountdown(new Rect(contentRect.x, yOffset, contentWidth, smallHeight), hoursRemaining);
            yOffset += smallHeight + 6f;

            // Draw staged progress bar
            DrawStagedProgressBar(new Rect(contentRect.x, yOffset, contentWidth, ProgressBarHeight), severity);
            yOffset += ProgressBarHeight + 2f;

            // Draw stage labels below bar
            DrawStageLabels(new Rect(contentRect.x, yOffset, contentWidth, tinyHeight * 3), severity, hoursRemaining);
            yOffset += tinyHeight * 3 + 2f;

            // Draw vomiting info and stage descriptor
            DrawSymptoms(new Rect(contentRect.x, yOffset, contentWidth, tinyHeight * 2), severity);
            yOffset += tinyHeight * 2 + 6f;

            // Draw verdict
            DrawVerdict(new Rect(contentRect.x, yOffset, contentWidth, smallHeight), severity, hoursRemaining);

            Text.Font = GameFont.Small;
        }

        private float GetSeverityChangePerDay()
        {
            var comp = hediff.TryGetComp<HediffComp_SeverityPerDay>();
            if (comp != null)
            {
                return comp.SeverityChangePerDay();
            }
            return -1f; // Default for food poisoning
        }

        private float CalculateHoursRemaining(float severity, float severityRate)
        {
            if (severityRate >= 0) return 0f; // Not decreasing
            float daysRemaining = severity / Mathf.Abs(severityRate);
            return daysRemaining * 24f;
        }

        private void DrawTitle(Rect rect)
        {
            Text.Font = GameFont.Small;
            string stageName = hediff.CurStage?.label ?? "Unknown";
            // Capitalize first letter
            if (stageName.Length > 0)
                stageName = char.ToUpper(stageName[0]) + stageName.Substring(1);
            Widgets.Label(rect, $"Food Poisoning - {stageName}");
        }

        private void DrawCountdown(Rect rect, float hoursRemaining)
        {
            Text.Font = GameFont.Small;

            string timeText;
            if (hoursRemaining <= 0)
            {
                timeText = "Resolving...";
            }
            else
            {
                int hours = (int)hoursRemaining;
                int minutes = (int)((hoursRemaining - hours) * 60);
                timeText = $"{hours}h {minutes:D2}m remaining";
            }

            GUI.color = SafeColor;
            Widgets.Label(rect, timeText);
            GUI.color = Color.white;
        }

        private void DrawStagedProgressBar(Rect rect, float severity)
        {
            // Background
            Widgets.DrawBoxSolid(rect, ProgressBarBgColor);

            // Calculate the three segment rects
            float initialWidth = rect.width * InitialFraction;
            float majorWidth = rect.width * MajorFraction;
            float recoveringWidth = rect.width * RecoveringFraction;

            Rect initialRect = new Rect(rect.x, rect.y, initialWidth, rect.height);
            Rect majorRect = new Rect(rect.x + initialWidth, rect.y, majorWidth, rect.height);
            Rect recoveringRect = new Rect(rect.x + initialWidth + majorWidth, rect.y, recoveringWidth, rect.height);

            // Draw colored segments
            Widgets.DrawBoxSolid(initialRect, InitialColor);
            Widgets.DrawBoxSolid(majorRect, MajorColor);
            Widgets.DrawBoxSolid(recoveringRect, RecoveringColor);

            // Draw current position marker
            // elapsedFraction = 1 - severity (severity goes from 1.0 down to 0.0)
            float elapsedFraction = Mathf.Clamp01(1f - severity);
            float markerX = rect.x + rect.width * elapsedFraction;

            // Draw marker as a white vertical line with wider indicator
            const float markerWidth = 3f;
            Rect markerRect = new Rect(markerX - markerWidth / 2f, rect.y - 2f, markerWidth, rect.height + 4f);
            Widgets.DrawBoxSolid(markerRect, MarkerColor);

            // Border
            Widgets.DrawBox(rect);
        }

        private void DrawStageLabels(Rect rect, float severity, float hoursRemaining)
        {
            Text.Font = GameFont.Tiny;
            float rowHeight = TinyFontHeight;

            float initialWidth = rect.width * InitialFraction;
            float majorWidth = rect.width * MajorFraction;
            float recoveringWidth = rect.width * RecoveringFraction;

            // Stage name labels (centered in each segment)
            Text.Anchor = TextAnchor.UpperCenter;

            GUI.color = InitialColor;
            Widgets.Label(new Rect(rect.x, rect.y, initialWidth, rowHeight), "Initial");

            GUI.color = MajorColor;
            Widgets.Label(new Rect(rect.x + initialWidth, rect.y, majorWidth, rowHeight), "Major");

            GUI.color = RecoveringColor;
            Widgets.Label(new Rect(rect.x + initialWidth + majorWidth, rect.y, recoveringWidth, rowHeight), "Recovering");

            // Duration labels
            GUI.color = AxisColor;
            Widgets.Label(new Rect(rect.x, rect.y + rowHeight, initialWidth, rowHeight), "~4.8h");
            Widgets.Label(new Rect(rect.x + initialWidth, rect.y + rowHeight, majorWidth, rowHeight), "~14.4h");
            Widgets.Label(new Rect(rect.x + initialWidth + majorWidth, rect.y + rowHeight, recoveringWidth, rowHeight), "~4.8h");

            // Time-to-next-stage indicator
            float timeToNextStage = CalculateTimeToNextStage(severity, hoursRemaining);
            string nextStageText = "";
            if (severity >= InitialThreshold)
            {
                nextStageText = $"Major stage in ~{timeToNextStage:0.#}h";
                GUI.color = WarningColor;
            }
            else if (severity >= MajorThreshold)
            {
                nextStageText = $"Recovery starts in ~{timeToNextStage:0.#}h";
                GUI.color = SafeColor;
            }
            else
            {
                nextStageText = $"Done in ~{hoursRemaining:0.#}h";
                GUI.color = SafeColor;
            }
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(rect.x, rect.y + rowHeight * 2, rect.width, rowHeight), nextStageText);

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private float CalculateTimeToNextStage(float severity, float totalHoursRemaining)
        {
            if (severity >= InitialThreshold)
            {
                // Time until severity drops below 0.80 (entering Major stage)
                float severityToDrop = severity - InitialThreshold;
                float totalDuration = totalHoursRemaining > 0 ? totalHoursRemaining / severity * severityToDrop : 0;
                return totalDuration;
            }
            else if (severity >= MajorThreshold)
            {
                // Time until severity drops below 0.20 (entering Recovering stage)
                float severityToDrop = severity - MajorThreshold;
                float totalDuration = totalHoursRemaining > 0 ? totalHoursRemaining / severity * severityToDrop : 0;
                return totalDuration;
            }
            return totalHoursRemaining;
        }

        private void DrawSymptoms(Rect rect, float severity)
        {
            Text.Font = GameFont.Tiny;
            float rowHeight = TinyFontHeight;

            // Vomiting info - translate MTB days to human-readable expected interval
            // MTB = Mean Time Between events (exponential distribution, so MTB IS the expected interval)
            string vomitText;
            if (severity >= InitialThreshold)
            {
                // MTB 0.3 days = every ~7 hours
                vomitText = "Expected vomiting every ~7 hours";
            }
            else if (severity >= MajorThreshold)
            {
                // MTB 0.2 days = every ~5 hours
                vomitText = "Expected vomiting every ~5 hours";
            }
            else
            {
                // MTB 0.4 days = every ~10 hours
                vomitText = "Expected vomiting every ~10 hours";
            }

            GUI.color = AxisColor;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, rowHeight), vomitText);

            // Stage descriptor
            string stageDesc;
            Color stageColor;
            if (severity >= InitialThreshold)
            {
                stageDesc = "Onset - symptoms ramping up";
                stageColor = InitialColor;
            }
            else if (severity >= MajorThreshold)
            {
                stageDesc = "Worst phase - severely impaired";
                stageColor = MajorColor;
            }
            else
            {
                stageDesc = "Recovering - symptoms easing";
                stageColor = RecoveringColor;
            }

            GUI.color = stageColor;
            Widgets.Label(new Rect(rect.x, rect.y + rowHeight, rect.width, rowHeight), stageDesc);
            GUI.color = Color.white;
        }

        private void DrawVerdict(Rect rect, float severity, float hoursRemaining)
        {
            Text.Font = GameFont.Small;

            string verdictText;
            Color verdictColor;

            if (severity >= InitialThreshold)
            {
                verdictText = "The worst part is coming soon - brace yourself";
                verdictColor = WarningColor;
            }
            else if (severity >= 0.50f)
            {
                float hoursToRecovery = CalculateTimeToNextStage(severity, hoursRemaining);
                verdictText = $"Hang in there, recovery starts in ~{hoursToRecovery:0.#}h";
                verdictColor = DangerColor;
            }
            else if (severity >= MajorThreshold)
            {
                float hoursToRecovery = CalculateTimeToNextStage(severity, hoursRemaining);
                verdictText = $"Almost through the worst - recovery in ~{hoursToRecovery:0.#}h";
                verdictColor = WarningColor;
            }
            else
            {
                verdictText = $"Almost done! ~{hoursRemaining:0.#}h to go";
                verdictColor = SafeColor;
            }

            GUI.color = verdictColor;
            Widgets.Label(rect, verdictText);
            GUI.color = Color.white;
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            // Close if the hediff we're tracking is no longer active in the tooltip
            bool tooltipActive = Patches.FoodPoisoningPatch.IsTooltipActiveFor(hediff);
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
                if (window is FoodPoisoningWindow fpWindow && fpWindow.hediff == hediff)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
