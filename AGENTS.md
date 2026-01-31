# Agent Guidance

This file provides guidance when working with code in this repository.

## Project Overview

See the README.md file in the root dir for the overview.

## Build Commands

After making changes, always build the mod with:

```powershell
dotnet build "Source/RecoveryProcessTracker/RecoveryProcessTracker.csproj"
```

This should produce:
- `Assemblies\RecoveryProcessTracker.dll` (the mod assembly)
- `Assemblies\RecoveryProcessTracker.pdb` (debug symbols)

Expected output: `Build succeeded. 0 Warning(s) 0 Error(s)`

**Deploy to RimWorld:**

Tell the user to run this to deploy, do not attempt to deploy automatically:

```powershell
.\deploy.ps1
```

## Decompiled Reference Code

Shared RimWorld decompiled source lives at `../decompiled/RimWorld/` (parent directory).
Mod-specific decompiled references live in `./decompiled/`.

**DO NOT ATTEMPT TO FULLY READ THESE FILES** - Some of them are quite large. Search for relevant code and only read specific ranges.

The Rimworld decompiled source code in `../decompiled/RimWorld/` is a large codebase, so there's a file listing provided in `../decompiled/RimWorld/file_listing.txt` which is just one filename (full relative path) per line.  There's also a ctags database, so the "readtags" command should work in the `../decompiled/RimWorld` directory to search for symbols.  The XML defs for the core game and all DLCs are stored in the `../decompiled/RimWorld/Data` directory.

There is the source code for a similar mod to ours in "decompiled\AmIGonnaMakeItDoc\RW.AmIGonnaMakeItDoc" that may provide some useful example code.

## Architecture

### Disease Type Classification

The mod tracks diseases using a type system based on their cure mechanics:

| Type | Name | Examples | Cure Mechanism | Key Components | UI Window |
|------|------|----------|----------------|----------------|-----------|
| **Type 1** | Immunizable | Plague, Flu, Malaria, Sleeping Sickness | Immunity races severity; first to 100% wins | `HediffComp_Immunizable` (with immunity gain) | `DiseaseGraphWindow` |
| **Type 2** | Cumulative Tend | Gut Worms, Muscle Parasites | Accumulated tend quality reaches threshold (typically 300%) | `HediffComp_TendDuration` with `disappearsAtTotalTendQuality >= 0` | `CumulativeTendWindow` |
| **Type 3a** | Mechanites | Fibrous Mechanites, Sensory Mechanites | Time-based (1-2 quadrums); treatment controls severity/pain only | `HediffComp_Disappears` + `HediffComp_Immunizable` (NO immunity gain) | `TimeBasedWindow` |
| **Type 3b** | Fatal Rots | Lung Rot, Blood Rot | Time-based; treatment prevents fatal severity | `HediffComp_Disappears` + `HediffComp_TendDuration` (no Immunizable) | `TimeBasedWindow` |

**Type 3a vs 3b Key Differences:**
- **Type 3a (Mechanites)**: Non-fatal. Severity controls pain level (0-49% = mild 20% pain, 50-100% = intense 60% pain). Has `HediffComp_Immunizable` but `immunityPerDaySick = 0` (no immunity gain).
- **Type 3b (Fatal Rots)**: Potentially fatal. Severity can reach lethal levels if untreated. Has `lethalSeverity > 0`.

**Detection Order in Code:**
Type 3 diseases are checked FIRST (before Type 1) because Type 3a Mechanites have `HediffComp_Immunizable` which would otherwise cause them to be misclassified as Type 1.

### Core Components

- **DiseaseTracker** (`Source/RecoveryProcessTracker/Core/DiseaseTracker.cs`):
    - A `GameComponent` that tracks disease progression history for all pawns.
    - Maintains a dictionary of `DiseaseHistory` objects keyed by hediff load ID.
    - Tracks diseases by type (see Disease Type Classification below).
    - Contains `IsTimeBasedDisease()` static helper to identify Type 3 diseases (both 3a and 3b).
    - Contains `IsMechaniteDisease()` static helper to identify Type 3a mechanite diseases.
    - Handles data persistence (Save/Load) via `ExposeData`.

- **PrognosisCalculator** (`Source/RecoveryProcessTracker/Core/PrognosisCalculator.cs`):
    - Static utility class that calculates disease prognosis (survival chance, time to immunity/death).
    - Can use current rates (instantaneous) or historical data (observed rates over time) for better accuracy.
    - Returns a `PrognosisResult` struct containing all calculated metrics.

### UI Components

