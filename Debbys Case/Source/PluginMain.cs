using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using System;
using System.Reflection;
using System.IO;
using REPODebbysCase.Config;
using System.Collections.Generic;
using REPOLib.Objects.Sdk;
using REPODebbysCase.Utilities;
namespace REPODebbysCase
{
    [BepInPlugin(modGUID, modName, modVersion)]
    [BepInDependency(REPOLib.MyPluginInfo.PLUGIN_GUID, BepInDependency.DependencyFlags.HardDependency)]
    public class DebbysCase : BaseUnityPlugin
    {
        internal const string modGUID = "deB.DebbysCase";
        internal const string modName = "Debbys Case";
        internal const string modVersion = "0.0.7";
        internal ManualLogSource log = null!;
        public static DebbysCase instance;
        internal DebbysCaseConfig ModConfig { get; private set; } = null!;
        public Utils utils;
        public List<EnemyContent> enemiesList = new List<EnemyContent>();
        public List<GameObject> itemsList = new List<GameObject>();
        public void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            utils = new Utils();
            log = Logger;
            Type[] assemblyTypes = Assembly.GetExecutingAssembly().GetTypes();
            for (int i = 0; i < assemblyTypes.Length; i++)
            {
                MethodInfo[] typeMethods = assemblyTypes[i].GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                for (int j = 0; j < typeMethods.Length; j++)
                {
                    object[] methodAttributes;
                    try
                    {
                        methodAttributes = typeMethods[j].GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    }
                    catch
                    {
                        continue;
                    }
                    if (methodAttributes.Length > 0)
                    {
                        typeMethods[j].Invoke(null, null);
                    }
                }
            }
            PropogateLists();
            ModConfig = new DebbysCaseConfig(base.Config, enemiesList, itemsList);
            HandleContent();
            log.LogInfo($"{modName} Successfully Loaded");
        }
        public void PropogateLists()
        {
            AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "debbyscase"));
            string[] allAssetPaths = bundle.GetAllAssetNames();
            for (int i = 0; i < allAssetPaths.Length; i++)
            {
                string assetPath = allAssetPaths[i][..allAssetPaths[i].LastIndexOf("/")];
                switch (assetPath)
                {
                    case "assets/debbys case/resources/items":
                        {
                            itemsList.Add(bundle.LoadAsset<GameObject>(allAssetPaths[i]));
                            break;
                        }
                    case "assets/debbys case/resources/enemies":
                        {
                            enemiesList.Add(bundle.LoadAsset<EnemyContent>(allAssetPaths[i]));
                            break;
                        }
                    default:
                        {
                            log.LogWarning($"\"{assetPath}\" is not a known asset path, skipping.");
                            break;
                        }
                }
            }
        }
        public void HandleContent()
        {
            HandleItems();
            HandleEnemies();
        }
        public void HandleItems()
        {
            for (int i = 0; i < itemsList.Count; i++)
            {
                if (ModConfig.isItemEnabled[i].Value)
                {
                    REPOLib.Modules.Items.RegisterItem(itemsList[i].GetComponent<ItemAttributes>());
                    log.LogDebug($"{itemsList[i].name} item was loaded!");
                }
                else
                {
                    log.LogInfo($"{itemsList[i].name} item was disabled!");
                }
            }
        }
        public void HandleEnemies()
        {
            for (int i = 0; i < enemiesList.Count; i++)
            {
                if (ModConfig.isEnemyEnabled[i].Value)
                {
                    REPOLib.Modules.Enemies.RegisterEnemy(enemiesList[i]);
                    log.LogDebug($"{enemiesList[i].name} enemy was loaded!");
                }
                else
                {
                    log.LogInfo($"{enemiesList[i].name} enemy was disabled!");
                }
            }
        }
    }
}