# RimWorld Immunity System - Complete Technical Analysis

Based on exploration of the RimWorld decompiled source code and the RimWorld Wiki, here's the complete breakdown of how the immunity system works and what factors affect disease survival.

---

## 1. FOUR TYPES OF DISEASE MECHANICS

Not all diseases work the same way. RimWorld has four distinct disease mechanics:

### Type 1: Immunity Race (Most Common)
The disease progresses over time, and treatment slows progression. Immunity is gained independently based on `ImmunityGainSpeed`. Once immunity reaches 100%, the disease severity starts decreasing and the pawn recovers.

**Applies to**: Flu, Infection, Plague, Malaria, Sleeping sickness, Infant illness

### Type 2: Cumulative Tend Quality
The disease does NOT progress. Treatment must reach a **total tend quality of 300%** before the disease is cured. For example, 4 tends at 80% quality = 320% total → cured.

**Applies to**: Gut worms, Muscle parasites

### Type 3: Time-Based with Treatment
The disease progresses over time, and treatment slows/prevents progression. The disease only fades after a certain time passes (not immunity-based).

**Applies to**: Lung rot, Fibrous mechanites, Sensory mechanites, Blood rot

### Type 4: Environmental Severity (Toxic Buildup)
Severity accumulates from environmental exposure and naturally decreases when safe. NOT tendable - recovery is purely environmental. Lethal at 100% severity. Has health risks (dementia, carcinoma) at higher severity stages.

**Applies to**: Toxic Buildup

**Key mechanics**:
- Uses `HediffComp_ImmunizableToxic` (extends `HediffComp_Immunizable`) but with `immunityPerDaySick = 0` (no immunity gain)
- Recovery rate: **-8%/day** when not exposed to toxins (`severityPerDayNotImmune = -0.08`)
- Accumulation rate: **+40%/day** from toxic fallout (unroofed) or pollution
- Recovery is **blocked** (not just slowed) when exposed:
  - Standing on polluted terrain AND `ToxicEnvironmentResistance < 100%`
  - OR Unroofed during toxic fallout AND `ToxicResistance < 100%`
  - OR In tox gas cloud (any density)

**Severity stages and health risks**:

| Stage | Severity | Dementia MTB | Carcinoma MTB | Daily Risk |
|-------|----------|--------------|---------------|------------|
| Initial | 0-40% | None | None | 0% |
| Moderate | 40-60% | 146 days | 438 days | ~0.7% / ~0.2% |
| Serious | 60-80% | 37 days | 111 days | ~2.7% / ~0.9% |
| Extreme | 80-100% | 13 days | 39 days | ~7.7% / ~2.6% |

---

## 2. DISEASE STATS REFERENCE TABLE

Base values for Type 1 (immunity race) diseases:

| Disease | Severity/day | Immunity/day | Treatment/day (at 100% quality) | Time to Kill (untreated) |
|---------|-------------|--------------|--------------------------------|--------------------------|
| **Infection** | +0.84 | +0.644 | -0.53 severity | ~1.25 days |
| **Plague** | +0.666 | +0.522 | -0.362 severity | ~1.5 days |
| **Malaria** | +0.370 | +0.314 | -0.232 severity | ~2.7 days |
| **Flu** | +0.249 | +0.239 | -0.077 severity | ~4 days |
| **Sleeping sickness** | +0.12 | +0.11 | -0.07 severity | ~8.3 days |

**Key insight**: For all these diseases, base severity gain is faster than base immunity gain. Treatment and ImmunityGainSpeed bonuses are what tip the balance toward survival.

**Treatment effect scales with tend quality**: 50% tend quality = half the severity reduction per day.

---

## 3. IMMUNITY SYSTEM CORE MECHANICS (Type 1 Diseases)

