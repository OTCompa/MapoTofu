using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using static MapoTofu.Common;
using MapoTofu.Structs;

namespace MapoTofu.Windows;

public partial class ConfigWindow
{
    private bool enabledInput = false;
    private string labelInput = "";
    private ConfigTriggerType typeInput = ConfigTriggerType.Timer;
    private int newWeatherInput = -1;
    private bool oldWeatherEnabledInput = false;
    private int oldWeatherInput = -1;
    private ConfigWeatherSetting weatherSettingInput = ConfigWeatherSetting.Any;
    private int timeInput = -1;
    private TriggerEntry triggerEntryInput = new();
    private int territoryInput = 0;
    public SortedDictionary<int, StrategyConfigEntry> boardsInput = [];
    private Strategy? selectedStrategy = null;
    private bool isInterruptibleInput = true;

    private bool pendingChanges = false;

    private void DrawOptionPane()
    {
        using (var c = ImRaii.Child("OptionPane", Vector2.Zero, true))
        {
            if (selectedTerritory != -1)
            {
                if (configuration.StrategyBoardTriggerOptions.ContainsKey(selectedTerritory))
                {
                    var territory = configuration.StrategyBoardTriggerOptions[selectedTerritory];
                    if (territory.Count > selectedEntry)
                    {
                        DrawTriggerEntry();
                    }
                }
            }
        }
    }

    private void DrawTriggerEntry()
    {
        DrawTriggerEntryFields();

        DrawTimerTable();

        DrawAddSaveButton();

        if (ImGui.Button("Refresh Strategy Boards"))
        {
            PopulateStrategyData();
        }
    }

