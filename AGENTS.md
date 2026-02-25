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

TBD - Document main classes and their responsibilities here.

### Source Files

```
Source/RecoveryProcessTracker/
├── RecoveryProcessTracker.csproj   # Build configuration
├── RecoveryProcessTrackerMod.cs    # Main mod class & settings
└── Patches/                        # (if needed) Harmony patches
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