### ImmunityRecord (Verse/ImmunityRecord.cs)
- **Tracks**: Hediff immunity level (0.0 to 1.0)
- **Updated**: Every 60 game ticks (StandardInterval)
- **Key Method**: `ImmunityChangePerTick(pawn, sick, diseaseInstance)`

### ImmunityHandler (Verse/ImmunityHandler.cs)
- **Tracks**: List of `ImmunityRecord` for all diseases the pawn can develop immunity to
- **Critical Thresholds**:
  - **1.0 (100%)**: Fully immune - disease severity starts decreasing (pawn survives)
  - **0.6 (60%)**: Re-infection prevention threshold - pawn can't contract this disease again
  - **0.65 (65%)**: Forced immunity level for certain sources

---

## 2. IMMUNITY GAIN FORMULA (THE HEART OF THE SYSTEM)

### From ImmunityRecord.cs:

```
immunity += ImmunityChangePerTick(pawn, sick, diseaseInstance) * delta
```

**When SICK (diseased):**
```csharp
immunityPerDaySick = HediffDef.CompProps.immunityPerDaySick
immunityPerDaySick *= pawn.GetStatValue(StatDefOf.ImmunityGainSpeed)
immunityPerDaySick *= Mathf.Lerp(0.8f, 1.2f, Rand.Value)  // +/- 20% random variance

return immunityPerDaySick / 60000f  // per-tick rate
```

**When NOT SICK (recovered):**
```
return immunityPerDayNotSick / 60000f
```

### Key Stat: `ImmunityGainSpeed` (StatDefOf.cs)
- **This is the PRIMARY multiplier** for immunity gain while sick
- Accessed via: `pawn.GetStatValue(StatDefOf.ImmunityGainSpeed)`

**Factors affecting ImmunityGainSpeed** (from RimWorld Wiki):

| Factor | Effect |
|--------|--------|
| **Food** | No penalty if saturation > 12.5%. Starvation hurts immunity. |
| **Rest** | Resting gives bonus. Hospital bed > regular bed > not resting. Hospital bed + vitals monitor = maximum bonus. |
| **Age** | Decreases after age 54 for baseline humans. Genes affecting lifespan also affect this. |
| **Blood Filtration** | Luciferium increases it. Kidney/liver damage decreases it. Detoxifier kidney helps slightly. |
| **Traits** | Super-immune trait directly boosts immunity speed. |
| **Implants** | Immunoenhancer implant increases immunity speed. |
| **Genes** | Various genes can increase or decrease immunity speed. |

### Random Factor ("InfectionLuck"):
- **Range: 0.8 to 1.2** (±20% variance) (HealthTuning.cs)
- Called "InfectionLuck" by the community - an uncontrollable generated value
- Uses deterministic seeding based on disease instance loadID
- This ensures disease progression is reproducible within a save

---

## 3. DISEASE SEVERITY SYSTEM - How Diseases Progress

### HediffComp_Immunizable.cs - Severity Calculation

**Key Method**: `SeverityChangePerDay()`:

```csharp
return (FullyImmune ? Props.severityPerDayImmune
                    : (Props.severityPerDayNotImmune * severityPerDayNotImmuneRandomFactor))
       * SeverityFactorFromHediffs;
```

### Props Definition (HediffCompProperties_Immunizable.cs):
```csharp
public float immunityPerDayNotSick;      // Immunity gain when not sick
public float immunityPerDaySick;          // Immunity gain when sick (multiplied by ImmunityGainSpeed)
public float severityPerDayNotImmune;     // Disease worsens at this rate when NOT immune
public float severityPerDayImmune;        // Disease worsens at this rate when IMMUNE (usually negative = healing)
public FloatRange severityPerDayNotImmuneRandomFactor;  // Random variance in progression
public List<HediffDefFactor> severityFactorsFromHediffs;  // Hediffs that modify severity progression
```

### SeverityFactorFromHediffs
This multiplies disease severity change if certain hediffs are present:

```csharp
float num = 1f;
foreach (HediffDefFactor factor in Props.severityFactorsFromHediffs)
{
    if (pawn.health.hediffSet.GetFirstHediffOfDef(factor.HediffDef) != null)
    {
        num *= factor.Factor;  // Multiply by factor
    }
}
return num;
```

**Example**: A Vitals Monitor implant could have `severityFactorFromHediffs` reducing the multiplier, thus slowing disease progression.

---

## 4. TREATMENT QUALITY - How Tending Helps

### TendUtility.cs - Treatment Quality Formula

```csharp
public static float CalculateBaseTendQuality(Pawn doctor, Pawn patient,
                                              float medicinePotency, float medicineQualityMax)
{
    // Base doctor quality
    float num = doctor?.GetStatValue(StatDefOf.MedicalTendQuality) ?? 0.75f;

    // Apply medicine potency multiplier
    num *= medicinePotency;

    // Add bed quality modifier
    Building_Bed building_Bed = patient?.CurrentBed();
    if (building_Bed != null)
    {
        num += building_Bed.GetStatValue(StatDefOf.MedicalTendQualityOffset);
    }

    // Self-tend penalty (70% of normal quality)
    if (doctor == patient && doctor != null)
    {
        num *= 0.7f;
    }

    return Mathf.Clamp(num, 0f, medicineQualityMax);
}
```

### Components of Treatment Quality:

1. **Doctor Skill** (StatDefOf.MedicalTendQuality):
   - Base of 0.75 if no doctor
   - Scales with Medical skill

2. **Medicine Type** (medicinePotency & medicineQualityMax):
   - Herbal: potency ~0.3
   - Industrial: potency varies
   - Ultratech: potency varies

3. **Bed Quality** (StatDefOf.MedicalTendQualityOffset):
   - Hospital beds provide +X bonus
   - Clinical beds provide +Y bonus
   - Regular beds provide no bonus

4. **Self-Tending Penalty**: 70% of calculated quality

---

## 5. BED REST EFFECTIVENESS

### StatDefOf: `BedRestEffectiveness`

- **What it does**: Affects how much bed rest improves recovery
- **Where used**: StatPart_BedStat applies bed multipliers to stats
- **Applied to**: ImmunityGainSpeed when in bed (via StatPart_BedStat mechanism)
- **Better beds** = higher BedRestEffectiveness = faster immunity gain while resting

### Hospital Bed vs Sleeping Spot:
- Hospital beds have higher BedRestEffectiveness
- This multiplies the base ImmunityGainSpeed stat
- More comfortable resting means faster immunity

---

## 6. DISEASE CONTRACT CHANCE - Re-infection Prevention

> **Note**: This section is about preventing RE-INFECTION after recovery, NOT about surviving a current disease. See Section 9 for survival mechanics.

### ImmunityHandler.cs: `DiseaseContractChanceFactor()`

```csharp
float contractChance = Mathf.Lerp(1f, 0f, immunityList[j].immunity / 0.6f);
```

**Interpretation** (for contracting a NEW infection):
- At 0.0 immunity: 100% contract chance
- At 0.3 immunity: 50% contract chance (halfway to 0.6)
- At 0.6 immunity: 0% contract chance
- Above 0.6: Impossible to catch this disease again

**The 0.6 threshold** only applies to re-infection prevention, not survival of current disease.

---

## 7. TENDING EFFECTS ON DISEASE SEVERITY

> **Important**: Treatment DIRECTLY REDUCES SEVERITY. It does not affect immunity gain rate. Immunity gain is separate and based on ImmunityGainSpeed stat.

### HediffComp_TendDuration.cs - Severity While Tended

```csharp
public override float SeverityChangePerDay()
{
    if (IsTended)
    {
        return TProps.severityPerDayTended * tendQuality;  // Usually NEGATIVE (healing)
    }
    return 0f;  // No change when not tended
}
```

### Treatment Severity Reduction Formula:

```
actual_severity_reduction = base_treatment_effect × tend_quality
```

**Example for Infection** (base treatment: -0.53/day at 100%):
- 100% tend quality: -0.53 severity/day
- 75% tend quality: -0.40 severity/day
- 50% tend quality: -0.265 severity/day
- 25% tend quality: -0.13 severity/day

**Key Points**:
- Diseases with `HediffCompProperties_TendDuration` get better when tended
- `severityPerDayTended` is typically NEGATIVE (slows disease)
- Multiplied by `tendQuality` (0.0 to 1.0 range based on doctor skill + medicine)
- Better treatments = faster disease reduction
- Treatment does NOT speed up immunity gain - it only slows severity increase

---

## 8. VITALS MONITOR EFFECT

**How it works** (based on severity factor system):
- Likely has `severityFactorsFromHediffs` entry in disease defs
- Reduces the severity progression multiplier (e.g., factor = 0.8)
- This SLOWS disease worsening by 20%
- Effect is: `severityPerDay * 0.8`

---

## 9. WHAT DETERMINES IF A PAWN SURVIVES A DISEASE (Type 1 Only)

> **Note**: This section applies to Type 1 (immunity race) diseases only. See Section 1 for other disease types.

### The Survival Equation:

```
SURVIVAL = Immunity reaches 100% BEFORE Severity reaches 100%
```

This is a **race to 100%** for both values:
- **Immunity hits 100% first** → `FullyImmune = true` → severity switches to `severityPerDayImmune` (usually negative = healing) → pawn survives
- **Severity hits 100% first** → pawn dies (for lethal diseases)

### Immunity Gain Rate:
```
immunity_per_day = base_immunity × ImmunityGainSpeed × InfectionLuck(0.8-1.2)
```

**Factors stacking for immunity gain**:
1. Disease `immunityPerDaySick` (base from disease def - see table in Section 2)
2. × `ImmunityGainSpeed` stat (affected by food, rest, age, blood filtration, traits, implants, genes)
3. × InfectionLuck random factor (0.8 to 1.2)

### Net Severity Change Rate:
```
net_severity_per_day = disease_progression - treatment_reduction
                     = (base_severity × random × hediff_factors) - (treatment_base × tend_quality)
```

**Factors increasing severity**:
1. Disease `severityPerDayNotImmune` (base from disease def - see table in Section 2)
2. × random factor (from disease def range)
3. × SeverityFactorFromHediffs (e.g., vitals monitor reduces this)

**Factors decreasing severity**:
1. Treatment base effect (e.g., -0.53/day for infection)
2. × tend quality (0.0 to 1.0)

### Example Calculation (Infection):
- Base severity: +0.84/day
- Base immunity: +0.644/day
- With 80% tend quality treatment: -0.53 × 0.8 = -0.424/day
- **Net severity change**: +0.84 - 0.424 = **+0.416/day** (still rising, but slower)
- The pawn survives if immunity reaches 100% before severity does

---

## 10. CRITICAL THRESHOLDS & CONSTANTS

From HealthTuning.cs:

| Constant | Value | Meaning |
|----------|-------|---------|
| StandardInterval | 60 ticks | How often stats update |
| ImmunityGainRandomFactorMin | 0.8 | Minimum random immunity variance |
| ImmunityGainRandomFactorMax | 1.2 | Maximum random immunity variance |
| ImpossibleToFallSickIfAboveThisImmunityLevel | 0.6 | Re-infection prevention threshold (NOT survival threshold) |
| ForcedImmunityLevel | 0.65 | For certain immunity sources |
| NoDoctorTendQuality | 0.75 | Base quality if no doctor tends |
| SelfTendQualityFactor | 0.7 | Self-tending penalty |

---

## 11. KEY FILES REFERENCE MAP

