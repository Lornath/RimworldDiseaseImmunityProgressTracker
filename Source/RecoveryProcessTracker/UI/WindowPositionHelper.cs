using UnityEngine;
using Verse;

namespace RecoveryProcessTracker.UI
{
    /// <summary>
    /// Helper class for positioning tooltip companion windows.
    /// Uses the game's tooltip positioning logic to detect where the tooltip
    /// will appear, then positions our window to avoid overlap.
    /// </summary>
    public static class WindowPositionHelper
    {
        // Estimated tooltip dimensions for disease info tooltips
        private const float EstimatedTooltipWidth = 260f;
        private const float EstimatedTooltipHeight = 200f;

        // Tooltip positioning constants (from GenVerse.UI.GetMouseAttachedWindowPos)
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
        /// Reimplements GenVerse.UI.GetMouseAttachedWindowPos logic using the provided mouse position
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
            // Calculate where the tooltip would be positioned
            Vector2 tooltipPos = CalculateTooltipPosition(mousePos, EstimatedTooltipWidth, EstimatedTooltipHeight);

            // Determine if the tooltip is above or below the mouse by comparing Y positions
            bool tooltipIsBelowMouse = tooltipPos.y > mousePos.y;

            float xPos, yPos;

            if (tooltipIsBelowMouse)
            {
                // Normal case: tooltip is below mouse, position our window above
                xPos = mousePos.x + WindowOffsetFromMouse;
                yPos = mousePos.y - windowSize.y - WindowGapAboveMouse;
            }
            else
            {
                // Tooltip is above mouse (near bottom of screen)
                // Position our window to the right of the tooltip
                xPos = tooltipPos.x + EstimatedTooltipWidth + WindowGapFromTooltip;

                // If that doesn't fit, try to the left of the tooltip
                if (xPos + windowSize.x > Verse.UI.screenWidth)
                {
                    xPos = tooltipPos.x - windowSize.x - WindowGapFromTooltip;
                }

                // Y position: bottom-align with the tooltip
                // Tooltip bottom = tooltipPos.y + EstimatedTooltipHeight
                // Our window bottom should match, so our top = tooltip bottom - our height
                float tooltipBottom = tooltipPos.y + EstimatedTooltipHeight;
                yPos = tooltipBottom - windowSize.y;
            }

            // Final clamping to screen bounds
            if (xPos + windowSize.x > Verse.UI.screenWidth)
            {
                xPos = mousePos.x - windowSize.x - WindowOffsetFromMouse;
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
