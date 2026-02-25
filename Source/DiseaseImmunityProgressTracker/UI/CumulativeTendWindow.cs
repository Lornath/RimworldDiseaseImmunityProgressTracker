using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using RimWorld;
using DiseaseImmunityProgressTracker.Core;

namespace DiseaseImmunityProgressTracker.UI
{
    /// <summary>
    /// A tooltip companion window that displays cumulative tend progress for diseases
    /// like Gut Worms and Muscle Parasites that cure through accumulated tend quality.
    /// </summary>
    [StaticConstructorOnStartup]
    public class CumulativeTendWindow : Window, ICompanionWindow
    {
        private readonly Hediff hediff;
        public Hediff Hediff => hediff;

        private readonly HediffComp_TendDuration tendComp;

        // Reflection for accessing private field
        private static readonly FieldInfo totalTendQualityField = typeof(HediffComp_TendDuration)
            .GetField("totalTendQuality", BindingFlags.NonPublic | BindingFlags.Instance);

        // Layout constants
        private const float Padding = 10f;
        private const float ProgressBarHeight = 22f;
        private const int MaxTendsToShow = 3;

        // Text heights - measured from actual font metrics
        private static float SmallFontHeight => Text.LineHeightOf(GameFont.Small);
        private static float TinyFontHeight => Text.LineHeightOf(GameFont.Tiny);

        // Colors
        private static readonly Color BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        private static readonly Color ProgressBarBgColor = new Color(0.2f, 0.2f, 0.2f);
        private static readonly Color ProgressBarFillColor = new Color(0.3f, 0.7f, 0.3f);
        private static readonly Color AxisColor = new Color(0.5f, 0.5f, 0.5f);

        // Fallback icon for tending without medicine
        private static readonly Texture2D NoMedsIcon = ContentFinder<Texture2D>.Get("UI/Icons/Medical/NoMeds");

        public override Vector2 InitialSize => new Vector2(280f, CalculateWindowHeight());

        /// <summary>
        /// Calculate window height based on actual font metrics.
        /// </summary>
        private static float CalculateWindowHeight()
        {
            return SmallFontHeight + 2f                    // Title + gap
                 + SmallFontHeight + 2f                    // Progress text + gap
                 + ProgressBarHeight + 10f                 // Progress bar + gap
                 + TinyFontHeight + 2f                     // "Recent tends:" label + gap
                 + TinyFontHeight * MaxTendsToShow + 5f    // Tend entries + gap
                 + TinyFontHeight;                         // Estimate row
        }

        protected override float Margin => 0f;

        public CumulativeTendWindow(Hediff hediff, HediffComp_TendDuration tendComp)
        {
            this.hediff = hediff;
            this.tendComp = tendComp;

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

            // Get current cumulative data
            float totalTendQuality = GetTotalTendQuality();
            float targetQuality = tendComp.TProps.disappearsAtTotalTendQuality;
            float progress = totalTendQuality / targetQuality;

            // Get history for tend events
            var tracker = DiseaseTracker.Instance;
            var history = tracker?.GetOrCreateHistory(hediff);

            // Draw title
            Text.Font = GameFont.Small;
            float smallHeight = SmallFontHeight;
            float tinyHeight = TinyFontHeight;
            string title = hediff?.Label?.CapitalizeFirst() ?? "Disease";
            Widgets.Label(new Rect(0, 0, inRect.width, smallHeight), title);

            float yOffset = smallHeight + 2f;

            // Draw progress text (mirror in-game tooltip format)
            Text.Font = GameFont.Small;
            float currentPct = totalTendQuality * 100f;
            float targetPct = targetQuality * 100f;
            string progressText = $"Progress: {currentPct:0.#}% / {targetPct:0.#}%";
            Widgets.Label(new Rect(0, yOffset, inRect.width, smallHeight), progressText);
            yOffset += smallHeight + 2f;

            // Draw progress bar
            Rect progressBarRect = new Rect(0, yOffset, inRect.width, ProgressBarHeight);
            DrawProgressBar(progressBarRect, progress);
            yOffset += ProgressBarHeight + 10f;

            // Draw recent tends section
            Text.Font = GameFont.Tiny;
            GUI.color = AxisColor;
            Widgets.Label(new Rect(0, yOffset, inRect.width, tinyHeight), "Recent tends:");
            GUI.color = Color.white;
            yOffset += tinyHeight + 2f;

            DrawRecentTends(new Rect(0, yOffset, inRect.width, MaxTendsToShow * tinyHeight), history);
            yOffset += MaxTendsToShow * tinyHeight + 5f;

            // Draw estimate
            DrawEstimate(new Rect(0, yOffset, inRect.width, tinyHeight), history, totalTendQuality, targetQuality);

            Text.Font = GameFont.Small;
        }

