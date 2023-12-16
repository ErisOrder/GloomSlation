# GloomStation
An unofficial Gloomwood translation project.

## Languages
This mod currently contains translation on:
- English - original text, to be used like reference and starting point
- Russian - by [@pipo-cxx](https://github.com/pipo-cxx)

## Preferences
Mod preferences are stored in file `Mods/GloomSlation/cfg.toml`. 
File is created on first launch (and exit).
It contains entries, such as
- `language` - is a name of currently chosen language directory
- `debug` - a boolean which, when `true`, enables extended logging

## Building
Before running build, you should install [MelonLoader](https://github.com/LavaGang/MelonLoader)
on your Gloomwood and then create symbolic link to Gloomwood root folder and name it `Gloomwood`.
This can be done using powershell with admin privileges (in this project's root):
```powershell
New-Item -Path .\Gloomwood -ItemType SymbolicLink -Value <path-to-your-Gloomwood>
```

## [Creating new translation](./docs/adding-new-translation.md)
## Notes

### About using `UnityExplorer`
For some reason `UnityExplorer`'s library, `UniverseLib`, cannot be loaded from `UserLibs`
and needs to be placed in `Gloomwood_Data/Managed` folder


## Credits
- `pipo-cxx` for initiating project and making russian translation.
