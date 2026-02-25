# Disease Immunity Progress Tracker

A [RimWorld](https://store.steampowered.com/app/294100/RimWorld/) mod that visualizes disease and infection progression with predictive timeline graphs, so you can make informed decisions about your pawns' medical care before it's too late.

**[Subscribe on Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3659005144)**

## What It Does

When you hover over a disease or infection in a pawn's Health tab, a companion window appears showing:

- **Immunizable diseases** (Plague, Flu, Malaria, etc.) — A graph of immunity vs. severity with projected trend lines and a survival verdict.
- **Cumulative tend diseases** (Gut Worms, Muscle Parasites) — Progress toward the tend quality threshold, recent tends, and estimated time to cure.
- **Time-based diseases** (Mechanites, Lung Rot, Blood Rot) — Countdown to cure with severity graph and pain/danger verdict.
- **Toxic Buildup** — Current exposure sources, recovery estimate, and dementia/carcinoma risk at higher severity.
- **Chronic diseases** (Asthma) — Treatment quality vs. regression threshold, severity trend.
- **Artery Blockage** — Progression rate, heart attack risk by stage, and cure options reminder.
- **Food Poisoning** — Staged countdown bar showing Initial / Major / Recovering phases and symptoms.

## Requirements

- RimWorld 1.6
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)

---

## Developer Setup

### 1. Configure paths

Two scripts contain a hardcoded Steam path (`$steamBase`) that you need to update for your system. Open each file and change the path at the top:

- **`Libs\CopyLibs.ps1`** — set `$steamBase` to your Steam install directory
- **`deploy.ps1`** — set `$destDir` to your RimWorld Mods directory

These are the only changes needed, and ideally you won't commit them (add a local `.gitignore` entry or just be careful when staging).

### 2. Populate Libs/

Run the copy script once after cloning to pull the required DLLs from your RimWorld installation and the Harmony workshop mod:

```powershell
.\Libs\CopyLibs.ps1
```

This copies `Assembly-CSharp.dll`, the required Unity modules, and `0Harmony.dll` into `Libs/`. These are not committed to the repo.

### 3. Build

```powershell
dotnet build "Source/DiseaseImmunityProgressTracker/DiseaseImmunityProgressTracker.csproj"
```

Expected output: `Build succeeded. 0 Warning(s) 0 Error(s)`

This produces `Assemblies/DiseaseImmunityProgressTracker.dll`.

### 4. Deploy

```powershell
.\deploy.ps1
```

Copies the mod files to your RimWorld Mods directory so you can test in-game.

---

## Contributing

Pull requests are welcome!

### Translations

The mod uses RimWorld's Keyed XML translation system. All language files live in `Languages/`.

- **Improve existing translations** — machine-translated strings could use human review in any language.
- **Add a new language** — copy the `Languages/English/` folder structure, translate the values, and submit a PR.

---

## License

MIT — see [LICENSE](LICENSE).

## Credits

Designed by a human, code implementation by our new AI best friends Claude and Gemini
