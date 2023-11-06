# GloomStation

## Building
Before running build, you should install [MelonLoader](https://github.com/LavaGang/MelonLoader)
on your Gloomwood and then create symbolic link to Gloomwood root folder and name it `Gloomwood`.
This can be done using powershell with admin privileges (in this project's root):
```
New-Item -Path .\Gloomwood -ItemType SymbolicLink -Value <path-to-your-Gloomwood>
```

## Notes

### About using `UnityExplorer`
For some reason `UnityExplorer`'s library, `UniverseLib`, cannot be loaded from `UserLibs`
and needs to be placed in `Gloomwood_Data/Managed` folder
