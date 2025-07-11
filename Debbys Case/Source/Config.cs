﻿using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
namespace REPODebbysCase.Config
{
    public class DebbysCaseConfig
    {
        readonly BepInEx.Logging.ManualLogSource log = DebbysCase.instance.log;
        internal readonly List<ConfigEntry<bool>> isItemEnabled = new List<ConfigEntry<bool>>();
        internal readonly List<ConfigEntry<bool>> isEnemyEnabled = new List<ConfigEntry<bool>>();
        public DebbysCaseConfig(ConfigFile cfg, List<EnemySetup> enemiesList, List<Item> itemsList)
        {
            cfg.SaveOnConfigSet = false;
            for (int i = 0; i < itemsList.Count; i++)
            {
                isItemEnabled.Add(cfg.Bind("Items", $"Enable {itemsList[i].name}?", true));
                log.LogDebug($"Added config for {itemsList[i].name}");
            }
            for (int i = 0; i < enemiesList.Count; i++)
            {
                isEnemyEnabled.Add(cfg.Bind("Enemies", $"Enable {enemiesList[i].name}?", true));
                log.LogDebug($"Added config for {enemiesList[i].name}");
            }
            PropertyInfo orphanedEntriesProp = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries");
            Dictionary<ConfigDefinition, string> orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp.GetValue(cfg);
            orphanedEntries.Clear();
            cfg.Save();
            cfg.SaveOnConfigSet = true;
        }
    }
}