    private void DrawTriggerEntryFields()
    {
        pendingChanges |= ImGui.Checkbox("Enabled###TriggerEnabled", ref enabledInput);
        ImGui.SameLine();
        pendingChanges |= ImGui.Checkbox("Interruptible###TriggerInterrupt", ref isInterruptibleInput);

        using (ImRaii.ItemWidth(250))
        {
            pendingChanges |= ImGui.InputText("Label", ref labelInput);
            using (var combo = ImRaii.Combo($"Type", typeInput.ToString()))
            {
                if (combo.Success)
                {
                    if (ImGui.Selectable("Timer"))
                    {
                        typeInput = ConfigTriggerType.Timer;
                        pendingChanges = true;
                    }
                    if (ImGui.Selectable("Weather"))
                    {
                        typeInput = ConfigTriggerType.Weather;
                        pendingChanges = true;
                    }
                }
            }
            using (ImRaii.Disabled(typeInput != ConfigTriggerType.Weather))
            {
                ComboWeatherSetting();
                var checkboxSize = ImGui.GetTextLineHeight() + ImGui.GetStyle().FramePadding.Y * 2;
                pendingChanges |= ImGui.InputInt("New Weather Id", ref newWeatherInput);
                pendingChanges |= ImGui.Checkbox("###OldWeatherEnabled", ref oldWeatherEnabledInput);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.CalcItemWidth() - checkboxSize - ImGui.GetStyle().ItemSpacing.X);
                using (ImRaii.Disabled(!oldWeatherEnabledInput))
                {
                    pendingChanges |= ImGui.InputInt("Old Weather Id", ref oldWeatherInput);
                }
            }
        }
    }

    private void DrawAddSaveButton()
    {
        using var _ = ImRaii.Disabled(!pendingChanges);
        var buttonText = selectedEntry == -1 ? "Add" : "Save";
        if (ImGui.Button(buttonText))
        {
            if (!string.IsNullOrEmpty(labelInput))
            {

                if (selectedEntry == -1)
                {
                    triggerEntryInput.Enabled = enabledInput;
                    triggerEntryInput.Label = labelInput;
                    triggerEntryInput.Type = typeInput;
                    triggerEntryInput.NewWeather = newWeatherInput;
                    triggerEntryInput.OldWeatherEnabled = oldWeatherEnabledInput;
                    triggerEntryInput.OldWeatherId = oldWeatherInput;
                    triggerEntryInput.Boards = boardsInput;
                    triggerEntryInput.WeatherSetting = weatherSettingInput;
                    triggerEntryInput.IsInterruptable = isInterruptibleInput;
                    configuration.StrategyBoardTriggerOptions[selectedTerritory].Add(new(triggerEntryInput));
                    selectedEntry = configuration.StrategyBoardTriggerOptions[selectedTerritory].Count - 1;
                }
                else
                {
                    configuration.StrategyBoardTriggerOptions[selectedTerritory][selectedEntry].Enabled = enabledInput;
                    configuration.StrategyBoardTriggerOptions[selectedTerritory][selectedEntry].Label = labelInput;
                    configuration.StrategyBoardTriggerOptions[selectedTerritory][selectedEntry].Type = typeInput;
                    configuration.StrategyBoardTriggerOptions[selectedTerritory][selectedEntry].NewWeather = newWeatherInput;
                    configuration.StrategyBoardTriggerOptions[selectedTerritory][selectedEntry].OldWeatherEnabled = oldWeatherEnabledInput;
                    configuration.StrategyBoardTriggerOptions[selectedTerritory][selectedEntry].OldWeatherId = oldWeatherInput;
                    configuration.StrategyBoardTriggerOptions[selectedTerritory][selectedEntry].Boards = boardsInput;
                    configuration.StrategyBoardTriggerOptions[selectedTerritory][selectedEntry].WeatherSetting = weatherSettingInput;
                    configuration.StrategyBoardTriggerOptions[selectedTerritory][selectedEntry].IsInterruptable = isInterruptibleInput;
                }

                configuration.Save();
                if (selectedTerritory == Plugin.ClientState.TerritoryType)
                {
                    plugin.ActiveStrategyManager.SearchAndRunInitState();
                }

                pendingChanges = false;
            }
        }
    }

    private void DrawTimerTable()
    {
        int? toRemove = null;

        var tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY;
        var outerSize = new Vector2(400, 150);
        using var table = ImRaii.Table($"Table", 4, tableFlags, outerSize);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 23);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("Strategy");
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 23);
        ImGui.TableHeadersRow();
        if (table)
        {
            var i = 0;
            foreach (var key in boardsInput.Keys)
            {
                using var id = ImRaii.PushId(i);
                var timeEntry = boardsInput[key];
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                pendingChanges |= ImGui.Checkbox($"###Enabled", ref timeEntry.Enabled);
                ImGui.TableNextColumn();
                ImGui.Text($"{key}s");
                ImGui.TableNextColumn();
                ImGui.Text(timeEntry.Strategy.Title.ToString());
                ImGui.TableNextColumn();
                using (var font = ImRaii.PushFont(UiBuilder.IconFont))
                {
                    if (ImGui.Button(FontAwesomeIcon.Trash.ToIconChar().ToString()))
                    {
                        toRemove = key;
                    }
                }
                i++;
            }

            // new entry
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(1);
            using (ImRaii.ItemWidth(100))
            {
                ImGui.InputInt($"###Timer", ref timeInput);
            }
            ImGui.TableNextColumn();
            using (ImRaii.ItemWidth(150))
            {
                ComboNewEntry();
                ImGui.TableNextColumn();
                using (var font = ImRaii.PushFont(UiBuilder.IconFont))
                {
                    if (ImGui.Button(FontAwesomeIcon.Plus.ToIconChar().ToString()))
                    {
                        if (selectedStrategy != null && (timeInput > 0 || boardsInput.Count == 0 || boardsInput.Keys.First() > 0))
                        {
                            boardsInput[timeInput] = new(new(selectedStrategy.Index, selectedStrategy.Title.Trim(), selectedStrategy.IsFolder));
                            pendingChanges = true;
                        }
                    }
                }
            }
        }
        if (toRemove.HasValue)
        {
            boardsInput.Remove(toRemove.Value);
            pendingChanges = true;
        }
    }

    private void ComboNewEntry()
    {
        ImGui.SetNextItemWidth(-1);
        using (var combo = ImRaii.Combo($"###MPTFSlideSelectionNew", selectedStrategy?.Title.Trim()))
        {
            if (!combo.Success) return;
            if (strategyData.Count == 0)
            {
                PopulateStrategyData();
            }
            var idx = 0;
            foreach (var strategy in strategyData)
            {
                idx++;
                if (ImGui.Selectable($"{strategy.Title}###MPTFNewCombo{idx}"))
                {
                    selectedStrategy = strategy;
                }
            }
        }
    }

    private void ComboWeatherSetting()
    {
        using (var combo = ImRaii.Combo($"Combat Condition###MPTFWeatherSetting", GetWeatherSettingText(weatherSettingInput)))
        {
            if (!combo.Success) return;
            var idx = 0;
            foreach (var val in Enum.GetValues<ConfigWeatherSetting>())
            {
                if (ImGui.Selectable($"{GetWeatherSettingText(val)}###{idx}"))
                {
                    pendingChanges = true;
                    weatherSettingInput = val;
                }
                idx++;
            }
        }
    }

    private static string GetWeatherSettingText(ConfigWeatherSetting weatherSetting) => weatherSetting switch
    {
        ConfigWeatherSetting.Any => "Any",
        ConfigWeatherSetting.OnlyInCombat => "Only in combat",
        ConfigWeatherSetting.OnlyOutCombat => "Only out of combat",
        _ => "Unknown"
    };

    private unsafe void PopulateStrategyData()
    {
        var tofu = (TofuModule*)FFXIVClientStructs.FFXIV.Client.UI.Misc.TofuModule.Instance();
        if (tofu == null) return;
        var tofuChild = tofu->TofuModuleChild;
        if (tofuChild == null) return;

        var sortedTree = tofuChild->SavedFolders.ToArray()
            .Where(f => f.IsValid)
            .GroupJoin(
                tofuChild->SavedBoards.ToArray().Where(b => b.IsValid),
                f => f.Index,
                b => b.Folder,
                (f, boards) => new {
                    Folder = f,
                    SortedBoards = boards.OrderBy(b => b.PositionInList).ToList()
                }
            )
            .OrderBy(x => x.Folder.PositionInList)
            .ToList();

        foreach (var entry in sortedTree)
        {
            if (entry.Folder.IsSingleItem)
            {
                var first = entry.SortedBoards.FirstOrDefault();
                strategyData.Add(new(first.Index, first.Title, false));
            }
            else
            {
                strategyData.Add(new(entry.Folder.Index, entry.Folder.Title, true));
                foreach (var board in entry.SortedBoards)
                {
                    strategyData.Add(new(board.Index, $"\t{board.Title}", false));
                }
            }
        }

    }
}