- **DiseaseGraphWindow** (`Source/RecoveryProcessTracker/UI/DiseaseGraphWindow.cs`):
    - A `Window` subclass that renders the disease progression graph.
    - Draws historical data (past) and projected trends (future).
    - Features:
        - Immunity (Green) and Severity (Red) trend lines with projection into the future.
        - Projection lines stop at 100% with a circle marker (no horizontal continuation).
        - Projections are disabled once disease outcome is resolved (immunity or severity >= 100%).
        - "Verdict" text predicting the outcome (Immune, Death, etc.).
        - Blue background regions indicating bed rest periods, with brightness varying by bed quality (dim for sleeping spot, bright for hospital bed with vitals monitor).
        - Yellow vertical lines for tending events, with medicine icon and quality below the x-axis.
        - Legend positioned in bottom-right corner to avoid overlap with high trend lines.
        - Tooltip-like behavior: positions itself near the mouse/tooltip and closes when the tooltip updates.
    - **IMPORTANT: No tooltips allowed** - This window acts as a companion to the game's disease tooltip. When the mouse moves off the disease entry in the health tab, this window closes. Therefore, `TooltipHandler.TipRegion()` cannot be used for interactive elements within this window since users cannot hover over them without triggering the window to close.

- **CumulativeTendWindow** (`Source/RecoveryProcessTracker/UI/CumulativeTendWindow.cs`):
    - A `Window` subclass that displays cumulative tend progress for diseases like Gut Worms and Muscle Parasites.
    - These diseases cure through accumulated tend quality (typically 300% total) rather than immunity racing.
    - Features:
        - Progress display mirroring in-game tooltip format (e.g., "Progress: 53.2% / 300%").
        - Visual progress bar showing completion percentage.
        - Recent tends list (up to 3) with medicine icon, quality added, and doctor name.
        - Time and tend count estimates based on historical average tend quality and interval.
    - Uses reflection to access private `totalTendQuality` field in `HediffComp_TendDuration`.
    - Same tooltip-companion behavior as `DiseaseGraphWindow`.

- **TimeBasedWindow** (`Source/RecoveryProcessTracker/UI/TimeBasedWindow.cs`):
    - A `Window` subclass that displays time-based disease progress for Type 3 diseases.
    - Handles both Type 3a (Mechanites) and Type 3b (Fatal Rots).
    - These diseases cure when a countdown timer expires; treatment manages severity while waiting.
    - Features:
        - Time remaining countdown with progress bar showing % complete toward cure.
        - Current severity display with color-coded urgency (green/yellow/red).
        - Mini severity graph showing historical trend and projection to cure time.
        - Tending status indicator ("Currently tended" / "Needs tending").
        - Recent tends list (up to 3) with medicine icon, quality, and doctor name.
        - Verdict text predicting outcome based on projected final severity.
    - **Type 3a (Mechanite) specific features**:
        - Shows pain level: "Severity: 35% (Mild pain, 20%)" or "Severity: 65% (Intense pain, 60%)"
        - Draws orange pain threshold line at 50% severity on graph with "Pain ++" label
        - Verdict focuses on pain management: "SAFE - Pain controlled", "WARNING - Pain will intensify", "INTENSE PAIN - Keep tended"
    - **Type 3b (Fatal Rot) features**:
        - Standard severity display and danger-based verdict
        - Verdict focuses on survival: "SAFE - Will survive", "DANGER - Severity may reach fatal levels"
    - Same tooltip-companion behavior as `DiseaseGraphWindow`.

- **TooltipCompanionPatch** (`Source/RecoveryProcessTracker/Patches/TooltipCompanionPatch.cs`):
    - Harmony patch on `HediffComp_Immunizable.CompTipStringExtra`.
    - Detects when a Type 1 (immunizable) disease tooltip is shown and opens `DiseaseGraphWindow`.
    - Skips Type 3a (Mechanites) which have Immunizable but should use `TimeBasedWindow`.

- **CumulativeTendPatch** (`Source/RecoveryProcessTracker/Patches/CumulativeTendPatch.cs`):
    - Harmony patch on `HediffComp_TendDuration.CompTipStringExtra`.
    - Detects Type 2 (cumulative tend) diseases where `TProps.disappearsAtTotalTendQuality >= 0`.
    - Skips diseases that also have `HediffComp_Immunizable` (handled by `TooltipCompanionPatch`).
    - Opens/positions the `CumulativeTendWindow` for qualifying diseases.