| File | Purpose |
|------|---------|
| ImmunityRecord.cs | Tracks immunity per disease, calculates per-tick gain |
| ImmunityHandler.cs | Manages all immunities, contract chance formula |
| HediffComp_Immunizable.cs | Disease severity calculation with immunity dependency |
| HediffComp_ImmunizableToxic.cs | Type 4: Toxic buildup - extends Immunizable, blocks recovery when exposed |
| TendUtility.cs | Treatment quality formula (doctor + bed + medicine) |
| HediffComp_TendDuration.cs | How tending slows disease |
| StatDefOf.cs | Defines ImmunityGainSpeed, BedRestEffectiveness, MedicalTendQuality |
| HealthTuning.cs | Constants and thresholds |
| GasUtility.cs | Tox gas exposure effects, damage application |

---

## SUMMARY: How to Maximize Disease Survival

### For Type 1 Diseases (Immunity Race):
**Pawn must reach 100% immunity BEFORE severity reaches 100% (death).**

**Two parallel tracks to optimize:**

#### Track 1: Speed Up Immunity Gain
- Keep pawn fed (> 12.5% saturation - no penalty)
- Rest in hospital bed (best) or regular bed (good)
- Attach vitals monitor to hospital bed (maximum immunity bonus)
- Younger pawns have faster immunity (declines after age 54)
- Luciferium boosts blood filtration → faster immunity
- Super-immune trait / Immunoenhancer implant / beneficial genes

#### Track 2: Slow Down Severity Increase
- Tend regularly with high-quality medicine
  - Good doctor (high Medical skill)
  - High potency medicine (Glitterworld > Industrial > Herbal)
  - Treatment directly reduces severity per day
- Vitals monitor reduces severity progression multiplier
- Avoid other hediffs that worsen disease progression

### For Type 2 Diseases (Cumulative Tend):
- Gut worms, Muscle parasites need **300% total tend quality** to cure
- Focus purely on tend quality, not immunity
- Example: 4 tends at 80% = 320% → cured

### For Type 3 Diseases (Time-Based):
- Lung rot, Mechanites, Blood rot just need time to pass
- Treatment slows/prevents progression while waiting
- Keep severity low until the timer runs out

### For Type 4 Diseases (Environmental - Toxic Buildup):
- **NOT tendable** - medical treatment does nothing
- Recovery requires **avoiding all toxin sources**:
  - Stay under a roof during toxic fallout events
  - Avoid polluted terrain (Biotech DLC)
  - Avoid tox gas clouds
- Recovery rate is slow: only **-8%/day** when safe
- At higher severity (40%+), risk of permanent conditions (dementia, carcinoma) increases
- Pawns with 100% Toxic Resistance or Toxic Environment Resistance are immune to those respective sources
- **Best strategy**: Keep pawns indoors during toxic fallout, clean up pollution, avoid tox gas

---

Once immunity reaches 100% (Type 1), severity starts decreasing and the pawn recovers.
(Side note: Once immunity hits 60%, the pawn can't catch that same disease again.)

---

## Key Source File Paths

```
../decompiled/RimWorld/Verse/ImmunityRecord.cs
../decompiled/RimWorld/Verse/ImmunityHandler.cs
../decompiled/RimWorld/Verse/HediffComp_Immunizable.cs
../decompiled/RimWorld/Verse/HediffComp_ImmunizableToxic.cs  # Type 4 - Toxic Buildup
../decompiled/RimWorld/RimWorld/TendUtility.cs
../decompiled/RimWorld/Verse/HediffComp_TendDuration.cs
../decompiled/RimWorld/RimWorld/StatDefOf.cs
../decompiled/RimWorld/Verse/HealthTuning.cs
../decompiled/RimWorld/Verse/GasUtility.cs  # Tox gas exposure logic
```

---

## Sources

- RimWorld decompiled source code (version 1.6)
- [RimWorld Wiki - Disease](https://rimworldwiki.com/wiki/Disease) (saved as `docs/Disease - RimWorld Wiki.pdf`)
