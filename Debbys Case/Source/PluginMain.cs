using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using System;
using System.Reflection;
using System.IO;
using REPODebbysCase.Config;
using HarmonyLib;
using REPOLib.Modules;
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
            HandleContent();
            DoPatches();
            log.LogInfo($"{modName} Successfully Loaded");
        }
        public void PropogateLists()
        {
            AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "wildcardmod"));
            string[] allAssetPaths = bundle.GetAllAssetNames();
            for (int i = 0; i < allAssetPaths.Length; i++)
            {
                string assetPath = allAssetPaths[i][..allAssetPaths[i].LastIndexOf("/")];
                switch (assetPath)
                {
                    case "assets/Debbys Case/resources/valuables":
                        {
                            break;
                        }
                    case "assets/Debbys Case/resources/items":
                        {
                            break;
                        }
                    case "assets/Debbys Case/resources/enemies":
                        {
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
            ModConfig = new DebbysCaseConfig(base.Config);
            //HandleItems();
        }
        //public void HandleItems()
        //{
        //    for (int i = 0; i < itemList.Count; i++)
        //    {
        //        if (i >= ModConfig.isItemEnabled.Count || ModConfig.isItemEnabled[i].Value)
        //        {
        //            Items.RegisterItem(itemList[i]);
        //            log.LogDebug($"{itemList[i].name} item was loaded!");
        //        }
        //        else
        //        {
        //            log.LogInfo($"{itemList[i].name} item was disabled!");
        //        }
        //    }
        //}
        public void DoPatches()
        {
            log.LogDebug("Patching Game");
        }
    }
}