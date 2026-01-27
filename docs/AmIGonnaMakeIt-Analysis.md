# "Am I Gonna Make It, Doc?" Mod Analysis

This is a well-structured mod that provides disease prognosis information. Here's how it works:

## Architecture Overview

The mod has 4 main components:

| Component | Purpose |
|-----------|---------|
| `Prognosis_TooltipPatch` | Adds "Prognosis: X" line to disease tooltips |
| `PrognosisDoctorMemory` | GameComponent tracking which pawns have been tended by skilled doctors |
| `Prognosis_RiskAlert` | Sends red alert letters when diseases are "at risk" |
| `ModSettings` | Configuration UI |

## Core Algorithm (The Key Part)

The prognosis calculation is in `Prognosis_TooltipPatch.Verdict()` (lines 187-196 of `Patch_HediffComp_Immunizable.cs`):

```csharp
float dImm = DaysSafe((1f - curImm) / immPerDay);   // days until immunity reaches 100%
float dMax = DaysSafe((1f - curSev) / sevPerDay);   // days until severity reaches 100% (death)
return dImm < dMax ? "Likely immune" : "At risk";
```

It calculates:
1. **Days to full immunity** = (1.0 - current_immunity) / immunity_gain_per_day
2. **Days to death** = (1.0 - current_severity) / severity_increase_per_day
3. If immunity wins the race → "Likely immune", otherwise → "At risk"

## Data Sources

The mod pulls these values from RimWorld's API:
- `rec.ImmunityChangePerTick(pawn, true, hediff) * 60000f` → immunity gain per day
- `immComp.SeverityChangePerDay()` → disease progression per day
- `rec.immunity` → current immunity %
- `hediff.Severity` → current severity %

## The "Doctor Gate" System

The mod only shows prognosis after a pawn has been tended by a doctor with sufficient Medicine skill (default 12). This prevents instant prognosis viewing before any diagnosis.

The gate is tracked per-pawn, per-disease in `PrognosisDoctorMemory` and persists across saves.

## What It Displays

Just a simple text verdict in the tooltip:
- **"Stable"** - no immunity gain and no severity gain
- **"Improving"** - severity decreasing (disease retreating)
- **"Likely immune"** - immunity will win the race
- **"At risk"** - severity will reach 100% first

---

## Observations for Our Mod

**What the existing mod does NOT do (our opportunity):**
1. No graphical timeline/graph visualization
2. No trend lines showing projections
3. No historical tracking of treatments over time
4. No visualization of treatment quality impact
5. Doesn't show the actual immunity gain factors (bed quality, vitals monitor, etc.)

**Key RimWorld APIs we need:**
- `HediffComp_Immunizable` - the disease component with immunity tracking
- `ImmunityRecord.ImmunityChangePerTick()` - gets immunity gain rate
- `HediffComp_Immunizable.SeverityChangePerDay()` - gets disease progression rate
- `ImmunityHandler` - manages all immunity records for a pawn

**For a graphical display:**
RimWorld uses Unity's IMGUI. Tooltips are fairly limited, but we could:
1. **Custom window/tab** - Add a tab to the Health panel
2. **Overlay window** - Pop up when hovering over a disease (more complex)
3. **Draw directly** using `GUI.DrawTexture()` and procedural line drawing

## Source Files Reference

Located in `decompiled/AmIGonnaMakeItDoc/RW.AmIGonnaMakeItDoc/`:
- `Patch_HediffComp_Immunizable.cs` - Main Harmony patches and tooltip logic
- `Prognosis_DoctorMemory.cs` - GameComponent for tracking doctor skill records
- `Prognosis_RiskAlert.cs` - Alert letter system
- `ModSettings.cs` - Settings UI
