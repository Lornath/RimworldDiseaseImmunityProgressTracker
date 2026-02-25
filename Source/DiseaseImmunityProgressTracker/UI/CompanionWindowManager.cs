using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DiseaseImmunityProgressTracker.UI
{
    /// <summary>
    /// Central manager for tooltip companion windows.
    /// Handles tracking active tooltips across all disease types and stacking windows to avoid overlap.
    /// </summary>
    public static class CompanionWindowManager
    {
        // Track all hediffs whose tooltips were rendered this frame
        private static HashSet<Hediff> activeHediffsThisFrame = new HashSet<Hediff>();
        private static int lastFrameUpdated = -1;

        // Track all hediffs that were active last frame (for staleness detection)
        private static HashSet<Hediff> activeHediffsLastFrame = new HashSet<Hediff>();

        // Track window slots for stable positioning
        // Maps window instance to its slot index (assigned when window opens)
        private static Dictionary<Window, int> windowSlots = new Dictionary<Window, int>();
        private static int nextSlot = 0;

        // Stacking configuration
        private const float VerticalStackGap = 8f;

        /// <summary>
        /// Call this when a tooltip is being rendered for a hediff.
        /// Returns true if a window should be opened (wasn't already active).
        /// </summary>
        public static bool RegisterTooltipActive(Hediff hediff)
        {
            UpdateFrameTracking();
            bool isNew = activeHediffsThisFrame.Add(hediff);
            return isNew;
        }

        /// <summary>
        /// Check if a tooltip is currently active for the given hediff.
        /// Considers a tooltip "stale" if it wasn't rendered in the last couple frames.
        /// </summary>
        public static bool IsTooltipActiveFor(Hediff hediff)
        {
            if (hediff == null) return false;

            int currentFrame = Time.frameCount;

            // Current frame - definitely active
            if (currentFrame == lastFrameUpdated && activeHediffsThisFrame.Contains(hediff))
                return true;

            // Previous frame - still considered active (grace period)
            if (currentFrame == lastFrameUpdated + 1 && activeHediffsLastFrame.Contains(hediff))
                return true;

            // 2 frames ago - still give a brief grace period
            if (currentFrame == lastFrameUpdated + 2 && activeHediffsLastFrame.Contains(hediff))
                return true;

            return false;
        }

        /// <summary>
        /// Get all companion windows currently open, sorted by slot for consistent ordering.
        /// </summary>
        public static List<Window> GetOpenCompanionWindows()
        {
            var windows = new List<Window>();
            foreach (var window in Find.WindowStack.Windows)
            {
                if (window is DiseaseGraphWindow ||
                    window is CumulativeTendWindow ||
                    window is TimeBasedWindow)
                {
                    windows.Add(window);
                }
            }

            // Sort by slot for consistent ordering
            windows.Sort((a, b) => GetWindowSlot(a).CompareTo(GetWindowSlot(b)));
            return windows;
        }

        /// <summary>
        /// Get or assign a slot index for a window.
        /// Slots are assigned when windows first request their position.
        /// </summary>
        private static int GetWindowSlot(Window window)
        {
            if (!windowSlots.TryGetValue(window, out int slot))
            {
                slot = nextSlot++;
                windowSlots[window] = slot;
            }
            return slot;
        }

        /// <summary>
        /// Get the slot for a window (for debug logging).
        /// </summary>
        public static int GetWindowSlotDebug(Window window)
        {
            return windowSlots.TryGetValue(window, out int slot) ? slot : -1;
        }

        /// <summary>
        /// Remove a window from slot tracking (call when window closes).
        /// </summary>
        public static void UnregisterWindow(Window window)
        {
            windowSlots.Remove(window);

            // Reset slot counter if no windows are open
            if (windowSlots.Count == 0)
            {
                nextSlot = 0;
            }
        }

        /// <summary>
        /// Calculate window position with stacking offset based on slot ordering.
        /// Treats all windows for the same pawn as a single "super window" stack to ensure
        /// the entire group is positioned correctly relative to the tooltip/mouse.
        /// </summary>
        public static Rect CalculateStackedWindowRect(Vector2 windowSize, Window self = null, bool logDebug = false)
        {
            if (self == null) return WindowPositionHelper.CalculateWindowRect(windowSize);

            // 1. Get all relevant windows (same pawn)
            Pawn selfPawn = (self as ICompanionWindow)?.Hediff?.pawn;
            var allWindows = GetOpenCompanionWindows();
            var myWindows = new List<Window>();

            // Debug info
            bool verbose = DiseaseImmunityProgressTrackerMod.Settings.verboseLogging && logDebug;
            if (verbose) Log.Message($"[DiseaseImmunityProgressTracker] CalcStack for {self}, Pawn={selfPawn}, AllWins={allWindows.Count}");

            foreach (var w in allWindows)
            {
                // Always add self if found (sanity check, though self might not be in allWindows yet)
                if (w == self)
                {
                    myWindows.Add(w);
                    continue;
                }

                if (selfPawn != null)
                {
                    var otherPawn = (w as ICompanionWindow)?.Hediff?.pawn;
                    if (otherPawn != selfPawn)
                    {
                        // if (verbose) Log.Message($"  Skipping {w}: Pawn mismatch ({otherPawn} vs {selfPawn})");
                        continue;
                    }
                }
                myWindows.Add(w);
            }

            // Crucial: If self is not in the list (e.g. during SetInitialSizeAndPosition), add it!
            if (!myWindows.Contains(self))
            {
                if (verbose) Log.Message($"  Self not found in stack, adding manually.");
                myWindows.Add(self);
            }

            // Re-sort to ensure correct slot order (since we might have appended self at the end)
            myWindows.Sort((a, b) => GetWindowSlot(a).CompareTo(GetWindowSlot(b)));

            // 2. Calculate total stack dimensions
            float totalHeight = 0f;
            float maxWidth = 0f;

            foreach (var w in myWindows)
            {
                // Use passed size for self, current rect for others
                float h = (w == self) ? windowSize.y : w.windowRect.height;
                float width = (w == self) ? windowSize.x : w.windowRect.width;

                totalHeight += h;
                maxWidth = Mathf.Max(maxWidth, width);
            }

            // Add gaps
            if (myWindows.Count > 1) totalHeight += (myWindows.Count - 1) * VerticalStackGap;

            if (verbose) Log.Message($"  Stack: {myWindows.Count} windows, TotalH={totalHeight}, MaxW={maxWidth}");

            // 3. Position the "Super Stack"
            // This ensures the entire group fits and avoids the tooltip
            Rect stackRect = WindowPositionHelper.CalculateWindowRect(new Vector2(maxWidth, totalHeight));

            // 4. Find our position within the stack
            // We stack UPWARDS from the bottom (stackRect.yMax)
            float currentY = stackRect.yMax;

            foreach (var w in myWindows)
            {
                float h = (w == self) ? windowSize.y : w.windowRect.height;
                float width = (w == self) ? windowSize.x : w.windowRect.width;

                // Move cursor up to the top of this window
                currentY -= h;

                if (w == self)
                {
                    if (verbose) Log.Message($"  Found self at Y={currentY}");
                    return new Rect(stackRect.x, currentY, width, h);
                }

                // Add gap before next window (moving up)
                currentY -= VerticalStackGap;
            }

            return WindowPositionHelper.CalculateWindowRect(windowSize);
        }

        /// <summary>
        /// Update frame tracking - swap frame buffers when a new frame starts.
        /// </summary>
        private static void UpdateFrameTracking()
        {
            int currentFrame = Time.frameCount;
            if (currentFrame != lastFrameUpdated)
            {
                // New frame - move current to last, clear current
                activeHediffsLastFrame.Clear();
                var temp = activeHediffsLastFrame;
                activeHediffsLastFrame = activeHediffsThisFrame;
                activeHediffsThisFrame = temp;
                lastFrameUpdated = currentFrame;
            }
        }

        /// <summary>
        /// Clear all tracking (for cleanup purposes).
        /// </summary>
        public static void Clear()
        {
            activeHediffsThisFrame.Clear();
            activeHediffsLastFrame.Clear();
            windowSlots.Clear();
            nextSlot = 0;
            lastFrameUpdated = -1;
        }
    }
}
