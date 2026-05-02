using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using static MapoTofu.Common;

namespace MapoTofu.Windows;

public partial class ConfigWindow
{
    private bool newPlace = false;
    private int placeToDelete = -1;
    private int triggerToDelete = -1;

    private void DrawSelectionPane()
    {
        using (var c = ImRaii.Child("SelectionPane", new Vector2(350, 0), true))
        {
            var n = 0;
            foreach (var placeId in configuration.StrategyBoardTriggerOptions)
            {
                using var _ = ImRaii.PushId(n);
                var name = placeId.Value[0].Migrated ? GetCFCName((ushort)placeId.Key) : GetTerritoryName((ushort)placeId.Key);
                using var tree = ImRaii.TreeNode($"{name} ({placeId.Key})");
                DrawContextMenuPlace(placeId.Key);
                if (tree)
                {
                    for (var i = 0; i < placeId.Value.Count; i++)
                    {
                        using var __ = ImRaii.PushId(i);
                        var selectedLoop = selectedPlace == placeId.Key && selectedEntry == i;
                        if (ImGui.Selectable($"{placeId.Value[i].Label}", selectedLoop))
                        {
                            OnSelect(placeId.Value[i]);
                            selectedPlace = (int)placeId.Key;
                            selectedEntry = i;
                        }
                        DrawContextMenuTrigger((uint)i);
                    }

                    if (triggerToDelete > -1)
                    {
                        if (selectedPlace == placeId.Key && selectedEntry == triggerToDelete) selectedEntry = -1;
                        placeId.Value.RemoveAt(triggerToDelete);
                        configuration.Save();
                        triggerToDelete = -1;
                    }

                    var selected = selectedPlace == placeId.Key && selectedEntry == -1;
                    if (ImGui.Selectable("Add trigger", selected))
                    {
                        selectedPlace = (int)placeId.Key;
                        selectedEntry = -1;
                        OnSelect(new());
                    }
                }
            }

            if (!newPlace)
            {
                if (ImGui.Selectable("Add Content Finder ID"))
                {
                    placeInput = Plugin.DutyState.ContentFinderCondition.RowId;
                    newPlace = true;
                }
            }
            else
            {
                ImGui.SetNextItemWidth(100);
                ImGui.InputUInt("Content Finder ID", ref placeInput);
                ImGui.SameLine();
                if (ImGui.Button("Add"))
                {
                    configuration.StrategyBoardTriggerOptions[placeInput] = [];
                    newPlace = false;
                    configuration.Save();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    newPlace = false;
                }
            }

            if (placeToDelete > -1)
            {
                configuration.StrategyBoardTriggerOptions.Remove((uint)placeToDelete);
                placeToDelete = -1;
                configuration.Save();
            }
            n++;
        }
    }

    private void DrawContextMenuTrigger(uint i)
    {
        using var c = ImRaii.ContextPopupItem($"TriggerContextMenu###{i}");
        if (!c.Success) return;
        if (ImGui.MenuItem($"Delete###Trigger{i}"))
        {
            triggerToDelete = (int)i;
        }
    }
    private void DrawContextMenuPlace(uint i)
    {
        using var c = ImRaii.ContextPopupItem($"TerritoryContextMenu###{i}");
        if (!c.Success) return;
        if (ImGui.MenuItem($"Delete###Territory{i}"))
        {
            placeToDelete = (int)i;
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
        weatherSettingInput = triggerEntry.WeatherSetting;
        isInterruptibleInput = triggerEntry.IsInterruptable;

        timeInput = 0;
        pendingChanges = false;
    }

    private static string GetTerritoryName(uint territory) => Plugin.DataManager.GetExcelSheet<TerritoryType>().GetRow(territory).PlaceName.Value.Name.ToString();
    private static string GetCFCName(uint cfc) => Plugin.DataManager.GetExcelSheet<ContentFinderCondition>().GetRow(cfc).Name.ExtractText();
    private static string GetWeatherName(uint weather) => Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Weather>().GetRow(weather).Name.ToString();
}
