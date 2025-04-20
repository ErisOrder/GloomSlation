using Gloomwood.Languages;
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
using UnityEngine.UI;
using Gloomwood.Entity;
using Gloomwood.Entity.Items;
using System.Linq;


[assembly: MelonInfo(typeof(GloomSlation.GloomSlation), "GloomSlation", "0.1.302.08-modv0.4", "pipo, nikvoid")]
[assembly: MelonGame("Dillon Rogers", "Gloomwood")]

namespace GloomSlation
{
    public static class Extensions
    {
        public static T ReadPrivateField<T>(this object instance, string name)
        {
            return (T)instance
                .GetType()
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(instance);
        }

        public static bool NullOrEmpty(this string inst)
        {
            return inst == null || inst == string.Empty;
        }

        /// Try to add marker component on GameObject
        public static bool SetMarker<T>(this GameObject obj) where T : Marker
        {
            // Already set
            if (obj.GetComponent<T>() != null)
            {
                return false;
            }

            // Set mark
            obj.AddComponent<T>();
            return true;
        }
    }

    public class Marker : MonoBehaviour { }

    // Classes for marking translated objects
    public class TextProcessedMarker : Marker { }
    public class SpriteProcessedMarker : Marker { }
    public class TextureProcessedMarker : Marker { }

    // Recursively visit objects in scene and apply patches on matched paths
    class AdjustVisitor {
        Dictionary<String, List<(bool, Action<Component>, Type)>> adjustments = 
            new Dictionary<string, List<(bool, Action<Component>, Type)>>();
    
        public void RunAdjustments(GameObject[] roots) {
            foreach (var root in roots) {
                if (VisitNeeded(root.name)) {
                    AdjustObjectsRec(root, root.name);
                }
            }
        }

        bool VisitNeeded(string pathStart) {
            return adjustments.Any((kv) => kv.Key.StartsWith(pathStart));
        } 

        void AdjustObjectsRec(GameObject obj, string path) {
            foreach (Transform child in obj.transform) {
                var childObj = child.gameObject;
                var thisPath = $"{path}/{childObj.name}";
                if (adjustments.TryGetValue(thisPath, out var list)) {
                    foreach ((var children, var adjust, var ty) in list) {
                        Component[] comps;
                        if (children) {
                            comps = childObj.GetComponentsInChildren(ty, true);
                        } else {
                            comps = childObj.GetComponents(ty);
                        }
                        
                        foreach (var comp in comps) {
                            Melon<GloomSlation>.Instance.DebugMsg($"adjusting {thisPath}");
                            adjust(comp);
                        }                           
                    }
                }
                if (VisitNeeded(thisPath)) {
                    AdjustObjectsRec(childObj, thisPath);
                }
            }
        }

        public void AddAdjustment<T>(bool children, string path, Action<T> adjust) where T: Component {
            Action<Component> dynAdjust = (comp) => {
                adjust((comp as T)!);
            };
            
            if (adjustments.TryGetValue(path, out var val)) {
                val.Add((children, dynAdjust, typeof(T)));
            } else {
                adjustments.Add(path, new List<(bool, Action<Component>, Type)> { (children, dynAdjust, typeof(T)) });
            }
        } 
        
        public void AddAdjustment<T>(bool recursive, string[] paths, Action<T> adjust) where T: Component {
            foreach (var path in paths) {
                AddAdjustment<T>(recursive, path, adjust);
            }
        } 
    }
    
    public class GloomSlation : MelonMod
    {
        private readonly Dictionary<string, TMP_FontAsset> fontMap = new Dictionary<string, TMP_FontAsset>();
        private readonly HashSet<string> availableTextures = new HashSet<string>();
        private readonly HashSet<string> specializedTex = new HashSet<string>();
        private readonly AdjustVisitor postInitAdjustVisitor = new AdjustVisitor();

        private static readonly string modPath = "Mods\\GloomSlation";

        private static readonly Regex notIdentRex = new Regex("[^0-9a-zA-Z_]+");

        private string langPath = "";
        private bool debugMode = false;

