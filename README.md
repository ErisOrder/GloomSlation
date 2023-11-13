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

## Creating new translation
As a starting point, copy directory of language, from which you want to translate.

### Text
Inside folder you can find text files such as `Menus`, `Journal`, etc.
These files have simple format that looks like
```c
/*
  Block comment
*/
// Line comment
KEY = "line\nnext line"; 
```
keys should be left unchanged and each key-value entry should be terminated by `;`.
You can use some escape sequences to insert a newline or tab, like `\n`, `\r` (though `\n` itself is enough) or `\t`. 

At the moment the game has some half-baked localization way, and that way is used where possible.
However, there are still a lot of objects that do not have localization component.
Keys for such object are generated from their name and text.
If you have `debug` prefence enabled, there will be log entries about unlocalized objects,
containing their generated key and text (or you can just view other translation).
For now all such generated keys are stored in `Menus` file.

### Fonts
You can use `Unity Editor` of same version as game's Unity Engine (`2021.2.13f1` as for game version `0.1.230.05`)
to create font assets. Look at [TextMeshPro docs](https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.2/manual/index.html) to learn more.

Font assets should be packed into single `font.bundle` `assetbundle` file. You can achieve this by using so-called
[`Addressables`](https://docs.unity3d.com/Packages/com.unity.addressables@2.0/manual/get-started-make-addressable.html).

There is `fontMap.json` file that is used to determine which original font to replace with which new font.
It's a simple json map, where keys are original font names, and values are new font names.
It may also contain special `FALLBACK_FONT` key, which is used when there are no suitable new fonts. 

### Textures
A language directory may also contain `Textures` folder. 
Images in `png` format placed in this directory will be loaded into memory and will replace any 
in-game texture, which name is equal to image name (excluding `.png` extension).
You could extract textures using [AssetStudio](https://github.com/Perfare/AssetStudio). The extracted image
will have appropriate name already.


## Notes

### About using `UnityExplorer`
For some reason `UnityExplorer`'s library, `UniverseLib`, cannot be loaded from `UserLibs`
and needs to be placed in `Gloomwood_Data/Managed` folder


## Credits
- `pipo-cxx` for initiating project and making russian translation.
