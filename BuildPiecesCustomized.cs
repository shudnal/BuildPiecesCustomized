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

namespace BuildPiecesCustomized
{
    [BepInPlugin(pluginID, pluginName, pluginVersion)]
    [BepInIncompatibility("aedenthorn.BuildPieceTweaks")]
    [BepInIncompatibility("TheSxW_EditMaterialProperties")]
    internal class BuildPiecesCustomized : BaseUnityPlugin
    {
        const string pluginID = "shudnal.BuildPiecesCustomized";
        const string pluginName = "Build Pieces Customized";
        const string pluginVersion = "1.0.0";

        private readonly Harmony harmony = new Harmony(pluginID);

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        internal static ConfigEntry<bool> modEnabled;
        internal static ConfigEntry<bool> configLocked;
        internal static ConfigEntry<bool> loggingEnabled;

        internal static ConfigEntry<string> toolsToPatchPieces;

        internal static ConfigEntry<bool> globalClipEverything;
        internal static ConfigEntry<bool> globalAllowedInDungeons;
        internal static ConfigEntry<bool> globalRepairPiece;
        internal static ConfigEntry<bool> globalCanBeRemoved;

        internal static ConfigEntry<bool> globalAshDamageImmune;
        internal static ConfigEntry<bool> globalNoRoofWear;
        internal static ConfigEntry<bool> globalNoSupportWear;

        internal static BuildPiecesCustomized instance;

        private static readonly CustomSyncedValue<Dictionary<string, string>> configsJSON = new CustomSyncedValue<Dictionary<string, string>>(configSync, "JSON configs", new Dictionary<string, string>());

        internal static readonly Dictionary<string, CustomPieceData> pieceData = new Dictionary<string, CustomPieceData>();
        internal static readonly Dictionary<string, CraftingStation> craftingStations = new Dictionary<string, CraftingStation>();

        private static DirectoryInfo pluginDirectory;
        private static DirectoryInfo configDirectory;

        private static FileSystemWatcher fileSystemWatcherPlugin;
        private static FileSystemWatcher fileSystemWatcherConfig;

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
            if (loggingEnabled.Value)
                instance.Logger.LogWarning(data);
        }

        public void ConfigInit()
        {
            config("General", "NexusID", 2782, "Nexus mod ID for updates", false);

            modEnabled = config("General", "Enabled", defaultValue: true, "Enable the mod.");
            configLocked = config("General", "Lock Configuration", defaultValue: true, "Configuration is locked and can be changed by server admins only.");
            loggingEnabled = config("General", "Logging enabled", defaultValue: false, "Enable logging. [Not Synced with Server]", false);

            toolsToPatchPieces = config("General", "Tools list", defaultValue: "Hammer,Hoe", "Comma separated list of tool prefab name");

            globalClipEverything = config("Global", "Clip everything", defaultValue: false, "All pieces will clip through each other");
            globalAllowedInDungeons = Config.Bind("Global", "Allow in dungeons", defaultValue: false, "All pieces will be allowed to build in dungeons.");
            globalRepairPiece = Config.Bind("Global", "Can be repaired", defaultValue: false, "All pieces will be repairable.");
            globalCanBeRemoved = Config.Bind("Global", "Can be removed", defaultValue: false, "All pieces will be removeable.");
            
            globalAshDamageImmune = Config.Bind("Immunity", "Ash and lava", defaultValue: false, "All pieces will be immune to ash and lava damage.");
            globalNoRoofWear = Config.Bind("Immunity", "Water damage", defaultValue: false, "All pieces will be immune to water damage.");
            globalNoSupportWear = Config.Bind("Immunity", "Structural integrity", defaultValue: false, "All pieces will be immune to structural damage.");

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
            new Terminal.ConsoleCommand("bpcsave", "[Prefab name] - save piece data into JSON file next to mod", delegate (Terminal.ConsoleEventArgs args)
            {
                if (args.Length >= 2)
                {
                    string prefabName = args.FullLine.Substring(args[0].Length + 1);
                    CustomPieceData pieceData = CustomPieceData.GetByPieceName(prefabName);
                    if (pieceData == null)
                        args.Context?.AddString($"Piece with name {prefabName} was not found");
                    else
                    {
                        pieceData.SaveToDirectory(pluginDirectory.FullName);
                        args.Context?.AddString($"Saved {prefabName} file to plugin directory");
                    }
                }
            }, isCheat: false, isNetwork: false, onlyServer: false, isSecret: false, allowInDevBuild: false, () => CustomPieceData.GetBuildPieces().Select(piece => piece.name).ToList(), alwaysRefreshTabOptions: true, remoteCommand: false);
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
                configFiles.AddRangeToArray(configDirectory.GetFiles("*.json", SearchOption.AllDirectories));

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
        }

        private static void PatchPiece(Piece piece)
        {
            string name = Utils.GetPrefabName(piece.gameObject);
            if (pieceData.ContainsKey(name))
            {
                LogInfo($"Patching {name}");
                pieceData[name].PatchPiece(piece);
            }

            if (globalClipEverything.Value)
                piece.m_clipEverything = true;

            if (globalAllowedInDungeons.Value)
                piece.m_allowedInDungeons = true;

            if (globalRepairPiece.Value)
                piece.m_repairPiece = true;

            if (globalCanBeRemoved.Value)
                piece.m_canBeRemoved = true;

            if ((globalAshDamageImmune.Value || globalNoRoofWear.Value || globalNoSupportWear.Value) && piece.TryGetComponent(out WearNTear wnt))
            {
                if (globalAshDamageImmune.Value)
                    wnt.m_ashDamageImmune = true;

                if (globalNoRoofWear.Value)
                    wnt.m_noRoofWear = false;

                if (globalNoSupportWear.Value)
                    wnt.m_noSupportWear = false;
            }
        }

        private static void PatchPieces()
        {
            if (!(bool)ObjectDB.instance)
                return;

            SetupConfigWatcher();

            FillCraftingStations();

            foreach (GameObject go in CustomPieceData.GetBuildPieces())
                if (go.TryGetComponent(out Piece piece))
                    PatchPiece(piece);
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static class CopyOtherDB_Patch
        {
            private static void Postfix()
            {
                if (!modEnabled.Value)
                    return;

                //PatchPieces();
            }
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        [HarmonyPriority(Priority.Last)]
        private static class ZNetScene_Awake_Patch
        {
            private static void Postfix()
            {
                if (!modEnabled.Value)
                    return;

                PatchPieces();
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
    }
}