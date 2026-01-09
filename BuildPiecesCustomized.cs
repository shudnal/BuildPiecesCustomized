using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using Newtonsoft.Json;
using ServerSync;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using YamlDotNet.Serialization;

namespace BuildPiecesCustomized
{
    [BepInPlugin(pluginID, pluginName, pluginVersion)]
    [BepInIncompatibility("aedenthorn.BuildPieceTweaks")]
    [BepInIncompatibility("TheSxW_EditMaterialProperties")]
    [BepInIncompatibility("lime.plugins.foreverbuild")]
    [BepInIncompatibility("bonesbro.val.floorsareroofs")]
    public class BuildPiecesCustomized : BaseUnityPlugin
    {
        public const string pluginID = "shudnal.BuildPiecesCustomized";
        public const string pluginName = "Build Pieces Customized";
        public const string pluginVersion = "1.2.0";

        private readonly Harmony harmony = new Harmony(pluginID);

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        internal static ConfigEntry<bool> modEnabled;
        internal static ConfigEntry<bool> configLocked;
        internal static ConfigEntry<bool> loggingEnabled;
        internal static ConfigEntry<bool> saveAsYAML;

        internal static ConfigEntry<string> toolsToPatchPieces;

        internal static ConfigEntry<string> prefabListClipEverything;
        internal static ConfigEntry<string> prefabListAllowedInDungeons;
        internal static ConfigEntry<string> prefabListRepairPiece;
        internal static ConfigEntry<string> prefabListCanBeRemoved;
        internal static ConfigEntry<string> prefabListIsRoof;
        internal static ConfigEntry<string> prefabListIsLeaky;
        internal static ConfigEntry<string> prefabListDisabled;

        internal static ConfigEntry<string> prefabListAshDamageImmune;
        internal static ConfigEntry<string> prefabListNoRoofWear;
        internal static ConfigEntry<string> prefabListNoSupportWear;

        internal static readonly Dictionary<WearNTear.MaterialType, MaterialPropertiesConfig> materialConfigs = new Dictionary<WearNTear.MaterialType, MaterialPropertiesConfig>();

        internal static BuildPiecesCustomized instance;

        internal static readonly CustomSyncedValue<Dictionary<string, string>> configsJSON = new CustomSyncedValue<Dictionary<string, string>>(configSync, "JSON configs", new Dictionary<string, string>());
        internal static readonly CustomSyncedValue<Dictionary<string, int>> pieceCategories = new CustomSyncedValue<Dictionary<string, int>>(configSync, "Pieces categories", new Dictionary<string, int>());

        internal static readonly Dictionary<string, CustomPieceData> pieceData = new Dictionary<string, CustomPieceData>();
        internal static readonly Dictionary<string, CraftingStation> craftingStations = new Dictionary<string, CraftingStation>();
        internal static readonly Dictionary<string, CustomPieceData> defaultPieceData = new Dictionary<string, CustomPieceData>();

        internal static DirectoryInfo pluginDirectory;
        internal static DirectoryInfo configDirectory;

        private static FileSystemWatcher fileSystemWatcherPlugin;
        private static FileSystemWatcher fileSystemWatcherConfig;

        internal static readonly IDeserializer YamlDeserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        internal static readonly ISerializer YamlSerializer = new SerializerBuilder().Build();

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
            pieceCategories.ValueChanged += new Action(PiecePatches.UpdatePiecesProperties);

            Game.isModded = true;
        }