        public override void OnInitializeMelon()
        {
            // We want full stacktraces to be printed in case of exception
            Application.logMessageReceived += (text, trace, type) =>
            {
                if (type == LogType.Exception)
                {
                    MelonLogger.Error($"[Unity {type}] {text}\n{trace}");
                }
            };

            // Initialize preferences
            var prefCategory = MelonPreferences.CreateCategory("GloomSlation");
            prefCategory.SetFilePath(Path.Combine(modPath, "cfg.toml"));
            var languageEntry = prefCategory.CreateEntry<string>("language", "Russian");
            var debugEntry = prefCategory.CreateEntry<bool>("debug", false);

            langPath = Path.Combine(modPath, languageEntry.Value);
            debugMode = debugEntry.Value;

            // Read font map
            var text = File.ReadAllText(Path.Combine(langPath, "fontMap.json"));
            if (!(JSON.Load(text) is ProxyObject fMap))
            {
                LoggerInstance.Error("failed to load fontMap.json");
                return;
            }

            // Load asset bundle
            var bundle = AssetBundle.LoadFromFile(Path.Combine(langPath, "font.bundle"));
            DebugMsg("Loaded asset bundle, available assets:");
            foreach (var name in bundle.GetAllAssetNames())
            {
                DebugMsg(name);
            }

            // Load fonts
            DebugMsg("Font mapping:");
            foreach (var (k, v) in fMap)
            {
                var asset = (TMP_FontAsset)bundle.LoadAsset($"Assets/{v}.asset");
                if (asset == null)
                {
                    LoggerInstance.Error($"Asset not found: {k}");
                }
                else
                {
                    fontMap.Add(k, asset);
                    DebugMsg($"{k} -> {v}");
                }
            }

            // Load textures
            var texPath = Path.Combine(langPath, "Textures");
            if (Directory.Exists(texPath))
            {
                foreach (var path in Directory.EnumerateFiles(texPath))
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (Path.GetExtension(path) != ".png")
                    {
                        continue;
                    }

                    name = PreparePatchName(name);

                    if (name.StartsWith("obj_")) {
                        var objpath = name.Substring(4);
                        specializedTex.Add(objpath);
                        DebugMsg($"Available specialized texture for {objpath}");
                    }

                    availableTextures.Add(name);
                    DebugMsg($"Available texture {name}");
                }
            }

            InitAdjustments();
        }

        /// Log message only if debugMode active
        public void DebugMsg(object msg)
        {
            if (debugMode)
            {
                MelonLogger.Msg(msg);
            }
        }

        /// Patch all scene's GameObjects
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            var scene = SceneManager.GetSceneByBuildIndex(buildIndex);
            /// Patch all scene textures
            foreach (var tex in Resources.FindObjectsOfTypeAll<Texture2D>()) {
                DebugMsg($"found texture {tex.name}");
                PatchTexture(tex);
            }

