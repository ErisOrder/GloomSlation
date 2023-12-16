# How to create new translation

As a starting point, copy directory of language, from which you want to translate.

## Text
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

Please note that there are few dynamic text labels. Their change is handled and will be logged slightly differently.
Mod determines which labels to translate by their initial text, 
so be sure to find and provide translation for initial values of such labels. 

## Fonts
Most fonts in game only support ASCII english characters and symbols, so you probably ought to make new fonts.
This is the most complicated (if you don't count texture edit) step.

You can use `Unity Editor` of same version as game's Unity Engine (`2021.2.13f1` as for game version `0.1.230.05`)
to create font assets. 
Look at [TextMeshPro docs](https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.2/manual/index.html) 
to learn about font settings.

For easy start, clone this unity project [repository](https://github.com/Unity-Technologies/Addressables-Sample).

Open asset folder `Basic` and add `.ttf` or `.otf` fonts to `Basic\Basic AssetReference\Assets`.
![Added fonts](./img/raw-fonts.png)

Make SDF Font asset for each font you have added. 
Open Font Asset Creator.
![Open creator](./img/open-font-creator.png)

Select target raw font and choose appropriate atlas size 
(you can make it smaller if you see a lot of free space in generated texture).
Input characters or character ranges you need. You will be able to use **only** these characters, 
so don't forget to add ASCII range `20-7E`.
Look [here](https://www.ling.upenn.edu/courses/Spring_2003/ling538/UnicodeRanges.html) or 
[here](https://jrgraphix.net/r/Unicode/) to pick appropriate ranges.
Click `Generate Font Atlas` and wait for generation.
![Create font](./img/atlas-gen.png)

For each SDF Font asset you have generated, check the `Addressable` checkbox. 
You need to do this to pack them into `bundle`.

After that click on `Select` button in any of font assets. 
![Checkbox](./img/addressable-ckbox.png)

Select `Manage profiles` entry in opened window.
![Profiles](./img/addressable-profiles.png)

Edit build output paths to point to any location that you like. 
![Paths](./img/addressable-path.png)

Now build the bundle. **You need to do this and next step again each time you change or add a font asset.**
![Build](./img/addressable-build.png)

Go to chosen location and rename build artifact into `font.bundle`. Now you can move it to language directory.
![Build output](./img/build-output.png)

You can also refer to Unity docs on
[`Addressables`](https://docs.unity3d.com/Packages/com.unity.addressables@2.0/manual/get-started-make-addressable.html).

There is `fontMap.json` file that is used to determine which original font to replace with which new font.
It's a simple json map, where keys are original font names, and values are new font names.
It may also contain special `FALLBACK_FONT` key, which is used when there are no suitable new fonts. 

## Textures
A language directory may also contain `Textures` folder. 
Images in `png` format placed in this directory will be loaded into memory and will replace any 
in-game texture, which name is equal to image name (excluding `.png` extension).
You could extract textures using [AssetStudio](https://github.com/Perfare/AssetStudio). The extracted image
will have appropriate name already.


