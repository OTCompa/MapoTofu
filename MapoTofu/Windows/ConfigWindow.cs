using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static MapoTofu.Common;

namespace MapoTofu.Windows;

public partial class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly Plugin plugin;

    private readonly List<Strategy> strategyData = [];
    private readonly Dictionary<ushort, string> territoryLUT = [];

    private int selectedTerritory = -1;
    private int selectedEntry = -1;


    public ConfigWindow(Plugin plugin) : base("Mapo Tofu")
    {
        this.plugin = plugin;
        var constraint = new WindowSizeConstraints();
        constraint.MinimumSize =  new Vector2(600, 250);
        SizeConstraints = constraint;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawTriggersTab();
    }
    
    private void DrawTriggersTab()
    {
        DrawSelectionPane();
        ImGui.SameLine();
        DrawOptionPane();
    }
}