- **TimeBasedDiseasePatch** (`Source/RecoveryProcessTracker/Patches/TimeBasedDiseasePatch.cs`):
    - Harmony patch on `HediffComp_TendDuration.CompTipStringExtra` (same as CumulativeTendPatch).
    - Note: `HediffComp_Disappears` doesn't have `CompTipStringExtra`, so we patch the tend component instead.
    - Detects Type 3 diseases (both 3a and 3b) using `DiseaseTracker.IsTimeBasedDisease()`.
    - Allows Type 3a (Mechanites) through even though they have `HediffComp_Immunizable`.
    - Filters out Type 1 (true immunizable) and Type 2 (cumulative tend) diseases.
    - Opens/positions the `TimeBasedWindow` for qualifying diseases.

- **TendUtilityPatch** (`Source/RecoveryProcessTracker/Patches/TendUtilityPatch.cs`):
    - Harmony patches to capture tending events for diseases.
    - `TendingContext`: Static class that holds context during a tend operation (doctor name, medicine used, skill level).
    - `TendUtility_DoTend_Patch`: Prefix/Postfix on `TendUtility.DoTend` to set up and tear down tending context.
    - `Hediff_Tended_Patch`: Postfix on `HediffWithComps.Tended` to record the tend event.
    - Records tends for all three disease types: immunizable, cumulative tend, and time-based.
    - **Note on tend quality**: The `quality` parameter passed to `Hediff.Tended` is the BASE quality. The actual displayed quality includes ±25% random variance applied in `HediffComp_TendDuration.CompTended`. Always read the final quality from `HediffComp_TendDuration.tendQuality` after tending completes.

### Data Structures (in DiseaseTracker.cs)

- **DiseaseDataPoint**: Records immunity and severity at a specific tick (for Type 1 diseases).
- **TimeBasedDataPoint**: Records severity and time remaining at a specific tick (for Type 3 diseases):
    - `Tick`: When this data point was recorded
    - `Severity`: Current severity (0-1)
    - `TicksRemaining`: Ticks until cure
    - `TotalDuration`: Original duration when disease started
    - `WasTended`: Whether the disease was actively tended at this point
- **BedRestInterval**: Tracks periods when the pawn was in bed:
    - `StartTick`, `EndTick`: Time range (EndTick=-1 means ongoing)
    - `ImmunityGainSpeedFactor`: The bed's immunity gain speed stat (1.0 = no bonus like sleeping spot, ~1.07+ = hospital bed, higher with vitals monitor). Used to vary the blue background brightness in the graph.
- **TendingEvent**: Records a tending action with:
    - `Tick`: When it happened
    - `DoctorName`, `DoctorSkill`: Who performed the tend
    - `MedicineName`: Display label of medicine used
    - `MedicineDefName`: DefName for looking up the medicine's icon (null if no medicine used)
    - `Quality`: The final tend quality (after random variance)
- **DiseaseHistory**: Container for all tracking data for a single disease instance (supports all three disease types).

### Source Files

```
Source/RecoveryProcessTracker/
├── RecoveryProcessTracker.csproj       # Build configuration
├── RecoveryProcessTrackerMod.cs        # Main mod class & settings
├── Core/
│   ├── DiseaseTracker.cs               # Data tracking & persistence
│   └── PrognosisCalculator.cs          # Math & prediction logic
├── UI/
│   ├── DiseaseGraphWindow.cs           # Graph rendering for Type 1 (immunizable) diseases
│   ├── CumulativeTendWindow.cs         # Progress display for Type 2 (cumulative tend) diseases
│   ├── TimeBasedWindow.cs              # Time/severity display for Type 3 (time-based) diseases
│   └── WindowPositionHelper.cs         # Shared tooltip-companion window positioning logic
└── Patches/
    ├── TooltipCompanionPatch.cs        # Harmony patch for Type 1 disease tooltips
    ├── CumulativeTendPatch.cs          # Harmony patch for Type 2 disease tooltips
    ├── TimeBasedDiseasePatch.cs        # Harmony patch for Type 3 disease tooltips
    └── TendUtilityPatch.cs             # Harmony patch for capturing tend events
```

## Mod Structure

Standard RimWorld mod layout:
- `About/About.xml` - Mod metadata
- `Assemblies/` - Compiled DLLs (build output)
- `Source/` - C# source code
- `Libs/` - External references (not in repo)

## Important Constraints

- Target framework: .NET Framework 4.7.2
- Must be compatible with RimWorld 1.6
- Load order: After Harmony

## RimWorld UI/Asset References

