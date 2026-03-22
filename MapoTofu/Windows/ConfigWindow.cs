using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
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
        using var tb = ImRaii.TabBar("MPTFTabBar");
        using (var triggerTab = ImRaii.TabItem("Triggers"))
        {
            if (triggerTab) DrawTriggersTab(); 
        }
        using (var historyTab = ImRaii.TabItem("History"))
        {
            if (historyTab) DrawHistoryTab();
        }
        using (var configTab = ImRaii.TabItem("Config"))
        {
            if (configTab) DrawConfigTab();
        }
    }

    private void DrawHistoryTab()
    {
        var tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY;
        using var table = ImRaii.Table($"Table", 6, tableFlags);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 30);
        ImGui.TableSetupColumn("Timestamp", ImGuiTableColumnFlags.WidthFixed, 75);
        ImGui.TableSetupColumn("Time since last entry", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableSetupColumn("Combat", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn("Territory");
        ImGui.TableSetupColumn("Weather");
        ImGui.TableHeadersRow();
        if (table)
        {
            var i = 0;
            if (plugin.HistoryManager.HistoryEntries.Count == 0) { return; }
            foreach (var entry in plugin.HistoryManager.HistoryEntries.Reverse())
            {
                using var _ = ImRaii.PushId(i);

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.Text(i.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(entry.Timestamp.ToLongTimeString());
                ImGui.TableNextColumn();
                if (entry.MsSinceLastWeather > -1)
                {
                    ImGui.Text($"{entry.MsSinceLastWeather / 1000:d}s");
                }
                ImGui.TableNextColumn();
                using (var iconFont = ImRaii.PushFont(UiBuilder.IconFont))
                {
                    var icon = entry.InCombat ? FontAwesomeIcon.CheckSquare.ToIconString() : FontAwesomeIcon.SquareXmark.ToIconString();
                    using var basic = ImRaii.PushColor(ImGuiCol.Text, entry.InCombat ? new Vector4(0f, 1f, 0f, 1f) : new Vector4(1f, 0f, 0f, 1f));
                    ImGui.Text(icon);
                }
                ImGui.TableNextColumn();
                ImGui.TextWrapped($"{GetTerritoryName(entry.Territory)} ({entry.Territory})");
                ImGui.TableNextColumn();
                ImGui.TextWrapped($"{GetWeatherName(entry.Weather)} ({entry.Weather})");

                i++;
            }
        }
    }

    private void DrawConfigTab()
    {
        var changed = false;

        using (ImRaii.ItemWidth(100))
        {
            changed |= ImGui.Checkbox("Check triggers on plugin load", ref configuration.CheckOnPluginLoad);
            changed |= ImGui.InputUInt("Maximum history entries", ref configuration.MaxHistoryEntries);
        }

        if (changed)
        {
            configuration.Save();
        }
    }

    private void DrawTriggersTab()
    {
        DrawSelectionPane();
        ImGui.SameLine();
        DrawOptionPane();
    }
}
