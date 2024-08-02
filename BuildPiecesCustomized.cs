using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ServerSync;
using UnityEngine;
using System.Linq;
using System.Collections;
using System.Text.RegularExpressions;

namespace BuildPiecesCustomized
{
    [BepInPlugin(pluginID, pluginName, pluginVersion)]
    [BepInIncompatibility("aedenthorn.BuildPieceTweaks")]
    [BepInIncompatibility("TheSxW_EditMaterialProperties")]
    [BepInIncompatibility("lime.plugins.foreverbuild")]
    [BepInIncompatibility("bonesbro.val.floorsareroofs")]
    internal class BuildPiecesCustomized : BaseUnityPlugin
    {
        const string pluginID = "shudnal.BuildPiecesCustomized";
        const string pluginName = "Build Pieces Customized";
        const string pluginVersion = "1.0.7";

        private readonly Harmony harmony = new Harmony(pluginID);

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        internal static ConfigEntry<bool> modEnabled;
        internal static ConfigEntry<bool> configLocked;
        internal static ConfigEntry<bool> loggingEnabled;

        internal static ConfigEntry<string> toolsToPatchPieces;

        internal static ConfigEntry<string> prefabListClipEverything;
        internal static ConfigEntry<string> prefabListAllowedInDungeons;
        internal static ConfigEntry<string> prefabListRepairPiece;
        internal static ConfigEntry<string> prefabListCanBeRemoved;

        internal static ConfigEntry<string> prefabListAshDamageImmune;
        internal static ConfigEntry<string> prefabListNoRoofWear;
        internal static ConfigEntry<string> prefabListNoSupportWear;

        internal static readonly Dictionary<WearNTear.MaterialType, MaterialPropertiesConfig> materialConfigs = new Dictionary<WearNTear.MaterialType, MaterialPropertiesConfig>();

        internal static BuildPiecesCustomized instance;

        private static readonly CustomSyncedValue<Dictionary<string, string>> configsJSON = new CustomSyncedValue<Dictionary<string, string>>(configSync, "JSON configs", new Dictionary<string, string>());

        internal static readonly Dictionary<string, CustomPieceData> pieceData = new Dictionary<string, CustomPieceData>();
        internal static readonly Dictionary<string, CraftingStation> craftingStations = new Dictionary<string, CraftingStation>();
        internal static readonly Dictionary<string, CustomPieceData> defaultPieceData = new Dictionary<string, CustomPieceData>();

        internal static DirectoryInfo pluginDirectory;
        internal static DirectoryInfo configDirectory;

        private static FileSystemWatcher fileSystemWatcherPlugin;
        private static FileSystemWatcher fileSystemWatcherConfig;

        internal const string allPiecesIdentifier = "AllPieces";

        internal class MaterialPropertiesConfig
        {
            public ConfigEntry<float> maxSupport;
            public ConfigEntry<float> minSupport;
            public ConfigEntry<float> verticalLoss;
            public ConfigEntry<float> horizontalLoss;
        }

        private void Awake()
        {
            harmony.PatchAll();

            instance = this;

            pluginDirectory = new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent;
            configDirectory = new DirectoryInfo(Path.Combine(Paths.ConfigPath, pluginID));

            ConfigInit();

            _ = configSync.AddLockingConfigEntry(configLocked);

            configsJSON.ValueChanged += new Action(LoadConfigs);

            Game.isModded = true;
        }

        private void OnDestroy()
        {
            Config.Save();
            instance = null;
            harmony?.UnpatchSelf();
        }

        public static void LogInfo(object data)
        {
            if (loggingEnabled.Value)
                instance.Logger.LogInfo(data);
        }
        public static void LogWarning(object data)
        {
            instance.Logger.LogWarning(data);
        }