- **No-medicine icon**: `ContentFinder<Texture2D>.Get("UI/Icons/Medical/NoMeds")` - The hand icon used for "Doctor care, no medicine" in the Level of medical care settings. Used as fallback when a tend is performed without medicine.
- **Medicine icons**: Access via `ThingDef.uiIcon` after looking up the def with `DefDatabase<ThingDef>.GetNamedSilentFail(defName)`.

## UI Text Sizing Guidelines

**Text clipping is a common issue.** Unity/RimWorld will clip text that doesn't fit in its label rect. Follow these guidelines to prevent it:

### Minimum Label Heights by Font
- **GameFont.Small**: Use at least **24f height** for labels. This font has larger descenders (g, y, p, q, j) that get clipped with smaller heights.
- **GameFont.Tiny**: Use at least **16-18f height**. Even tiny font needs room for descenders.
- **GameFont.Medium**: Use at least **28-30f height**.

### Label Width Considerations
- **Always size for the longest possible content.** If a label might show "0%" or "100%", size it for "100%" (approximately 36f width for GameFont.Tiny).
- **Word wrapping occurs silently** if width is too narrow. Text will wrap to multiple lines but only the first line shows in a single-line-height rect, making it appear garbled/unreadable.
- **Percentage labels**: "100%" needs ~36f width in GameFont.Tiny. Single/double digit percentages need less but always plan for the max.

### Common Mistakes
1. **Using 12f or 14f height for labels** - Almost always too small. Start with 16-18f minimum.
2. **Calculating width from margins** like `margin - 4f` - This can result in widths that are too narrow. Use explicit widths sized for content.
3. **Placing labels outside their parent rect** - Labels positioned with negative offsets (e.g., `y - 6f`) may extend outside the clipping region.
4. **Forgetting about TextAnchor** - Even with `TextAnchor.MiddleCenter`, the rect must be large enough to contain the full text.

### Testing Tips
- Test with maximum-length values (100%, longest names, etc.)
- Check labels at graph extremes (0% and 100% on axes)
- Verify descenders aren't clipped by typing test strings with "g", "y", "p", "q", "j"

## Unity GUI Coordinate Systems

**Critical: Mouse position behaves differently depending on context.** Understanding when to use which method is essential for correct window positioning.

### Mouse Position Methods

| Method | Coordinate Space | When to Use |
|--------|------------------|-------------|
| `Event.current.mousePosition` | Local to current GUI context | Only during GUI drawing (`OnGUI`, `DoWindowContents`) when you want window-local coords |
| `Verse.UI.MousePositionOnUIInverted` | Screen coordinates | Anytime - works regardless of GUI matrix state |
| `GenUI.GetMouseAttachedWindowPos()` | Screen coordinates | Only during GUI drawing (uses `Event.current.mousePosition` internally) |

### The Window Coordinate Trap

Inside a `Window.DoWindowContents(Rect inRect)` method, the GUI matrix has been transformed so that coordinates are relative to the window's content area, **not** the screen.

**Problem:** If you call `Event.current.mousePosition` inside `DoWindowContents()`, you get coordinates relative to the window, not screen coordinates. Using these for positioning another window or calculating screen positions will give wrong results.

**Symptoms of this bug:**
- Window positioned far off from where expected (e.g., upper-left or offset by the parent window's position)
- Window movement appears "accelerated" (e.g., mouse moves 5px, window moves 10px) due to coordinate space mismatch

**Solution:** Use `Verse.UI.MousePositionOnUIInverted` which uses `Input.mousePosition` directly and works regardless of GUI matrix state.

### Window Update Methods

| Method | Called During | `Event.current` Valid? |
|--------|---------------|------------------------|
| `SetInitialSizeAndPosition()` | Window opening | Yes (if opened during GUI) |
| `DoWindowContents()` | GUI drawing | Yes, but in **local** coordinates |
| `WindowUpdate()` | Game update loop | No - may be null, stale, or wrong type |

**Key insight:** If you need to update window position dynamically:
- Do it in `DoWindowContents()` (not `WindowUpdate()`)
- Use `Verse.UI.MousePositionOnUIInverted` for screen-space mouse position
- If replicating tooltip positioning logic, reimplement it using your own mouse position rather than calling `GenUI.GetMouseAttachedWindowPos()` (which uses `Event.current.mousePosition` internally)

### Namespace Collision

This project uses namespace `RecoveryProcessTracker.UI`. RimWorld's UI utilities are in `Verse.UI`. Always use fully-qualified `Verse.UI.screenWidth`, `Verse.UI.MousePositionOnUIInverted`, etc. to avoid ambiguity.
