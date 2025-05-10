using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
namespace REPODebbysCase.Config
{
    public class DebbysCaseConfig
    {
        readonly BepInEx.Logging.ManualLogSource log = DebbysCase.instance.log;
        internal DebbysCaseConfig(ConfigFile cfg)
        {
            cfg.SaveOnConfigSet = false;
            //for (int i = 0; i < valList.Count; i++)
            //{
            //    isValEnabled.Add(cfg.Bind("Valuables", $"Enable {valList[i].name}?", true));
            //    log.LogDebug($"Added config for {valList[i].name}");
            //}
            ClearOrphanedEntries(cfg);
            cfg.Save();
            cfg.SaveOnConfigSet = true;
        }
        private static void ClearOrphanedEntries(ConfigFile cfg)
        {
            PropertyInfo orphanedEntriesProp = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries");
            Dictionary<ConfigDefinition, string> orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp.GetValue(cfg);
            orphanedEntries.Clear();
        }
    }
}