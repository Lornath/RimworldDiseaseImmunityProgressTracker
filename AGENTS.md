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

There is the source code for a similar mod to ours in "decompiled\AmIGonnaMakeItDoc\RW.AmIGonnaMakeItDoc" that may provide some useful example code.

## Architecture

### Core Components

- **DiseaseTracker** (`Source/RecoveryProcessTracker/Core/DiseaseTracker.cs`):
    - A `GameComponent` that tracks disease progression history for all pawns.
    - Maintains a dictionary of `DiseaseHistory` objects keyed by hediff load ID.
    - Updates every ~1 hour (2500 ticks) to record immunity and severity data points.
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
        - Immunity (Green) and Severity (Red) trend lines.
        - "Verdict" text predicting the outcome (Immune, Death, etc.).
        - Blue background regions indicating bed rest periods.
        - Yellow vertical lines for tending events, with medicine icon and quality below the x-axis.
        - Tooltip-like behavior: positions itself near the mouse/tooltip and closes when the tooltip updates.
    - **IMPORTANT: No tooltips allowed** - This window acts as a companion to the game's disease tooltip. When the mouse moves off the disease entry in the health tab, this window closes. Therefore, `TooltipHandler.TipRegion()` cannot be used for interactive elements within this window since users cannot hover over them without triggering the window to close.

- **TooltipCompanionPatch** (`Source/RecoveryProcessTracker/Patches/TooltipCompanionPatch.cs`):
    - Harmony patch on `HediffComp_Immunizable.CompTipStringExtra`.
    - Detects when a disease tooltip is shown and automatically opens/positions the `DiseaseGraphWindow`.

- **TendUtilityPatch** (`Source/RecoveryProcessTracker/Patches/TendUtilityPatch.cs`):
    - Harmony patches to capture tending events for diseases.
    - `TendingContext`: Static class that holds context during a tend operation (doctor name, medicine used, skill level).
    - `TendUtility_DoTend_Patch`: Prefix/Postfix on `TendUtility.DoTend` to set up and tear down tending context.
    - `Hediff_Tended_Patch`: Postfix on `HediffWithComps.Tended` to record the tend event.
    - **Note on tend quality**: The `quality` parameter passed to `Hediff.Tended` is the BASE quality. The actual displayed quality includes ±25% random variance applied in `HediffComp_TendDuration.CompTended`. Always read the final quality from `HediffComp_TendDuration.tendQuality` after tending completes.

### Data Structures (in DiseaseTracker.cs)

- **DiseaseDataPoint**: Records immunity and severity at a specific tick.
- **BedRestInterval**: Tracks periods when the pawn was in bed (StartTick, EndTick; EndTick=-1 means ongoing).
- **TendingEvent**: Records a tending action with:
    - `Tick`: When it happened
    - `DoctorName`, `DoctorSkill`: Who performed the tend
    - `MedicineName`: Display label of medicine used
    - `MedicineDefName`: DefName for looking up the medicine's icon (null if no medicine used)
    - `Quality`: The final tend quality (after random variance)
- **DiseaseHistory**: Container for all tracking data for a single disease instance.

### Source Files

```
Source/RecoveryProcessTracker/
├── RecoveryProcessTracker.csproj       # Build configuration
├── RecoveryProcessTrackerMod.cs        # Main mod class & settings
├── Core/
│   ├── DiseaseTracker.cs               # Data tracking & persistence
│   └── PrognosisCalculator.cs          # Math & prediction logic
├── UI/
│   └── DiseaseGraphWindow.cs           # Graph rendering
└── Patches/
    ├── TooltipCompanionPatch.cs        # Harmony patch for tooltip integration
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
