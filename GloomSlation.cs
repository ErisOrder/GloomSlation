using Gloomwood.Languages;
using Gloomwood.UI;
using HarmonyLib;
using MelonLoader;
using MelonLoader.TinyJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;
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

        private static readonly Regex notIdentRex = new Regex("[^0-9a-zA-Z_]+");

        private string langPath = "";

        public override void OnInitializeMelon()
        {
            // We want full stacktraces to be printed in case of exception
            Application.logMessageReceived += (text, trace, type) => {
                if (type == LogType.Exception) {
                    MelonLogger.Error($"[Unity {type}] {text}\n{trace}");
                }
            };
            
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

        /// Patch all scene's GameObjects
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            var scene = SceneManager.GetSceneByBuildIndex(buildIndex);

            foreach (var obj in scene.GetRootGameObjects())
            {
                PatchGameObject(obj);
            }
        }

        /// Construct localization entry ID from GameObject name and original text
        public static string ConstructLocaleKey(string objName, string origText) {
            return notIdentRex.Replace($"{objName}@{origText}", "_");
        }

        /// Patch single GameObject
        public void PatchGameObject(GameObject obj)
        {
            // Recursively patch all texts found
            foreach (var tmp in obj.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
            {
                // Remap fonts, do not remap twice
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

                var tmpObj = tmp.gameObject;
                var component = tmpObj.GetComponent<TextBehaviour>();
                
                // For every object that doesn't have localization component or key, 
                // add one and generate key
                if (
                    (component != null && component.LocalizationId.Length == 0 || component == null) 
                    && tmp.text != null && tmp.text.Length > 0
                ) {
                    var localeKey = ConstructLocaleKey(tmp.gameObject.name, tmp.text);

                    // Create new if necessary
                    if (component == null) {
                        component = tmpObj.AddComponent<TextBehaviour>();
                    }

                    // This private fields are set in serialized asset and inacessible without reflection
                    typeof(TextBehaviour)
                        .GetField("localizedID", BindingFlags.NonPublic | BindingFlags.Instance)
                        .SetValue(component, localeKey);
                    typeof(TextBehaviour)
                        .GetField("startLocalizedID", BindingFlags.NonPublic | BindingFlags.Instance)
                        .SetValue(component, localeKey);
                    typeof(TextBehaviour)
                        .GetField("localizedFormatId", BindingFlags.NonPublic | BindingFlags.Instance)
                        .SetValue(component, localeKey);
                        
                    // Most of such texts are related to menu, so let's store there all of them
                    typeof(TextBehaviour)
                        .GetField("localizationType", BindingFlags.NonPublic | BindingFlags.Instance)
                        .SetValue(component, LanguageDataTypes.Menus);
                        
                    // TODO: Make some kind of "debug mode" to only enable such helper logs in it
                    MelonLogger.Msg($"Found unlocalized text: {localeKey} = \"{tmp.text}\";");                    
                }
                
                // Force-enable text behavior
                if (component != null) {
                    component.enabled = true;
                }
            }
        }

        /// Read language data of selected language and LanguageDataTypes
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

            Melon<GloomSlation>.Instance.PatchGameObject(__instance);
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
    
    [HarmonyPatch(typeof(TextBehaviour), "SetString", new Type[] { typeof(string), typeof(bool), typeof(bool) })]
    static class PatchTextBehaviour {
        static void Prefix(
            // ref bool localize,
            ref bool lowercase
        ) {
            // Text in some places is forcefully set to lowercase.
            // Undo that
            lowercase = false;
        }
    }
}

