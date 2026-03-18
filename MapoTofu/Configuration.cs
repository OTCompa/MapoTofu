using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace MapoTofu;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public Dictionary<ushort, Common.StrategyConfigEntry> TerritoryInitialStrategy = [];

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
