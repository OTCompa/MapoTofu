using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using static MapoTofu.Common;

namespace MapoTofu.Windows;

public partial class ConfigWindow
{
    private bool newTerritory = false;
    private int territoryToDelete = -1;
    private int triggerToDelete = -1;

    private void DrawSelectionPane()
    {
        using (var c = ImRaii.Child("SelectionPane", new Vector2(300, 0), true))
        {
            var n = 0;
            foreach (var territory in configuration.StrategyBoardTriggerOptions)
            {
                using var _ = ImRaii.PushId(n);
                using var tree = ImRaii.TreeNode($"{GetTerritoryName((ushort)territory.Key)} ({territory.Key})");
                DrawContextMenuTerritory(territory.Key);
                if (tree)
                {
                    for (var i = 0; i < territory.Value.Count; i++)
                    {
                        using var __ = ImRaii.PushId(i);
                        var selectedLoop = selectedTerritory == territory.Key && selectedEntry == i;
                        if (ImGui.Selectable($"{territory.Value[i].Label}", selectedLoop))
                        {
                            OnSelect(territory.Value[i]);
                            selectedTerritory = territory.Key;
                            selectedEntry = i;
                        }
                        DrawContextMenuTrigger(i);
                    }

                    if (triggerToDelete > -1)
                    {
                        if (selectedTerritory == territory.Key && selectedEntry == triggerToDelete) selectedEntry = -1;
                        territory.Value.RemoveAt(triggerToDelete);
                        configuration.Save();
                        triggerToDelete = -1;
                    }

                    var selected = selectedTerritory == territory.Key && selectedEntry == -1;
                    if (ImGui.Selectable("Add trigger", selected))
                    {
                        selectedTerritory = territory.Key;
                        selectedEntry = -1;
                        OnSelect(new());
                    }
                }
            }

            if (!newTerritory)
            {
                if (ImGui.Selectable("Add Territory"))
                {
                    territoryInput = Plugin.ClientState.TerritoryType;
                    newTerritory = true;
                }
            }
            else
            {
                ImGui.SetNextItemWidth(100);
                ImGui.InputInt("Territory", ref territoryInput);
                ImGui.SameLine();
                if (ImGui.Button("Add"))
                {
                    configuration.StrategyBoardTriggerOptions[territoryInput] = [];
                    newTerritory = false;
                    configuration.Save();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    newTerritory = false;
                }
            }

            if (territoryToDelete > -1)
            {
                configuration.StrategyBoardTriggerOptions.Remove(territoryToDelete);
                territoryToDelete = -1;
                configuration.Save();
            }
            n++;
        }
    }

    private void DrawContextMenuTrigger(int i)
    {
        using var c = ImRaii.ContextPopupItem($"TriggerContextMenu###{i}");
        if (!c.Success) return;
        if (ImGui.MenuItem($"Delete###Trigger{i}"))
        {
            triggerToDelete = i;
        }
    }
    private void DrawContextMenuTerritory(int i)
    {
        using var c = ImRaii.ContextPopupItem($"TerritoryContextMenu###{i}");
        if (!c.Success) return;
        if (ImGui.MenuItem($"Delete###Territory{i}"))
        {
            territoryToDelete = i;
        }
    }

    private void OnSelect(TriggerEntry triggerEntry)
    {
        enabledInput = triggerEntry.Enabled;
        labelInput = triggerEntry.Label;
        typeInput = triggerEntry.Type;
        newWeatherInput = triggerEntry.NewWeather;
        oldWeatherEnabledInput = triggerEntry.OldWeatherEnabled;
        oldWeatherInput = triggerEntry.OldWeatherId;
        boardsInput = new(triggerEntry.Boards);

        timeInput = 0;
        pendingChanges = false;
    }

    private string GetTerritoryName(ushort territory)
    {
        if (!territoryLUT.ContainsKey(territory))
        {
            territoryLUT[territory] = Plugin.DataManager.GetExcelSheet<TerritoryType>().GetRow(territory)
                .PlaceName.Value.Name.ToString() ?? "Unknown";
        }
        return territoryLUT[territory];
    }
}
