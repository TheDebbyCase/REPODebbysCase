using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
namespace REPODebbysCase.Config
{
    public class DebbysCaseConfig
    {
        readonly BepInEx.Logging.ManualLogSource log = DebbysCase.instance.log;
        public DebbysCaseConfig(ConfigFile cfg/*, List<GameObject> valList*/)
        {
            cfg.SaveOnConfigSet = false;
            //for (int i = 0; i < valList.Count; i++)
            //{
            //    isValEnabled.Add(cfg.Bind("Valuables", $"Enable {valList[i].name}?", true));
            //    log.LogDebug($"Added config for {valList[i].name}");
            //}
            PropertyInfo orphanedEntriesProp = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries");
            Dictionary<ConfigDefinition, string> orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp.GetValue(cfg);
            orphanedEntries.Clear();
            cfg.Save();
            cfg.SaveOnConfigSet = true;
        }
    }
}