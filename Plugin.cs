using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HelheimHarmonizer.Patches;
using HelheimHarmonizer.Util;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;
using UnityEngine;

namespace HelheimHarmonizer
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class HelheimHarmonizerPlugin : BaseUnityPlugin
    {
        internal const string ModName = "HelheimHarmonizer";
        internal const string ModVersion = "1.0.3";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource HelheimHarmonizerLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new(ModGUID)
            { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
        
        internal static Dictionary<string, object> yamlData;
        internal static Dictionary<string, HashSet<string>> groups;
        internal static readonly CustomSyncedValue<string> HelheimHarmonizerData = new(ConfigSync, "HelheimHarmonizerData", "");
        internal static readonly string yamlFileName = $"{Author}.{ModName}.yml";
        internal static readonly string yamlPath = Paths.ConfigPath + Path.DirectorySeparatorChar + yamlFileName;
        
        internal static Assembly quickSlotsAssembly;

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
                "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);
            
            totalPinRemoval = config("1 - Pin Control", "TotalPinRemoval", Toggle.Off, "If on, no death pin will be created when the player dies.");
            removeOnEmpty = config("1 - Pin Control", "RemovePinOnTombstoneInteract", Toggle.On, "If on, the death pin for the tombstone will be removed when the tombstone is fully looted.");
            
            noItemLoss = config("2 - Death Control", "NoItemLossOnDeath", Toggle.Off, "If on, you will not lose any items on death even if it's set to drop via the yml configuration.");
            keepEquipped = config("2 - Death Control", "KeepEquippedOnDeath", Toggle.Off, "If on, you will not lose any of your equipped items on death even if it's set to drop via the yml configuration.");
            createDeathEffects = config("2 - Death Control", "CreateDeathEffects", Toggle.On, "Toggle death effects when the player dies.");
            //createTombstone = config("2 - Death Control", "CreateTombstone", Toggle.On, "Toggle tombstone creation when the player dies.");
            clearFoods = config("2 - Death Control", "ClearFoods", Toggle.On, "Toggle clearing the player's food list when the player dies.");
            reduceSkills = config("2 - Death Control", "ReduceSkills", Toggle.On, "Toggle skill reduction when the player dies.");
            skillReduceFactor = config("2 - Death Control", "SkillReduceFactor", 0.25f, "The factor to reduce the player's skills by when ReduceSkills is on. 0.25 is vanilla.");
            
            spawnAtStart = config("3 - Spawn Control", "SpawnAtStart", Toggle.Off, "Toggle spawning at the start location when the player dies.");
            useFixedSpawnCoordinates = config("3 - Spawn Control", "UseFixedSpawnCoordinates", Toggle.Off, "If on, the player will spawn at the fixed spawn coordinates.");
            fixedSpawnCoordinates = config("3 - Spawn Control", "FixedSpawnCoordinates", new Vector3(0,0,0), "The fixed spawn coordinates to use when UseFixedSpawnCoordinates is on.");
            
            
            if (!File.Exists(yamlPath))
            {
                Functions.WriteConfigFileFromResource(yamlPath);
            }
            
            
            HelheimHarmonizerData.ValueChanged += OnValChangedUpdate; // check for file changes
            HelheimHarmonizerData.AssignLocalValue(Functions.ReadYamlFile());
            
            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void Start()
        {
            AutoDoc();
        }

        private void AutoDoc()
        {
#if DEBUG

            // Store Regex to get all characters after a [
            Regex regex = new(@"\[(.*?)\]");

            // Strip using the regex above from Config[x].Description.Description
            string Strip(string x) => regex.Match(x).Groups[1].Value;
            StringBuilder sb = new();
            string lastSection = "";
            foreach (ConfigDefinition x in Config.Keys)
            {
                // skip first line
                if (x.Section != lastSection)
                {
                    lastSection = x.Section;
                    sb.Append($"{Environment.NewLine}`{x.Section}`{Environment.NewLine}");
                }

                sb.Append($"\n{x.Key} [{Strip(Config[x].Description.Description)}]" +
                          $"{Environment.NewLine}   * {Config[x].Description.Description.Replace("[Synced with Server]", "").Replace("[Not Synced with Server]", "")}" +
                          $"{Environment.NewLine}     * Default Value: {Config[x].GetSerializedValue()}{Environment.NewLine}");
            }

            File.WriteAllText(
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, $"{ModName}_AutoDoc.md"),
                sb.ToString());
#endif
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
            
            FileSystemWatcher yamlwatcher = new(Paths.ConfigPath, yamlFileName);
            yamlwatcher.Changed += ReadYamlFiles;
            yamlwatcher.Created += ReadYamlFiles;
            yamlwatcher.Renamed += ReadYamlFiles;
            yamlwatcher.IncludeSubdirectories = true;
            yamlwatcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            yamlwatcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                HelheimHarmonizerLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                HelheimHarmonizerLogger.LogError($"There was an issue loading your {ConfigFileName}");
                HelheimHarmonizerLogger.LogError("Please check your config entries for spelling and format!");
            }
        }
        
        private void ReadYamlFiles(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(yamlPath)) return;
            try
            {
                HelheimHarmonizerLogger.LogDebug("ReadConfigValues called");
                HelheimHarmonizerData.AssignLocalValue(Functions.ReadYamlFile());
            }
            catch
            {
                HelheimHarmonizerLogger.LogError($"There was an issue loading your {yamlFileName}");
                HelheimHarmonizerLogger.LogError("Please check your entries for spelling and format!");
            }
        }
        
        
        
        private static void OnValChangedUpdate()
        {
            HelheimHarmonizerLogger.LogDebug("OnValChanged called");
            try
            {
                YamlUtils.ReadYaml(HelheimHarmonizerData.Value);
                YamlUtils.ParseGroups();
            }
            catch (Exception e)
            {
                HelheimHarmonizerLogger.LogError($"Failed to deserialize {yamlFileName}: {e}");
            }
        }


        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        public static ConfigEntry<Toggle> totalPinRemoval = null!;
        public static ConfigEntry<Toggle> removeOnEmpty = null!;
        public static ConfigEntry<Toggle> noItemLoss = null!;
        public static ConfigEntry<Toggle> keepEquipped = null!;
        
        public static ConfigEntry<Toggle> createDeathEffects;
        public static ConfigEntry<Toggle> createTombstone;
        public static ConfigEntry<Toggle> clearFoods;
        public static ConfigEntry<Toggle> reduceSkills;
        public static ConfigEntry<float> skillReduceFactor;
        
        public static ConfigEntry<Vector3> fixedSpawnCoordinates;
        public static ConfigEntry<Toggle> useFixedSpawnCoordinates;
        public static ConfigEntry<Toggle> spawnAtStart;


        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }

        #endregion
    }
}