        public void ConfigInit()
        {
            config("General", "NexusID", 2782, "Nexus mod ID for updates", false);

            modEnabled = config("General", "Enabled", defaultValue: true, "Enable the mod.");
            configLocked = config("General", "Lock Configuration", defaultValue: true, "Configuration is locked and can be changed by server admins only.");
            loggingEnabled = config("General", "Logging enabled", defaultValue: false, "Enable logging. [Not Synced with Server]", false);

            toolsToPatchPieces = config("General", "Tools list", defaultValue: "Hammer,Hoe,Cultivator", "Comma-separated list of tool prefab name to get build pieces from");

            prefabListClipEverything = config("List - Global setting", "Clip everything", defaultValue: "", "Comma-separated list of pieces that will clip through each other. Set \"" + allPiecesIdentifier + "\" identifier to apply for all pieces.");
            prefabListAllowedInDungeons = config("List - Global setting", "Allow in dungeons", defaultValue: "", "Comma-separated list of pieces that will be allowed to build in dungeons. Set \"" + allPiecesIdentifier + "\" identifier to apply for all pieces.");
            prefabListRepairPiece = config("List - Global setting", "Can be repaired", defaultValue: "", "Comma-separated list of pieces that will be repairable. Set \"" + allPiecesIdentifier + "\" identifier to apply for all pieces.");
            prefabListCanBeRemoved = config("List - Global setting", "Can be removed", defaultValue: "", "Comma-separated list of pieces that will be removeable. Set \"" + allPiecesIdentifier + "\" identifier to apply for all pieces.");

            prefabListAshDamageImmune = config("List - Immune to", "Ash and lava", defaultValue: "", "Comma-separated list of pieces that will be immune to ash and lava damage. Set \"" + allPiecesIdentifier + "\" identifier to apply for all pieces.");
            prefabListNoRoofWear = config("List - Immune to", "Water damage", defaultValue: "", "Comma-separated list of pieces that will be immune to water damage. Set \"" + allPiecesIdentifier + "\" identifier to apply for all pieces.");
            prefabListNoSupportWear = config("List - Immune to", "Structural integrity", defaultValue: "", "Comma-separated list of pieces that will not be needed support. Set \"" + allPiecesIdentifier + "\" identifier to apply for all pieces.");

            foreach (WearNTear.MaterialType materialType in Enum.GetValues(typeof(WearNTear.MaterialType)))
            {
                materialConfigs.Add(materialType, new MaterialPropertiesConfig()
                {
                    maxSupport = config($"Material - {materialType}", "Max support multiplier", defaultValue: 1f, "Multiplier of maximum support value material can provide."),
                    minSupport = config($"Material - {materialType}", "Min support multiplier", defaultValue: 1f, "Multiplier of minimum support value material can provide."),
                    verticalLoss = config($"Material - {materialType}", "Vertical stability multiplier", defaultValue: 1f, "Multiplier of vertical support value. Increase to be able to build higher."),
                    horizontalLoss = config($"Material - {materialType}", "Horizontal stability multiplier", defaultValue: 1f, "Multiplier of horizontal support value. Increase to be able to build longer hanging beams.")
                });
            }

            InitCommands();
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, defaultValue, description);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, string description, bool synchronizedSetting = true) => config(group, name, defaultValue, new ConfigDescription(description), synchronizedSetting);

        public static void InitCommands()
        {
            new Terminal.ConsoleCommand("bpcsaveall", $"[Prefab partial name or wildcard *] - save data of ALL pieces into JSON files to config directory {configDirectory.Name}", delegate (Terminal.ConsoleEventArgs args)
            {
                bool filterPieces = args.Length >= 2;
                string prefabNameFilter = filterPieces ? args.FullLine.Substring(args[0].Length + 1) : "";

                foreach (GameObject piece in CustomPieceData.GetBuildPieces())
                {
                    CustomPieceData pieceData = CustomPieceData.GetByPieceName(piece.name);
                    if (pieceData != null && (!filterPieces || pieceData.prefabName.IndexOf(prefabNameFilter) != -1 || Regex.IsMatch(pieceData.prefabName, WildCardToRegular(prefabNameFilter))))
                    {
                        pieceData.SaveToDirectory(configDirectory.FullName);
                        args.Context?.AddString($"Saved {pieceData.prefabName} file to config directory");
                    }
                }
            }, optionsFetcher: () => CustomPieceData.GetBuildPieces().Select(piece => piece.name).ToList());

            new Terminal.ConsoleCommand("bpcsave", $"[Prefab name] - save piece data into JSON file to config directory {configDirectory.Name}", delegate (Terminal.ConsoleEventArgs args)
            {
                if (args.Length >= 2)
                {
                    string prefabName = args.FullLine.Substring(args[0].Length + 1);
                    CustomPieceData pieceData = CustomPieceData.GetByPieceName(prefabName);
                    if (pieceData == null)
                        args.Context?.AddString($"Piece with name {prefabName} was not found");
                    else
                    {
                        pieceData.SaveToDirectory(configDirectory.FullName);
                        args.Context?.AddString($"Saved {prefabName} file to config directory");
                    }
                }
            }, optionsFetcher:() => CustomPieceData.GetBuildPieces().Select(piece => piece.name).ToList());

            new Terminal.ConsoleCommand("bpcdocs", $"Save documentation file {DocGen.filename} to config directory", (Terminal.ConsoleEventArgs args) => DocGen.GenerateDocumentationFile());

            string WildCardToRegular(string value)
            {
                return "^" + Regex.Escape(value).Replace("\\*", ".*") + "$";
            }
        }

