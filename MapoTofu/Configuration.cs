using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace MapoTofu;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public class SlideConfigEntry(bool Enabled, string Title, int Index, bool IsFolder) {
        public bool Enabled = Enabled;
        public string Title = Title;
        public int Index = Index;
        public bool IsFolder = IsFolder;
    };

    public Dictionary<ushort, SlideConfigEntry> TerritoryInitialStrategy = [];

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
