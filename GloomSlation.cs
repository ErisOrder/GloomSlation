using Gloomwood.Languages;
using HarmonyLib;
using MelonLoader;
using MelonLoader.TinyJSON;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;


[assembly: MelonInfo(typeof(GloomSlation.GloomSlation), "GloomSlation", "0.1.0", "pipo, nikvoid")]
[assembly: MelonGame("Dillon Rogers", "Gloomwood")]

namespace GloomSlation
{
    public class GloomSlation : MelonMod
    {
        private readonly Dictionary<string, TMP_FontAsset> fontMap = new Dictionary<string, TMP_FontAsset>();
        private readonly HashSet<string> ourFonts = new HashSet<string>();
       
        private static readonly string modPath = "Mods\\GloomSlation";

        private string langPath = "";

        public override void OnInitializeMelon()
        {
            // Initialize preferences
            var prefCategory = MelonPreferences.CreateCategory("GloomSlation");
            prefCategory.SetFilePath(Path.Combine(modPath, "cfg.toml"));
            var languageEntry = prefCategory.CreateEntry<string>("language", "Russian");

            langPath = Path.Combine(modPath, languageEntry.Value);

            // Read font map
            var text = File.ReadAllText(Path.Combine(langPath, "fontMap.json"));
            if (!(JSON.Load(text) is ProxyObject fMap))
            {
                LoggerInstance.Msg("failed to load fontMap.json");
                return;
            }

            // Load asset bundle
            var bundle = AssetBundle.LoadFromFile(Path.Combine(langPath, "font.bundle"));
            LoggerInstance.Msg("Loaded asset bundle, available assets:");
            foreach (var name in bundle.GetAllAssetNames())
            {
                LoggerInstance.Msg(name);
            }

            // Load fonts
            LoggerInstance.Msg("Font mapping:");
            foreach (var (k, v) in fMap)
            {
                var asset = (TMP_FontAsset)bundle.LoadAsset($"Assets/{v}.asset");
                if (asset == null) {
                    LoggerInstance.Msg($"Asset not found: {k}");
                } else {
                    ourFonts.Add(v);
                    fontMap.Add(k, asset);
                    LoggerInstance.Msg($"{k} -> {v}");
                }
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            var scene = SceneManager.GetSceneByBuildIndex(buildIndex);

            foreach (var obj in scene.GetRootGameObjects())
            {
                PatchGameObjectFonts(obj);
            }
        }

        public void PatchGameObjectFonts(GameObject obj)
        {
            // Recursively patch all fonts found
            foreach (var tmp in obj.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
            {
                // Do not remap twice
                if (tmp.font != null && !ourFonts.Contains(tmp.font.name))
                {
                    TMP_FontAsset font;
                    if (fontMap.TryGetValue(tmp.font.name, out font))
                    {
                        tmp.font = font;
                    }
                    else if (fontMap.TryGetValue("FALLBACK_FONT", out font))
                    {
                        LoggerInstance.Msg($"mapping {tmp.font.name} -> FALLBACK");
                        tmp.font = font;
                    }
                }
            }
        }

        public Dictionary<string, string> ReadLanguageData(LanguageDataTypes dataType)
        {
            var entries = new Dictionary<string, string>();

            // Load localization
            var path = Path.Combine(langPath, dataType.ToString());
            var text = File.ReadAllText(path);
            LanguageParser.Parse(text, entries);

            LoggerInstance.Msg($"Loaded translation ({dataType})");

            return entries;
        }
    }

    [HarmonyPatch(typeof(GameObject), "SetActive")]
    static class PatchGameObject
    {
        static void Postfix(ref GameObject __instance, ref bool value)
        {
            if (!value)
            {
                return;
            }

            Melon<GloomSlation>.Instance.PatchGameObjectFonts(__instance);
        }
    }

    [HarmonyPatch(typeof(LanguageManager), "BuildDataType")]
    static class PatchLanguageManager
    {
        static void Postfix(
            ref Dictionary<string, string> __result,
            ref LanguageDataTypes dataType
        )
        {
            __result = Melon<GloomSlation>.Instance.ReadLanguageData(dataType);
        }
    }
}

