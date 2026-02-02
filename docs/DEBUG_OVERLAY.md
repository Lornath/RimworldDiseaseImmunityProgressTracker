# Debug Overlay for Window Positioning

This document describes how to temporarily add a debug overlay for troubleshooting window positioning and overlap issues during development.

## What the Debug Overlay Does

The debug overlay draws on top of RimWorld's UI to visualize:

- **Yellow crosshair**: Current mouse position with coordinates
- **Green/Red box labeled "EST TOOLTIP"**: Estimated position of the game's disease tooltip
- **Cyan/Red boxes**: Outlines of all open companion windows (DiseaseGraph, CumulativeTend, TimeBased)
- **"DEBUG OVERLAY ACTIVE"**: Status text in top-left corner

Colors turn **red** when overlaps are detected, and overlap events are logged to the RimWorld player.log as warnings.

## Adding the Debug Overlay

### Step 1: Add Helper Methods to WindowPositionHelper.cs

Add these methods to `Source/RecoveryProcessTracker/UI/WindowPositionHelper.cs` after the `CalculateWindowRect(Vector2 windowSize)` method:

```csharp
/// <summary>
/// Get the estimated tooltip rect for debug visualization.
/// </summary>
public static Rect GetEstimatedTooltipRect()
{
    Vector2 mousePos = Verse.UI.MousePositionOnUIInverted;
    Vector2 tooltipPos = CalculateTooltipPosition(mousePos, EstimatedTooltipWidth, EstimatedTooltipHeight);
    return new Rect(tooltipPos.x, tooltipPos.y, EstimatedTooltipWidth, EstimatedTooltipHeight);
}

/// <summary>
/// Get the current mouse position in screen coordinates.
/// </summary>
public static Vector2 GetMousePosition()
{
    return Verse.UI.MousePositionOnUIInverted;
}
```

### Step 2: Create DebugOverlayLoader.cs

Create `Source/RecoveryProcessTracker/Core/DebugOverlayLoader.cs`:

```csharp
using UnityEngine;
using Verse;
using RecoveryProcessTracker.UI;

namespace RecoveryProcessTracker.Core
{
    /// <summary>
    /// Initializes the Unity-based debug overlay when the game starts.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DebugOverlayLoader
    {
        private static GameObject overlayObject;

        static DebugOverlayLoader()
        {
            // Create a persistent GameObject for our debug overlay
            overlayObject = new GameObject("RecoveryProcessTracker_DebugOverlay");
            Object.DontDestroyOnLoad(overlayObject);

            // Add the controller component
            overlayObject.AddComponent<DebugOverlayController>();

            if (RecoveryProcessTrackerMod.Settings?.verboseLogging == true)
            {
                Log.Message("[RecoveryProcessTracker] Debug overlay initialized");
            }
        }
    }
}
```

### Step 3: Create DebugOverlayController.cs

Create `Source/RecoveryProcessTracker/UI/DebugOverlayController.cs`:

