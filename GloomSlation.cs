﻿using Gloomwood.Languages;
using Gloomwood.UI;
using Gloomwood;
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
    public static class Extensions {
        public static T ReadPrivateField<T>(this object instance, string name) {
            return (T)instance
                .GetType()
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(instance);
        }

        public static bool NullOrEmpty(this string inst) {
            return inst == null || inst == string.Empty;
        }

    }

    public class LocalizeMarker: MonoBehaviour {}

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
                var localizedText = tmpObj.GetComponent<LocalizedText>();
                
                // For every object that doesn't have localization component or key, 
                // generate key and try to get localization
                if (tmpObj.GetComponent<LocalizeMarker>() == null
                    && !tmp.text.NullOrEmpty()
                    && (component == null || localizedText == null || !localizedText.enabled)
                ) {
                    var localeKey = ConstructLocaleKey(tmpObj.name, tmp.text);
                        
                    // TODO: Make some kind of "debug mode" to only enable such helper logs in it
                    MelonLogger.Msg($"Found unlocalized text: {localeKey} = \"{tmp.text}\";");                    
                    
                    // Most of such texts are related to menu, so let's store there all of them
                    if(GameManager.LanguageManager.TryGetLocalizedContent(
                        LanguageDataTypes.Menus,
                        localeKey,
                        out string localized
                    )) {
                        MelonLogger.Msg("Found localization!");
                        tmp.text = localized;

                        tmpObj.AddComponent<LocalizeMarker>();

                        // This prevents anything from changing our text back
                        tmp.OnPreRenderText += (ti) => {
                            // Prerender triggered by our action
                            if (ti.textComponent.text == localized) {
                                return;
                            }    
                        
                            var newLocaleKey = ConstructLocaleKey(tmpObj.name, ti.textComponent.text);
                            MelonLogger.Msg($"{localeKey} -> {newLocaleKey}: \"{ti.textComponent.text}\"");
                            if(GameManager.LanguageManager.TryGetLocalizedContent(
                                LanguageDataTypes.Menus,
                                newLocaleKey,
                                out string localizedNew
                            )) {
                                // Update text in case there's something localized
                                ti.textComponent.text = localizedNew;                               
                                localized = localizedNew;
                                ti.textComponent.ForceMeshUpdate(true, true);
                            } else {
                                // Write previous value back
                                ti.textComponent.text = localized;
                                ti.textComponent.ForceMeshUpdate(true, true);
                            }                        
                        };
                    }
                }

                // Force-enable components
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

