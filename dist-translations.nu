dotnet build --configuration Release
rm -rf dist
mkdir bin/temp

ls Mods/GloomSlation | where type == "dir" | each { |it| 
  let lang = $it.name | path basename 
  # Prepare config file
  $"[GloomSlation]\nlanguage = \"($lang)\"\ndebug = false\nhideConsole = true" | save bin/temp/cfg.toml -f
  # Find built dll
  let dll = (ls bin/Release/*/GloomSlation.dll | first).name
  let out = $"dist/GloomSlation-($lang)"
  # Pack dist archive
  7z a -tzip $out $it.name -xr!.git* $"-i!($dll)" -i!bin/temp/cfg.toml
  # Fix paths
  7z rn $"($out).zip" $dll Mods/GloomSlation.dll
  7z rn $"($out).zip" bin/temp/cfg.toml Mods/GloomSlation/cfg.toml
}
