using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using System;
using System.Reflection;
using System.IO;
using REPODebbysCase.Config;
using HarmonyLib;
using System.Collections.Generic;
namespace REPODebbysCase
{
    [BepInPlugin(modGUID, modName, modVersion)]
    [BepInDependency(REPOLib.MyPluginInfo.PLUGIN_GUID, BepInDependency.DependencyFlags.HardDependency)]
    public class DebbysCase : BaseUnityPlugin
    {
        internal const string modGUID = "deB.DebbysCase";
        internal const string modName = "Debbys Case";
        internal const string modVersion = "0.0.1";
        readonly Harmony harmony = new Harmony(modGUID);
        internal ManualLogSource log = null!;
        public static DebbysCase instance;
        internal DebbysCaseConfig ModConfig { get; private set; } = null!;
        public List<EnemySetup> enemiesList = new List<EnemySetup>();
        public void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
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
            ModConfig = new DebbysCaseConfig(base.Config, enemiesList);
            HandleContent();
            //DoPatches();
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
                    //case "assets/debbys case/resources/valuables":
                    //    {
                    //        break;
                    //    }
                    //case "assets/debbys case/resources/items":
                    //    {
                    //        break;
                    //    }
                    case "assets/debbys case/resources/enemies":
                        {
                            enemiesList.Add(bundle.LoadAsset<EnemySetup>(allAssetPaths[i]));
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
            HandleEnemies();
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
        public void DoPatches()
        {
            log.LogDebug("Patching Game");
        }
    }
}