        private void OnDestroy()
        {
            Config.Save();
            harmony?.UnpatchSelf();
            instance = null;
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

        private ConfigDescription GetDescriptionSeparatedStrings(string description) =>
            Chainloader.PluginInfos.ContainsKey("_shudnal.ConfigurationManager")
                    ? new ConfigDescription(description)
                    : new ConfigDescription(description, null, new CustomConfigs.ConfigurationManagerAttributes { CustomDrawer = CustomConfigs.DrawSeparatedStrings(",") });
        public void ConfigInit()
        {
            config("General", "NexusID", 2782, "Nexus mod ID for updates", false);

            modEnabled = config("General", "Enabled", defaultValue: true, "Enable the mod.");
            configLocked = config("General", "Lock Configuration", defaultValue: true, "Configuration is locked and can be changed by server admins only.");
            loggingEnabled = config("General", "Logging enabled", defaultValue: false, "Enable logging. [Not Synced with Server]", false);
            saveAsYAML = config("General", "Save piece data as YAML", defaultValue: false, "Save piece data in YAML format. [Not Synced with Server]", false);

            toolsToPatchPieces = config("General", "Tools list", defaultValue: "Hammer,Hoe,Cultivator", "Comma-separated list of tool prefab name to get build pieces from");

            toolsToPatchPieces.SettingChanged += (s, e) => PiecePatches.UpdatePiecesProperties();

            prefabListClipEverything = config("List - Global setting", "Clip everything", defaultValue: "", GetDescriptionSeparatedStrings("Comma-separated list of pieces that will clip through each other. Set \"" + PiecePatches.GlobalPatches.allPiecesIdentifier + "\" identifier to apply for all pieces."));
            prefabListAllowedInDungeons = config("List - Global setting", "Allow in dungeons", defaultValue: "", GetDescriptionSeparatedStrings("Comma-separated list of pieces that will be allowed to build in dungeons. Set \"" + PiecePatches.GlobalPatches.allPiecesIdentifier + "\" identifier to apply for all pieces."));
            prefabListRepairPiece = config("List - Global setting", "Can be repaired", defaultValue: "", GetDescriptionSeparatedStrings("Comma-separated list of pieces that will be repairable. Set \"" + PiecePatches.GlobalPatches.allPiecesIdentifier + "\" identifier to apply for all pieces."));
            prefabListCanBeRemoved = config("List - Global setting", "Can be removed", defaultValue: "", GetDescriptionSeparatedStrings("Comma-separated list of pieces that will be removeable. Set \"" + PiecePatches.GlobalPatches.allPiecesIdentifier + "\" identifier to apply for all pieces."));
            prefabListDisabled = config("List - Global setting", "Disabled pieces", defaultValue: "", GetDescriptionSeparatedStrings("Comma-separated list of pieces that will be disabled."));
            prefabListIsRoof = config("List - Global setting", "Is Roof", defaultValue: "", GetDescriptionSeparatedStrings("Comma-separated list of pieces that will work as a roof. Set \"" + PiecePatches.GlobalPatches.allPiecesIdentifier + "\" identifier to apply for all pieces." +
                                                                                            "\nLeaky -> Roof transition will be applied immediately. " +
                                                                                            "\nRestart the game if you need Roof -> Leaky transition of updated pieces."));
            prefabListIsLeaky = config("List - Global setting", "Is Leaky", defaultValue: "", GetDescriptionSeparatedStrings("Comma-separated list of pieces that will not work as a roof. Set \"" + PiecePatches.GlobalPatches.allPiecesIdentifier + "\" identifier to apply for all pieces." +
                                                                                            "\nRoof -> Leaky transition will be applied immediately. " +
                                                                                            "\nRestart the game if you need Leaky -> Roof transition of updated pieces."));


            prefabListClipEverything.SettingChanged += (s, e) => PiecePatches.UpdatePiecesProperties();
            prefabListAllowedInDungeons.SettingChanged += (s, e) => PiecePatches.UpdatePiecesProperties();
            prefabListRepairPiece.SettingChanged += (s, e) => PiecePatches.UpdatePiecesProperties();
            prefabListCanBeRemoved.SettingChanged += (s, e) => PiecePatches.UpdatePiecesProperties();
            prefabListDisabled.SettingChanged += (s, e) => PiecePatches.UpdatePiecesProperties();
            prefabListIsRoof.SettingChanged += (s, e) => PiecePatches.UpdatePiecesProperties();
            prefabListIsLeaky.SettingChanged += (s, e) => PiecePatches.UpdatePiecesProperties();

            prefabListAshDamageImmune = config("List - Immune to", "Ash and lava", defaultValue: "", GetDescriptionSeparatedStrings("Comma-separated list of pieces that will be immune to ash and lava damage. Set \"" + PiecePatches.GlobalPatches.allPiecesIdentifier + "\" identifier to apply for all pieces."));
            prefabListNoRoofWear = config("List - Immune to", "Water damage", defaultValue: "", GetDescriptionSeparatedStrings("Comma-separated list of pieces that will be immune to water damage. Set \"" + PiecePatches.GlobalPatches.allPiecesIdentifier + "\" identifier to apply for all pieces."));
            prefabListNoSupportWear = config("List - Immune to", "Structural integrity", defaultValue: "", GetDescriptionSeparatedStrings("Comma-separated list of pieces that will not be needed support. Set \"" + PiecePatches.GlobalPatches.allPiecesIdentifier + "\" identifier to apply for all pieces."));

            prefabListAshDamageImmune.SettingChanged += (s, e) => PiecePatches.UpdatePiecesProperties();
            prefabListNoRoofWear.SettingChanged += (s, e) => PiecePatches.UpdatePiecesProperties();
            prefabListNoSupportWear.SettingChanged += (s, e) => PiecePatches.UpdatePiecesProperties();

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
            if (fileSystemWatcherPlugin == null)
            {
                fileSystemWatcherPlugin = new FileSystemWatcher(pluginDirectory.FullName, "*.*");
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
                    fileSystemWatcherConfig = new FileSystemWatcher(configDirectory.FullName, "*.*");
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
            Dictionary<string, string> localConfigsJSON = new Dictionary<string, string>();

            List<FileInfo> configFiles = new List<FileInfo>();

            if (pluginDirectory.Exists)
            {
                configFiles.AddRange(pluginDirectory.GetFiles("*.json", SearchOption.AllDirectories));
                configFiles.AddRange(pluginDirectory.GetFiles("*.yaml", SearchOption.AllDirectories));
                configFiles.AddRange(pluginDirectory.GetFiles("*.yml", SearchOption.AllDirectories));
            }

            if (configDirectory.Exists)
            {
                configFiles.AddRange(configDirectory.GetFiles("*.json", SearchOption.AllDirectories));
                configFiles.AddRange(configDirectory.GetFiles("*.yaml", SearchOption.AllDirectories));
                configFiles.AddRange(configDirectory.GetFiles("*.yml", SearchOption.AllDirectories));
            }

            foreach (FileInfo file in configFiles)
            {
                if (file.Name == "manifest.json")
                    continue;

                LogInfo($"Found {file.FullName}");

                try
                {
                    using FileStream fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using StreamReader reader = new StreamReader(fs);
                    string content = reader.ReadToEnd();
                    reader.Close();
                    fs.Dispose();

                    string filename = Path.GetFileNameWithoutExtension(file.Name);

                    if (filename == "Piece categories")
                    {
                        Dictionary<int, List<string>> categories = (file.Extension != ".json" ? 
                                                            YamlDeserializer.Deserialize<Dictionary<int, List<string>>>(content) : 
                                                            JsonConvert.DeserializeObject<Dictionary<int, List<string>>>(content));

                        pieceCategories.AssignLocalValue(categories.SelectMany(kv => kv.Value.Select(v => new { Key = v, Value = kv.Key })).ToDictionary(x => x.Key.ToLower(), x => x.Value));
                    }
                    else
                    {
                        if (file.Extension != ".json")
                            content = JsonConvert.SerializeObject(YamlDeserializer.Deserialize<CustomPieceData>(content));

                        localConfigsJSON.Add(filename, content);
                    }
                }
                catch (Exception e)
                {
                    LogWarning($"Error reading file ({file.FullName})! Error: {e.Message}");
                }
            }

            configsJSON.AssignLocalValue(localConfigsJSON);
        }

        private static void LoadConfigs()
        {
            pieceData.Clear();

            foreach (KeyValuePair<string, string> configJSON in configsJSON.Value)
            {
                try
                {
                    pieceData.Add(configJSON.Key, JsonConvert.DeserializeObject<CustomPieceData>(configJSON.Value));
                }
                catch (Exception e)
                {
                    LogWarning($"Error parsing item ({configJSON.Key})! Error: {e.Message}");
                }
            }

            PiecePatches.UpdatePiecesProperties();
        }

        internal static readonly Dictionary<Piece, WearNTear> piecesWntComponent = new Dictionary<Piece, WearNTear>();

        internal static WearNTear GetWearNTearComponent(Piece piece)
        {
            if (piecesWntComponent.TryGetValue(piece, out WearNTear wnt))
                return wnt;

            WearNTear component = piece.GetComponent<WearNTear>();

            piecesWntComponent[piece] = component;

            return component;
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy))]
        private static class ZoneSystem_OnDestroy_PiecesWntCache
        {
            private static void Postfix()
            {
                if (!modEnabled.Value)
                    return;

                piecesWntComponent.Clear();
            }
        }

        [HarmonyPatch(typeof(Piece), nameof(Piece.Awake))]
        private static class Piece_Awake_PiecesWntCache
        {
            private static void Prefix(Piece __instance)
            {
                if (!modEnabled.Value)
                    return;

                piecesWntComponent[__instance] = __instance.GetComponent<WearNTear>();
            }
        }

        [HarmonyPatch(typeof(Piece), nameof(Piece.OnDestroy))]
        private static class Piece_OnDestroy_PiecesWntCache
        {
            private static void Prefix(Piece __instance)
            {
                if (!modEnabled.Value)
                    return;

                piecesWntComponent.Remove(__instance);
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        private static class ZoneSystem_Start_PiecesConfigWatcher
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix()
            {
                if (!modEnabled.Value)
                    return;

                SetupConfigWatcher();
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
