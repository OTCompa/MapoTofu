# Mapo Tofu
Mapo Tofu is a Dalamud plugin for FINAL FANTASY XIV that automatically opens a set strategy board depending on certain conditions.  
Never forget to open a strategy board again!
## Main Points
- Automatically open a strategy board based on territory
- Automatically swap to a specific strategy board based on encounter timer/phases (weather)

## In Progress/Planned
- Add way to import/export triggers
- Traverse strategy boards in the same folder without going back to the list
- Maybe import native strategy boards along side triggers?
- Load in saved strategy boards in the preview window without going back to the list?

## How To Use
### Getting Started
- Type `/xlsettings` in the chatbox or open up Dalamud's settings menu
- Open the "Experimental" tab and scroll down to the "Custom Plugin Repositories" section

`https://raw.githubusercontent.com/OTCompa/frey-s-dalamud-plugins/refs/heads/main/plogon.json`
- Paste the link above into the bottom-most textbox of the section and click the "+" button to the right
- Click on the save button on the bottom right corner of the window
- Type `/xlplugins` in the chatbox or open up Dalamud's plugin installer menu
- Search for `Mapo Tofu` and install.

Once installed, you can open the config menu through the plugin installer to select one of the available voicepacks. 

### Building
1. Open up `MapoTofu.sln` in your C# editor of choice (likely [Visual Studio 2022](https://visualstudio.microsoft.com) or [JetBrains Rider](https://www.jetbrains.com/rider/)).
2. Build the solution. By default, this will build a `Debug` build, but you can switch to `Release` in your IDE.
3. The resulting plugin can be found at `MapoTofu/bin/x64/Debug/MapoTofu.dll` (or `Release` if appropriate.)

## Credits
Voiced Nael Quotes heavily uses code from different projects and would not be possible if these did not exist.
Huge thanks to:
- Wintermute for the boardobject struct
- [PortraitFixer](https://github.com/Aida-Enna/XIVPlugins/tree/main/PortaitFixer) and further original sources for UI automation.