        private float GetTotalTendQuality()
        {
            if (tendComp == null || totalTendQualityField == null) return 0f;
            return (float)totalTendQualityField.GetValue(tendComp);
        }

        private void DrawProgressBar(Rect rect, float progress)
        {
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
            Widgets.Label(rect, $"{progressPct}% complete");
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawRecentTends(Rect rect, DiseaseHistory history)
        {
            Text.Font = GameFont.Tiny;
            float entryHeight = TinyFontHeight;

            if (history == null || history.TendingEvents.Count == 0)
            {
                GUI.color = AxisColor;
                Widgets.Label(rect, "  No tends recorded yet");
                GUI.color = Color.white;
                return;
            }

            // Get the most recent tends (up to MaxTendsToShow)
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
            // Format: [icon] +72% - Dr. Mia
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
                doctorInfo = "Self-tend";
            }

            int qualityPct = (int)(tend.Quality * 100);
            string text = $"+{qualityPct}% - {doctorInfo}";

            Rect textRect = new Rect(iconRect.xMax + iconPadding, rect.y, rect.width - iconSize - iconPadding - 4f, rect.height);
            Widgets.Label(textRect, text);
        }

        private void DrawEstimate(Rect rect, DiseaseHistory history, float currentTotal, float target)
        {
            float remaining = target - currentTotal;
            if (remaining <= 0)
            {
                GUI.color = ProgressBarFillColor;
                Widgets.Label(rect, "Will be cured on next tend!");
                GUI.color = Color.white;
                return;
            }

            // Calculate average tend quality from history (excluding 0% tends for estimation)
            float avgQuality = 0.5f; // Default estimate
            if (history != null && history.TendingEvents.Count > 0)
            {
                var nonZeroTends = history.TendingEvents.Where(t => t.Quality > 0.001f).ToList();
                if (nonZeroTends.Count > 0)
                {
                    avgQuality = nonZeroTends.Average(t => t.Quality);
                }
                // If all tends were 0%, keep default estimate
            }

            // Ensure we never divide by zero
            if (avgQuality < 0.01f)
            {
                avgQuality = 0.5f; // Fall back to default
            }

            int estimatedTends = (int)Math.Ceiling(remaining / avgQuality);

            // Calculate average time between tends (in days)
            float avgDaysBetweenTends = 2f; // Default: tends last ~2 days
            if (history != null && history.TendingEvents.Count >= 2)
            {
                var sortedTends = history.TendingEvents.OrderBy(t => t.Tick).ToList();
                float totalTicksBetween = 0f;
                for (int i = 1; i < sortedTends.Count; i++)
                {
                    totalTicksBetween += sortedTends[i].Tick - sortedTends[i - 1].Tick;
                }
                avgDaysBetweenTends = (totalTicksBetween / (sortedTends.Count - 1)) / 60000f; // 60000 ticks per day
            }

            float estimatedDays = estimatedTends * avgDaysBetweenTends;

            Text.Font = GameFont.Tiny;
            GUI.color = AxisColor;
            string timeEstimate = estimatedDays < 1f
                ? $"{estimatedDays * 24f:0.#}h"
                : $"{estimatedDays:0.#}d";
            string estimateText = $"Est. ~{estimatedTends} tend{(estimatedTends != 1 ? "s" : "")} remaining (~{timeEstimate})";
            Widgets.Label(rect, estimateText);
            GUI.color = Color.white;
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            // Close if the hediff we're tracking is no longer active in the tooltip
            bool tooltipActive = Patches.CumulativeTendPatch.IsTooltipActiveFor(hediff);
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
                if (window is CumulativeTendWindow tendWindow && tendWindow.hediff == hediff)
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
                if (window is CumulativeTendWindow tendWindow && tendWindow.hediff != exceptFor)
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