        public static void SetupConfigWatcher()
        {
            string filter = $"*.json";

            if (fileSystemWatcherPlugin == null)
            {
                fileSystemWatcherPlugin = new FileSystemWatcher(pluginDirectory.FullName, filter);
                fileSystemWatcherPlugin.Changed += new FileSystemEventHandler(ReadConfigs);
                fileSystemWatcherPlugin.Created += new FileSystemEventHandler(ReadConfigs);
                fileSystemWatcherPlugin.Renamed += new RenamedEventHandler(ReadConfigs);
                fileSystemWatcherPlugin.Deleted += new FileSystemEventHandler(ReadConfigs);
                fileSystemWatcherPlugin.IncludeSubdirectories = true;
                fileSystemWatcherPlugin.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            }

            fileSystemWatcherPlugin.EnableRaisingEvents = modEnabled.Value;

            if (fileSystemWatcherConfig == null)
            {
                if (configDirectory.Exists)
                {
                    fileSystemWatcherConfig = new FileSystemWatcher(configDirectory.FullName, filter);
                    fileSystemWatcherConfig.Changed += new FileSystemEventHandler(ReadConfigs);
                    fileSystemWatcherConfig.Created += new FileSystemEventHandler(ReadConfigs);
                    fileSystemWatcherConfig.Renamed += new RenamedEventHandler(ReadConfigs);
                    fileSystemWatcherConfig.Deleted += new FileSystemEventHandler(ReadConfigs);
                    fileSystemWatcherConfig.IncludeSubdirectories = true;
                    fileSystemWatcherConfig.SynchronizingObject = ThreadingHelper.SynchronizingObject;
                }
            }

            if (fileSystemWatcherConfig != null)
                fileSystemWatcherConfig.EnableRaisingEvents = modEnabled.Value;

            ReadConfigs(null, null);
        }