```csharp
using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace RecoveryProcessTracker.UI
{
    /// <summary>
    /// A raw Unity MonoBehaviour that draws debug info on top of the RimWorld UI.
    /// This bypasses the RimWorld WindowStack entirely, preventing input blocking and z-ordering issues.
    /// </summary>
    public class DebugOverlayController : MonoBehaviour
    {
        private HashSet<string> loggedOverlaps = new HashSet<string>();
        private bool lastVerboseState = false;

        public void OnGUI()
        {
            // Only draw if verbose logging is enabled
            if (RecoveryProcessTrackerMod.Settings == null || !RecoveryProcessTrackerMod.Settings.verboseLogging)
            {
                if (lastVerboseState)
                {
                    loggedOverlaps.Clear();
                    lastVerboseState = false;
                }
                return;
            }
            lastVerboseState = true;

            // Force this to draw on top of everything
            GUI.depth = -1000;

            // Save state
            Color oldColor = GUI.color;
            TextAnchor oldAnchor = Text.Anchor;
            GameFont oldFont = Text.Font;

            try
            {
                DrawOverlay();
            }
            finally
            {
                // Restore state
                GUI.color = oldColor;
                Text.Anchor = oldAnchor;
                Text.Font = oldFont;
            }
        }

        private void DrawOverlay()
        {
            // 1. Draw mouse crosshair (yellow)
            Vector2 mousePos = WindowPositionHelper.GetMousePosition();

            GUI.color = Color.yellow;
            GUI.DrawTexture(new Rect(mousePos.x - 20f, mousePos.y, 40f, 1f), BaseContent.WhiteTex);
            GUI.DrawTexture(new Rect(mousePos.x, mousePos.y - 20f, 1f, 40f), BaseContent.WhiteTex);

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.Label(new Rect(mousePos.x + 5, mousePos.y + 5, 100, 20), $"({mousePos.x:F0}, {mousePos.y:F0})");

            // Prepare rectangles
            Rect tooltipRect = WindowPositionHelper.GetEstimatedTooltipRect();
            var companionWindows = CompanionWindowManager.GetOpenCompanionWindows();

            // Default colors
            Color tooltipColor = Color.green;
            Dictionary<Window, Color> windowColors = new Dictionary<Window, Color>();
            foreach (var w in companionWindows) windowColors[w] = Color.cyan;

            // Detect overlaps
            // Check tooltip vs windows
            foreach (var window in companionWindows)
            {
                if (tooltipRect.Overlaps(window.windowRect))
                {
                    tooltipColor = Color.red;
                    windowColors[window] = Color.red;
                    LogOverlap("Tooltip", tooltipRect, window);
                }
            }

            // Check window vs window
            for (int i = 0; i < companionWindows.Count; i++)
            {
                for (int j = i + 1; j < companionWindows.Count; j++)
                {
                    if (companionWindows[i].windowRect.Overlaps(companionWindows[j].windowRect))
                    {
                        windowColors[companionWindows[i]] = Color.red;
                        windowColors[companionWindows[j]] = Color.red;
                        LogOverlap(companionWindows[i], companionWindows[j]);
                    }
                }
            }

            // 2. Draw estimated tooltip rect
            DrawBox(tooltipRect, tooltipColor, 2);
            GUI.color = tooltipColor;
            GUI.Label(new Rect(tooltipRect.x + 4, tooltipRect.y + 4, 80, 20), "EST TOOLTIP");

            // 3. Draw outlines for all open companion windows
            foreach (var window in companionWindows)
            {
                Color winColor = windowColors[window];
                DrawBox(window.windowRect, winColor, 2);

                string label = window.GetType().Name.Replace("Window", "");
                GUI.color = winColor;
                GUI.Label(new Rect(window.windowRect.x + 4, window.windowRect.y + 4, 150, 20), label);
            }

            // 4. Draw status text in top-left
            GUI.color = Color.white;
            GUI.Label(new Rect(10, 10, 300, 24), "DEBUG OVERLAY ACTIVE (Unity OnGUI)");
        }

        private void LogOverlap(string item1Type, Rect item1Rect, Window window)
        {
            string pawnName;
            string windowId = GetWindowStableId(window, out pawnName);
            pawnName = pawnName ?? "Unknown";

            string key = $"{pawnName}|{item1Type}|{windowId}";
            if (loggedOverlaps.Add(key))
            {
                Log.Warning($"[RecoveryProcessTracker] OVERLAP DETECTED ({pawnName}): {item1Type} ({item1Rect}) vs {windowId} @ {window.windowRect}");
            }
        }

        private void LogOverlap(Window w1, Window w2)
        {
            string pName1, pName2;
            string id1 = GetWindowStableId(w1, out pName1);
            string id2 = GetWindowStableId(w2, out pName2);
            string pawnName = pName1 ?? pName2 ?? "Unknown";

            // Sort IDs to make key order-independent
            string key;
            if (string.Compare(id1, id2) <= 0) key = $"{pawnName}|{id1}|{id2}";
            else key = $"{pawnName}|{id2}|{id1}";

            if (loggedOverlaps.Add(key))
            {
                Log.Warning($"[RecoveryProcessTracker] OVERLAP DETECTED ({pawnName}): {id1} @ {w1.windowRect} vs {id2} @ {w2.windowRect}");
            }
        }

        private string GetWindowStableId(Window window, out string pawnName)
        {
            pawnName = null;
            string type = window.GetType().Name;
            string extra = "";

            if (window is ICompanionWindow companion)
            {
                if (companion.Hediff != null)
                {
                    extra = $" [{companion.Hediff.def.defName}]";
                    if (companion.Hediff.pawn != null)
                    {
                        pawnName = companion.Hediff.pawn.LabelShort;
                    }
                }
            }
            return $"{type}{extra}";
        }

        private void DrawBox(Rect rect, Color color, int thickness)
        {
            GUI.color = color;
            // Top
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), BaseContent.WhiteTex);
            // Bottom
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), BaseContent.WhiteTex);
            // Left
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), BaseContent.WhiteTex);
            // Right
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), BaseContent.WhiteTex);
        }
    }
}
```

### Step 4: Enable Verbose Logging

In RimWorld, go to **Options > Mod Settings > Recovery Process Tracker** and enable **Verbose Logging**.

The debug overlay will now appear when hovering over diseases in the health tab.

## Why Use a Unity MonoBehaviour?

The overlay uses a raw Unity `MonoBehaviour` with `OnGUI()` instead of a RimWorld `Window` because:

1. **No input blocking**: RimWorld windows capture mouse events, which would interfere with the health tab tooltips we're trying to debug
2. **Z-order control**: `GUI.depth = -1000` ensures the overlay draws on top of everything
3. **Bypasses WindowStack**: The overlay exists outside RimWorld's window management, so it doesn't affect window stacking calculations

## Removing the Debug Overlay

After debugging, simply delete the two files you created:
- `Source/RecoveryProcessTracker/Core/DebugOverlayLoader.cs`
- `Source/RecoveryProcessTracker/UI/DebugOverlayController.cs`

And remove the helper methods from `WindowPositionHelper.cs`:
- `GetEstimatedTooltipRect()`
- `GetMousePosition()`

Then rebuild the mod.
