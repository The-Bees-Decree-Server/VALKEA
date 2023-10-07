using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ItemManager;
using ServerSync;
using UnityEngine;

namespace VALKEA
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class  VALKEAplugin: BaseUnityPlugin
    {
        internal const string ModName = "VALKEA";
        internal const string ModVersion = "2.1.8";
        internal const string Author = "TheBeesDecree";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

        internal static string ConnectionError = "";

        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource VALKEALogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static GameObject[]? myobjects;
        
        private static readonly ConfigSync ConfigSync = new(ModGUID)
            { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        public void Awake()
        {
            _serverConfigLocked = config("General", "Force Server Config", true, "Force Server Config");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            Item bjorksnas = new("valkea", "BJORKSNAS");
            bjorksnas.Name.English("BJORKSNAS"); // You can use this to fix the display name in code
            bjorksnas.Description.English("Legend has it that BJÖRKSNÄS is the holy key needed to assemble all goods from VALKEA");
            bjorksnas.Name.German("BJORKSNAS"); // Or add translations for other languages
            bjorksnas.Description.German("Das BJORKSNAS.");
          
         
          
            bjorksnas.Crafting.Add(CraftingTable.Workbench,
                1); // Custom crafting stations can be specified as a string
            bjorksnas.RequiredItems.Add("FineWood", 1);
            bjorksnas.RequiredItems.Add("QueenBee", 1);
            bjorksnas.RequiredItems.Add("Coins", 999);
            bjorksnas.RequiredUpgradeItems
                .Add("Coins", 99); // 10 Silver: You need 10 silver for level 2, 20 silver for level 3, 30 silver for level 4
            bjorksnas.CraftAmount = 1; // We really want to dual wield these
            
            AssetBundle bundle = ItemManager.PrefabManager.RegisterAssetBundle("valkea");
            myobjects = bundle.LoadAllAssets<GameObject>();

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
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
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                VALKEALogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                VALKEALogger.LogError($"There was an issue loading your {ConfigFileName}");
                VALKEALogger.LogError("Please check your config entries for spelling and format!");
            }
        }
        public static void TryRegisterFabs(ZNetScene zNetScene)
        {
            List<GameObject>? piecetable = null;
            if (zNetScene == null || zNetScene.m_prefabs is not { Count: > 0 }) return;
            if (myobjects != null)
                foreach (GameObject myobject in myobjects)
                {
                    if (myobject.name.Contains("BJORKSNAS"))
                    {
                        if (myobject.GetComponent<ItemDrop>())
                        {
                            piecetable = myobject.GetComponent<ItemDrop>().m_itemData.m_shared.m_buildPieces.m_pieces;
                        }
                    }

                    if (piecetable == null) continue;
                    foreach (GameObject o in piecetable)
                    {
                      ///VALKEALogger.LogWarning($"itemloaded: {o.name}");
                        if (zNetScene.m_prefabs.Contains(o)) return;
                        zNetScene.m_prefabs.Add(o);
                      ///VALKEALogger.LogWarning($"PREFABADDED: {o.name}");
                    }
                }
        }

        /*[HarmonyPatch(typeof(ZNetScene),nameof(ZNetScene.Awake))]
        [HarmonyPrefix]
        private static void hammerfix(ZNetScene __instance)
        {
           
     

            TryRegisterFabs(__instance);
        }*/
        
        #region ConfigOptions

        private static ConfigEntry<bool>? _serverConfigLocked;

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
            public bool? Browsable = false;
        }

        #endregion
    }
}