        private static void ReadConfigs(object sender, FileSystemEventArgs eargs)
        {
            Dictionary<string, string> localConfig = new Dictionary<string, string>();

            FileInfo[] configFiles = pluginDirectory.GetFiles("*.json", SearchOption.AllDirectories);

            if (configDirectory.Exists)
                configFiles = configFiles.AddRangeToArray(configDirectory.GetFiles("*.json", SearchOption.AllDirectories));

            foreach (FileInfo file in configFiles)
            {
                LogInfo($"Found {file.FullName}");

                try
                {
                    using (FileStream fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (StreamReader reader = new StreamReader(fs))
                    {
                        localConfig.Add(Path.GetFileNameWithoutExtension(file.Name), reader.ReadToEnd());
                        reader.Close();
                        fs.Dispose();
                    }
                }
                catch (Exception e)
                {
                    LogWarning($"Error reading file ({file.FullName})! Error: {e.Message}");
                }
            }

            configsJSON.AssignLocalValue(localConfig);
        }

        private static void LoadConfigs()
        {
            pieceData.Clear();

            foreach (KeyValuePair<string, string> configJSON in configsJSON.Value)
            {
                try
                {
                    pieceData.Add(configJSON.Key, JsonUtility.FromJson<CustomPieceData>(configJSON.Value));
                }
                catch (Exception e)
                {
                    LogWarning($"Error parsing item ({configJSON.Key})! Error: {e.Message}");
                }
            }

            if (ZNetScene.instance)
                instance.StartCoroutine(PatchPieces());

            Piece.s_allPieces?.Do(piece => PatchPiece(piece));

            Player.m_localPlayer?.UpdateAvailablePiecesList();
        }

        private static void FillCraftingStations()
        {
            craftingStations.Clear();
            foreach (Recipe recipe in ObjectDB.instance.m_recipes)
            {
                if (recipe?.m_craftingStation == null)
                    continue;

                if (craftingStations.ContainsKey(recipe.m_craftingStation.name))
                    continue;

                craftingStations[recipe.m_craftingStation.name] = recipe.m_craftingStation;
                craftingStations[recipe.m_craftingStation.m_name] = recipe.m_craftingStation;
                craftingStations[recipe.m_craftingStation.m_name.Substring(1)] = recipe.m_craftingStation;
            }
        }

        private static void PatchPiece(Piece piece)
        {
            if (piece == null || !ZNetScene.instance)
                return;

            string name = Utils.GetPrefabName(piece.gameObject);
            if (!defaultPieceData.ContainsKey(name))
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(name);

                if (prefab == null || !prefab.TryGetComponent(out Piece defaultPiece))
                    return;

                defaultPieceData[name] = new CustomPieceData(defaultPiece);
            }

            defaultPieceData[name].PatchPiece(piece);

            if (pieceData.ContainsKey(name))
            {
                LogInfo($"Patching {name}");
                pieceData[name].PatchPiece(piece);
            }

            if (prefabListClipEverything.Value.Contains(allPiecesIdentifier) || prefabListClipEverything.Value.IndexOf(name) != -1)
                piece.m_clipEverything = true;

            if (prefabListAllowedInDungeons.Value.Contains(allPiecesIdentifier) || prefabListAllowedInDungeons.Value.IndexOf(name) != -1)
                piece.m_allowedInDungeons = true;

            if (prefabListRepairPiece.Value.Contains(allPiecesIdentifier) || prefabListRepairPiece.Value.IndexOf(name) != -1)
                piece.m_repairPiece = true;

            if (prefabListCanBeRemoved.Value.Contains(allPiecesIdentifier) || prefabListCanBeRemoved.Value.IndexOf(name) != -1)
                piece.m_canBeRemoved = true;

            if (piece.TryGetComponent(out WearNTear wnt))
            {
                if (prefabListAshDamageImmune.Value.Contains(allPiecesIdentifier) || prefabListAshDamageImmune.Value.IndexOf(name) != -1)
                    wnt.m_ashDamageImmune = true;

                if (prefabListNoRoofWear.Value.Contains(allPiecesIdentifier) || prefabListNoRoofWear.Value.IndexOf(name) != -1)
                    wnt.m_noRoofWear = false;

                if (prefabListNoSupportWear.Value.Contains(allPiecesIdentifier) || prefabListNoSupportWear.Value.IndexOf(name) != -1)
                    wnt.m_noSupportWear = false;
            }

            if (piece.m_nview != null && piece.m_nview.IsValid())
                piece.m_nview.LoadFields();
        }

        private static IEnumerator PatchPieces()
        {
            yield return new WaitUntil(() => ObjectDB.instance != null);

            FillCraftingStations();

            yield return new WaitForFixedUpdate();

            foreach (GameObject go in CustomPieceData.GetBuildPieces())
                if (go != null && go.TryGetComponent(out Piece piece))
                    PatchPiece(piece);
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        [HarmonyPriority(Priority.Last)]
        private static class ZNetScene_Awake_PatchPieces
        {
            private static void Postfix()
            {
                if (!modEnabled.Value)
                    return;

                SetupConfigWatcher();
            }
        }

        [HarmonyPatch(typeof(Piece), nameof(Piece.Awake))]
        private static class Piece_Awake_PatchPiece
        {
            private static void Postfix(Piece __instance)
            {
                if (!modEnabled.Value)
                    return;

                PatchPiece(__instance);
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.GetMaterialProperties))]
        private static class WearNTear_GetMaterialProperties_MaterialProperties
        {
            private static void Postfix(WearNTear __instance, ref float maxSupport, ref float minSupport, ref float horizontalLoss, ref float verticalLoss)
            {
                if (!modEnabled.Value)
                    return;

                if (!materialConfigs.ContainsKey(__instance.m_materialType))
                    return;

                maxSupport *= materialConfigs[__instance.m_materialType].maxSupport.Value;
                minSupport *= materialConfigs[__instance.m_materialType].minSupport.Value;
                horizontalLoss /= materialConfigs[__instance.m_materialType].horizontalLoss.Value;
                verticalLoss /= materialConfigs[__instance.m_materialType].verticalLoss.Value;
            }
        }
    }
}
