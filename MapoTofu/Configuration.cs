using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace MapoTofu;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public Dictionary<int, List<Common.TriggerEntry>> StrategyBoardTriggerOptions = [];
    public uint MaxHistoryEntries = 30;
    public bool CheckOnPluginLoad = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