            foreach (var obj in scene.GetRootGameObjects())
            {
                PatchGameObject(obj);
            }
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            var scene = SceneManager.GetSceneByBuildIndex(buildIndex);
            postInitAdjustVisitor.RunAdjustments(scene.GetRootGameObjects());
        }

        /// Construct localization entry ID from GameObject name and original text
        public static string ConstructLocaleKey(string objName, string origText)
        {
            return notIdentRex.Replace($"{objName}@{origText}", "_");
        }

        /// Convert object/texture name to path
        private string PreparePatchName(string original) {
            return original.Replace("|", "_").ToLower();
        }
        
        /// Construct texture path if patch exists from texture name and optionally object name
        public string? GetTexturePath(string name, bool obj) {
            string filename;
            name = PreparePatchName(name);
            if (obj) {
                var objpath = name.Replace("/", "_");
                
                if (!specializedTex.Contains(objpath)) {
                    return null;
                }
            
                filename = $"obj_{objpath}";
            } else {
                if (!availableTextures.Contains(name)) {
                    return null;
                }
            
                filename = name;
            }
            return Path.Combine(langPath, $"Textures/{filename}.png");
        }

        void InitAdjustments() {
            // Cliffside title
            // Aligned by moving label objects...
            // ---A
            // ------B
            // ---C
            // ----D
            var cliffsideMenu = new [] {
                ("TitleScreen/Menu_Title/Button_NewGameMenu", 248.85f),
                ("TitleScreen/Menu_Title/Button_LoadGameMenu", 248.7f),
                ("TitleScreen/Menu_Title/Button_SettingsMenu", 249f),
                ("TitleScreen/Menu_Title/Button_QuitGame", 249f)
            };
            var posY = 23.7f;
            foreach (var (btn, nx) in cliffsideMenu) {
                var ny = posY;
                postInitAdjustVisitor.AddAdjustment<TMPro.TextMeshProUGUI>(
                    true,
                    btn,
                    tmp => {
                        // Align all objects by central axis and set text alignment to Center
                        var tf = tmp.gameObject.transform.parent;
                        var pos = tf.position;
                        pos.x = nx;
                        pos.y = ny;
                        tf.position = pos;
                        tmp.alignment = TextAlignmentOptions.Center;
                        var rtf = tmp.rectTransform;
                        rtf.localPosition = Vector3.zero;
                        // tmp.bounds;
                    }
                );
                posY -= 0.5f;
            }
            
            // Underport title
            // New Game and Load buttons slightly misaligned
            var underportMenu = new [] {
                ("Menu_Title/Button_NewGameMenu", 59.05f),
                ("Menu_Title/Button_LoadGameMenu", 59.21f)
            };
            foreach (var (btn, nx) in underportMenu) {
                postInitAdjustVisitor.AddAdjustment<TMPro.TextMeshProUGUI>(
                    true,
                    btn,
                    tmp => {
                        var tf = tmp.gameObject.transform.parent;
                        var pos = tf.position;
                        pos.x = nx;
                        tf.position = pos;
                    }
                );
            }
        }
        
        /// Load texture from disk
        public void PatchTexture(Texture2D tex) {
            if (GetTexturePath(tex.name, false) is string path)
            {
                tex.LoadImage(File.ReadAllBytes(path), true);
                tex.name += " [patched]";
                DebugMsg($"Patched texture {tex.name}");
            }
        }

        /// Patch single TextMeshProUGUI
        private void PatchTextMesh(TextMeshProUGUI tmp)
        {
            // Remap fonts
            if (tmp.font != null)
            {
                TMP_FontAsset font;
                if (fontMap.TryGetValue(tmp.font.name, out font))
                {
                    tmp.font = font;
                }
                else if (fontMap.TryGetValue("FALLBACK_FONT", out font))
                {
                    DebugMsg($"mapping {tmp.font.name} -> FALLBACK");
                    tmp.font = font;
                }
            }

            var tmpObj = tmp.gameObject;
            var component = tmpObj.GetComponent<TextBehaviour>();
            var localizedText = tmpObj.GetComponent<LocalizedText>();

            // For every object that doesn't have localization component or key, 
            // generate key and try to get localization
            if (!tmp.text.NullOrEmpty()
                && (component == null || localizedText == null || !localizedText.enabled)
            )
            {
                var localeKey = ConstructLocaleKey(tmpObj.name, tmp.text);

                DebugMsg($"Found unlocalized text: {localeKey} = \"{tmp.text}\";");

                // Most of such texts are related to menu, so let's store there all of them
                if (GameManager.LanguageManager.TryGetLocalizedContent(
                    LanguageDataTypes.Menus,
                    localeKey,
                    out string localized
                ))
                {
                    DebugMsg("Found localization!");
                    var previousText = tmp.text;
                    tmp.text = localized;

                    // This prevents anything from changing our text back
                    tmp.OnPreRenderText += (ti) =>
                    {
                        // Prerender triggered by our action
                        if (ti.textComponent.text == localized)
                        {
                            return;
                        }

                        var newLocaleKey = ConstructLocaleKey(tmpObj.name, ti.textComponent.text);
                        DebugMsg($"Unlocalized change: {localeKey} -> {newLocaleKey}: \"{ti.textComponent.text}\"");
                        if (GameManager.LanguageManager.TryGetLocalizedContent(
                            LanguageDataTypes.Menus,
                            newLocaleKey,
                            out string localizedNew
                        ))
                        {
                            // Update text in case there's something localized
                            DebugMsg("Found new localization!");
                            previousText = ti.textComponent.text;
                            ti.textComponent.text = localizedNew;
                            localized = localizedNew;
                            ti.textComponent.ForceMeshUpdate(true, true);
                        }
                        else if (previousText != ti.textComponent.text)
                        {
                            // Do nothing; allow to change value
                        }
                        else
                        {
                            // Write previous value back
                            ti.textComponent.text = localized;
                            ti.textComponent.ForceMeshUpdate(true, true);
                        }
                    };
                }
            }

            // Force-enable components
            if (component != null)
            {
                component.enabled = true;
            }
        }

        /// Get absolute path to gameobject
        public static string GetGameObjectPath(GameObject obj)
        {
            string path = "/" + obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                path = "/" + obj.name + path;
            }
            return path;
        }
        
        /// Patch main texture in renderer's material
        public void PatchRenderer(Renderer rend)
        {
            foreach (var mat in rend.materials)
            {
                if (mat == null)
                {
                    return;
                }
                foreach (var prop in mat.GetTexturePropertyNames())
                {
                    if (prop != "_MainTex")
                    {
                        continue;
                    }
                    // Strip leading slash
                    var objpath = GetGameObjectPath(rend.gameObject).Substring(1);
                    var tex = mat.GetTexture(prop);
                    if (tex != null && tex.name != null
                        && GetTexturePath(objpath, true) is string nTexPath
                    )
                    {
                        DebugMsg($"Patching specialized texture in {objpath}");
                        var nTex = new Texture2D(1, 1, TextureFormat.RGBA32, 0, false);
                        nTex.LoadImage(File.ReadAllBytes(nTexPath), true);
                        mat.SetTexture(prop, nTex);
                    }
                }
            }
        }

        /// Patch GameObject and all of its childen
        public void PatchGameObject(GameObject obj)
        {
            // Patch all nested texts found
            foreach (var tmp in obj.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
            {
                // If already processed, skip
                if (tmp.gameObject.SetMarker<TextProcessedMarker>())
                {
                    PatchTextMesh(tmp);
                }
            }
            // Patch all textures in materials
            foreach (var rend in obj.GetComponentsInChildren<Renderer>())
            {
                if (rend.gameObject.SetMarker<TextureProcessedMarker>())
                {
                    PatchRenderer(rend);
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

            DebugMsg($"Loaded translation ({dataType})");

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
    static class PatchTextBehaviour
    {
        static void Prefix(
            // ref bool localize,
            ref bool lowercase
        )
        {

            // Text in some places is forcefully set to lowercase.
            // Undo that
            lowercase = false;
        }
    }

    [HarmonyPatch(typeof(ShopReceiptUI), "AddListing")]
    static class PatchReceipt
    {
        static void Postfix(
            ref List<GameObject> ___spawnedElements
        )
        {
            // It seems like receipt layout is hardcoded to 1 item = 1 text line
            // somewhere. 
            // Here we are fixing it to be more flexible and expand to several lines if needed

            // Item is pushed first, then value
            var item = ___spawnedElements[___spawnedElements.Count - 2];
            var value = ___spawnedElements[___spawnedElements.Count - 1];
            var tmp = item.GetComponent<TextMeshProUGUI>();
            var layoutItem = item.GetComponent<LayoutElement>();
            var layoutValue = value.GetComponent<LayoutElement>();

            // Set layout mininmum height based on item text preferred height
            layoutItem.minHeight = tmp.preferredHeight;
            layoutValue.minHeight = tmp.preferredHeight;
        }
    }

    /// Patch entity model when it is created, this fixes texture patching on first
    /// examination in inventory (and probably not only in it)
    /// TODO: Find a more general way to patch objects on creation? 
    [HarmonyPatch(typeof(InventoryItemConfig), "CreateModel", new Type[] { typeof(Vector3), typeof(Quaternion), typeof(Transform) })]
    static class PatchEntityCreateModel
    {
        static void Postfix(ref EntityModel __result)
        {
            Melon<GloomSlation>.Instance.PatchGameObject(__result.gameObject);
        }
    }

    /// Force inventory item quantity, slot, etc. to be rendered as overlay.
    /// TODO: This should be achievable through changing font asset in some way, 
    /// but we currently haven't figured out, what to change exactly. 
    [HarmonyPatch(typeof(TextIcon), "Awake")]
    static class PatchTextIcon
    {
        static void Prefix(ref TextBehaviour ___counterText)
        {
            var tmp = ___counterText.ReadPrivateField<TextMeshProUGUI>("textMesh");
            // It won't actually be applied other way
            tmp.OnPreRenderText += (ti) =>
            {
                ti.textComponent.isOverlay = true;
            };
        }
    }
}

