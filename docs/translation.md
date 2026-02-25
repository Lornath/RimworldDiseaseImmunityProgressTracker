# Localization

[Jump to navigation](https://rimworldwiki.com/wiki/Modding_Tutorials/Localization#mw-head)  
[Jump to search](https://rimworldwiki.com/wiki/Modding_Tutorials/Localization#searchInput)

[Localization](https://en.wikipedia.org/wiki/Internationalization_and_localization) refers to the process of translating game text for different languages as well as presenting information in a format that is more natural to different countries and regions.

## Language Files

Language files should be placed into the Languages folder of your mod like so:

```
Mods  
└ MyModFolder  
  └ Languages  
    ├ English  
    │ ├ DefInjected  
    │ ├ Keyed  
    │ └ Strings  
    ├ French (Français)  
    ├ German (Deutsch)  
    └ etc.
```

Please see the [Mod Folder Structure](https://rimworldwiki.com/wiki/Modding_Tutorials/Mod_Folder_Structure) guide for more information about mod folders.

### **DefInjected**

DefInjected XML files are used to translate fields in XML Defs.

```
Mods  
└ MyModFolder  
  └ Languages  
    └ English  
      └ DefInjected  
        ├ ThingCategoryDef  
        ├ ThingDef  
        │ └ AnyFileName.xml  
        └ etc.
```

The folders inside of the DefInjected folder should match the Def type of the Def you are trying to translate. Thus, translations for ThingDefs must be inside the ThingDef folder.

DefInjected XML files have LanguageData as their root tag, and individual nodes within them should match the XML structure of the tags you are trying to translate. For example, the below file is the French translation file Alcohol\_Beer.xml. To target the label tag of the Beer ThingDef, the tag name should be Beer.label.

```xml
<?xml version="1.0" encoding="UTF-8"?>  
<LanguageData>  
    
  <!-- EN: beer -->  
  <Beer.label>bière</Beer.label>  
  <!-- EN: The first beverage besides water ever consumed by mankind. Beer can taste good, but its main effect is intoxication. Excessive consumption can lead to alcohol blackouts and, over time, addiction. -->  
  <Beer.description>Mise à part l'eau, la première boisson consommée par l'humanité. La bière est bonne, mais peut être toxique. Une consommation excessive peut rendre dépendant.</Beer.description>  
  <!-- EN: Drink {0} -->  
  <Beer.ingestible.ingestCommandString>Boire {0}</Beer.ingestible.ingestCommandString>  
  <!-- EN: Drinking {0}. -->  
  <Beer.ingestible.ingestReportString>Boit {0}.</Beer.ingestible.ingestReportString>  
  <!-- EN: bottle -->  
  <Beer.tools.bottle.label>bouteille</Beer.tools.bottle.label>  
  <!-- EN: neck -->  
  <Beer.tools.neck.label>goulot</Beer.tools.neck.label>  
    
  <!-- EN: wort -->  
  <Wort.label>moût</Wort.label>  
  <!-- EN: Un-fermented beer. This substance needs to ferment in a fermenting barrel before it becomes drinkable beer. -->  
  <Wort.description>Bière non fermentée. Elle doit fermenter dans un baril avant de pouvoir être bue.</Wort.description>  
    
</LanguageData>
```

### **Keyed**

Keyed XML files are used to define strings for use in C\# code, usually for custom UI. Unlike DefInjected files, these files must be created even for your default language. Using Keyed files is strongly recommended for strings used in custom assemblies, even if you do not plan on doing any translations yourself; if you do not use them then no one else can translate your mod either.

```
Mods  
└ MyModFolder  
  └ Languages  
    └ English  
      └ Keyed  
        └ AnyFileName.xml
```

Unlike DefInjected files, subfolder and file names for Keyed files do not matter except for your own personal organization. However, similar to Def names, the actual string names themselves should be prefixed as all translation keys from official content as well as mods are combined in memory.

The following is a snippet from the vanilla Alerts.xml file:

```xml
<?xml version="1.0" encoding="utf-8" ?>  
<LanguageData>

  <ClickToJumpToProblem>Click to jump to problem</ClickToJumpToProblem>

  <BreakRiskMinor>Minor break risk</BreakRiskMinor>  
  <BreakRiskMajor>Major break risk</BreakRiskMajor>  
  <BreakRiskExtreme>Extreme break risk</BreakRiskExtreme>  
  <BreakRiskMinorDesc>These colonists are in a poor mood and may have a minor mental break at any time:nn{0}</BreakRiskMinorDesc>  
  <BreakRiskMajorDesc>These colonists are in a very poor mood and may have a major mental break at any time:nn{0}</BreakRiskMajorDesc>  
  <BreakRiskExtremeDesc>These colonists are critically stressed and may have an extreme (and possibly violent) mental break at any moment:nn{0}</BreakRiskExtremeDesc>  
  <BreakRiskDescEnding>Check these colonists' Needs tab to see what's bothering them.nnTo make colonists feel better, you can do things like make them fancy meals, give them more recreation hours per day and nicer recreation objects, place them in beautiful environments, have them drink and smoke, and much more.</BreakRiskDescEnding>

  *<!-- many more keys omitted -->*  
</LanguageData>
```

The following is a snippet from a mod's Keyed files, showing string keys with a prefix for uniqueness:

```xml
<?xml version="1.0" encoding="utf-8" ?>  
<LanguageData>

  <ARR_UsePotionOn>Use {0} on...</ARR_UsePotionOn>  
  <ARR_DetoxifyAmount>{0}%</ARR_DetoxifyAmount>

  <ARR_ToxicShock>Alchemical Shock</ARR_ToxicShock>  
  <ARR_ToxicShockText>{PAWN_nameDef} has gone into alchemical shock from ingesting too many potions! This will render them unconscious and can last up to half a day.</ARR_ToxicShockText>

</LanguageData>
```

#### **Using Keyed Strings in C\#**

Keyed strings can be used from C\# by using the Verse.Translator.Translate() extension method like so:

```C#
string translatedString = "ClickToJumpToProblem".Translate();
```

If the specified key cannot be found, then the key itself will be shown. If Dev Mode is enabled, this will come out as glitched text to make them easier to notice and fix.

#### **String Arguments**

String arguments can be used to inject values into key strings. The simplest way of using arguments is by using indexed arguments like so:

```xml
<?xml version="1.0" encoding="utf-8" ?>  
<LanguageData>  
  <Example_KeyString>Current Class: Level {0} {1} ({2}%)</Example_KeyString>  
</LanguageData>
```

Strings to be injected into those indexed tokens can be added using arguments to the Translate() method:

```C#
int level = 4;  
string className = "Rogue";  
float levelProgress = 96f;  
string translatedString = "Example_KeyString".Translate(level.ToString("N0"), className, levelProgress.ToString("F1"));
```

The value of translatedString in this case would be Current Class: Level 4 Rogue (96.0%)

(Placeholder) You can also use named arguments in keyed strings. Please check out [GrammarResolver](https://rimworldwiki.com/wiki/Modding_Tutorials/GrammarResolver) for the time being. (Temporary link until cleanup can happen)

### **Strings**

(Placeholder) Strings files are used to create word lists for RulePackDefs to be used in name and sentence generation.

