using UnityEngine;
using Verse;

namespace DiseaseImmunityProgressTracker.UI
{
    /// <summary>
    /// Helper class for positioning tooltip companion windows.
    /// Uses the game's tooltip positioning logic to detect where the tooltip
    /// will appear, then positions our window to avoid overlap.
    /// </summary>
    public static class WindowPositionHelper
    {
        // Estimated tooltip dimensions for disease info tooltips
        private const float EstimatedTooltipWidth = 270f;
        private const float EstimatedTooltipHeight = 200f;

        // Tooltip positioning constants (from GenUI.GetMouseAttachedWindowPos)
        private const float TooltipOffsetBelow = 14f;
        private const float TooltipOffsetAbove = 5f;
        private const float TooltipOffsetX = 16f;

        // Our window positioning offsets
        private const float WindowOffsetFromMouse = 15f;
        private const float WindowGapAboveMouse = 5f;
        private const float WindowGapFromTooltip = 5f;

        /// <summary>
        /// Calculate the window position using the current mouse position.
        /// Uses Verse.UI.MousePositionOnUIInverted which works regardless of GUI matrix state.
        /// </summary>
        /// <param name="windowSize">Size of our window</param>
        /// <returns>The Rect for the window position</returns>
        public static Rect CalculateWindowRect(Vector2 windowSize)
        {
            // Use Verse.UI.MousePositionOnUIInverted - this uses Input.mousePosition and works
            // even inside DoWindowContents where Event.current.mousePosition is local coords
            Vector2 mousePos = Verse.UI.MousePositionOnUIInverted;
            return CalculateWindowRect(mousePos, windowSize);
        }

        /// <summary>
        /// Calculate where the game would position a tooltip of given size.
        /// Reimplements GenUI.GetMouseAttachedWindowPos logic using the provided mouse position
        /// (since GenUI version uses Event.current.mousePosition which may be in local coords).
        /// </summary>
        private static Vector2 CalculateTooltipPosition(Vector2 mousePos, float tooltipWidth, float tooltipHeight)
        {
            // Y position: prefer below mouse, fall back to above if doesn't fit
            float yPos;
            if (mousePos.y + TooltipOffsetBelow + tooltipHeight < Verse.UI.screenHeight)
            {
                // Fits below mouse
                yPos = mousePos.y + TooltipOffsetBelow;
            }
            else if (mousePos.y - TooltipOffsetAbove - tooltipHeight >= 0f)
            {
                // Fits above mouse
                yPos = mousePos.y - TooltipOffsetAbove - tooltipHeight;
            }
            else
            {
                // Doesn't fit either way, clamp to bottom
                yPos = Verse.UI.screenHeight - TooltipOffsetBelow - tooltipHeight;
            }

            // X position: prefer right of mouse, fall back to left if doesn't fit
            float xPos;
            if (mousePos.x + TooltipOffsetX + tooltipWidth < Verse.UI.screenWidth)
            {
                xPos = mousePos.x + TooltipOffsetX;
            }
            else
            {
                xPos = mousePos.x - 4f - tooltipWidth;
            }

            return new Vector2(xPos, yPos);
        }

        /// <summary>
        /// Calculate the window position for a tooltip companion window.
        /// Positions the window to avoid overlapping with the game's tooltip.
        /// </summary>
        /// <param name="mousePos">Mouse position in screen coordinates</param>
        /// <param name="windowSize">Size of our window</param>
        /// <returns>The Rect for the window position</returns>
        public static Rect CalculateWindowRect(Vector2 mousePos, Vector2 windowSize)
        {
            // Calculate where the tooltip would be positioned (using estimates)
            Vector2 tooltipPos = CalculateTooltipPosition(mousePos, EstimatedTooltipWidth, EstimatedTooltipHeight);

            // Determine if the tooltip is above or below the mouse by comparing Y positions
            bool tooltipIsBelowMouse = tooltipPos.y > mousePos.y;

            float xPos, yPos;

            // Strategy 1: Position Above Mouse (Normal case)
            if (tooltipIsBelowMouse)
            {
                // Calculate proposed Y position above mouse
                float proposedY = mousePos.y - windowSize.y - WindowGapAboveMouse;

                // Check if it fits on screen (top edge >= 0)
                if (proposedY >= 0)
                {
                    xPos = mousePos.x + WindowOffsetFromMouse;
                    yPos = proposedY;
                    goto Finalize;
                }

                // If it doesn't fit, fall through to fallback strategies
            }

            // Strategy 2: Position Right of Tooltip (Fallback or if Tooltip is Above)
            // Use estimated tooltip width to position to the right
            xPos = tooltipPos.x + EstimatedTooltipWidth + WindowGapFromTooltip;

            // Check if it fits on screen horizontally
            if (xPos + windowSize.x <= Verse.UI.screenWidth)
            {
                // Y position: Bottom-align with tooltip bottom to keep close to mouse/focus
                // Tooltip bottom = tooltipPos.y + EstimatedTooltipHeight
                float tooltipBottom = tooltipPos.y + EstimatedTooltipHeight;
                yPos = tooltipBottom - windowSize.y;
                goto Finalize;
            }

            // Strategy 3: Position Left of Tooltip (Last resort)
            xPos = tooltipPos.x - windowSize.x - WindowGapFromTooltip;

            // Y position: Bottom-align with tooltip bottom
            float tipBottom = tooltipPos.y + EstimatedTooltipHeight;
            yPos = tipBottom - windowSize.y;

            Finalize:
            // Final clamping to screen bounds
            if (xPos + windowSize.x > Verse.UI.screenWidth)
            {
                // Clamp right edge
                xPos = Verse.UI.screenWidth - windowSize.x;
            }
            if (xPos < 0)
            {
                xPos = 0;
            }
            if (yPos < 0)
            {
                yPos = 0;
            }
            if (yPos + windowSize.y > Verse.UI.screenHeight)
            {
                yPos = Verse.UI.screenHeight - windowSize.y;
            }

            return new Rect(xPos, yPos, windowSize.x, windowSize.y);
        }
    }
}
