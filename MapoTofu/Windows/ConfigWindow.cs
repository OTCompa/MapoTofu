using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using MapoTofu.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static MapoTofu.Common;

namespace MapoTofu.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly Plugin plugin;

    private readonly List<Strategy> strategyData = [];
    private readonly Dictionary<ushort, string> territoryLUT = [];

    private Strategy? selectedStrategy = null;

    private bool enabledInput = false;
    private string labelInput = "";
    private ConfigTriggerType typeInput = ConfigTriggerType.Timer;
    private int newWeatherInput = -1;
    private bool oldWeatherEnabledInput = false;
    private int oldWeatherInput = -1;
    private int timeInput = -1;
    private TriggerEntry triggerEntryInput = new();
    private int territoryInput = 0;
    public SortedDictionary<int, StrategyConfigEntry> boardsInput = [];

    private bool newTerritory = false;
    private int selectedTerritory = -1;
    private int selectedEntry = -1;
    private int toDelete = -1;

    private bool pendingChanges = false;

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
        DrawTestingTab();
    }
    
    private void DrawTestingTab()
    {
        DrawSelectionPane();
        ImGui.SameLine();
        DrawOptionPane();
    }

    private void DrawSelectionPane()
    {
        using (var c = ImRaii.Child("SelectionPane", new Vector2(300, 0), true))
        {
            foreach (var territory in configuration.StrategyBoardTriggerOptions)
            {
                using var tree = ImRaii.TreeNode($"{GetTerritoryName((ushort)territory.Key)} ({territory.Key})");
                if (tree)
                {
                    for (var i = 0; i < territory.Value.Count; i++)
                    {
                        using var _ = ImRaii.PushId(i);
                        var selectedLoop = selectedTerritory == territory.Key && selectedEntry == i;
                        if (ImGui.Selectable($"{territory.Value[i].Label}", selectedLoop))
                        {
                            OnSelect(territory.Value[i]);
                            selectedTerritory = territory.Key;
                            selectedEntry = i;
                        }
                        DrawContextMenu(i);
                    }

                    if (toDelete > -1)
                    {
                        if (selectedTerritory == territory.Key && selectedEntry == toDelete) selectedEntry = -1;
                        territory.Value.RemoveAt(toDelete);
                        configuration.Save();
                        toDelete = -1;
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
        }
    }

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
                    configuration.StrategyBoardTriggerOptions[selectedTerritory].Add(new(triggerEntryInput));
                    selectedEntry = configuration.StrategyBoardTriggerOptions[selectedTerritory].Count - 1;
                } else
                {
                    configuration.StrategyBoardTriggerOptions[selectedTerritory][selectedEntry].Enabled = enabledInput;
                    configuration.StrategyBoardTriggerOptions[selectedTerritory][selectedEntry].Label = labelInput;
                    configuration.StrategyBoardTriggerOptions[selectedTerritory][selectedEntry].Type = typeInput;
                    configuration.StrategyBoardTriggerOptions[selectedTerritory][selectedEntry].NewWeather = newWeatherInput;
                    configuration.StrategyBoardTriggerOptions[selectedTerritory][selectedEntry].OldWeatherEnabled = oldWeatherEnabledInput;
                    configuration.StrategyBoardTriggerOptions[selectedTerritory][selectedEntry].OldWeatherId = oldWeatherInput;
                    configuration.StrategyBoardTriggerOptions[selectedTerritory][selectedEntry].Boards = boardsInput;
                }

                configuration.Save();
                if (selectedTerritory == Plugin.ClientState.TerritoryType)
                {
                    plugin.ActiveStrategyManager.SearchAndRunInitState(Plugin.ClientState.TerritoryType);
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
                if (ImGui.Checkbox($"###Enabled", ref timeEntry.Enabled))
                {
                    
                }
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

    private void DrawContextMenu(int i)
    {
        using var c = ImRaii.ContextPopupItem("MPTFContextMenu");
        if (!c.Success) return;
        if (ImGui.MenuItem("Delete"))
        {
            toDelete = i;
        }
    }

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
        if (!territoryLUT.ContainsKey((ushort)territory))
        {
            territoryLUT[territory] = Plugin.DataManager.GetExcelSheet<TerritoryType>().GetRow((ushort)territory)
                .PlaceName.Value.Name.ToString() ?? "Unknown";
        }
        return territoryLUT[territory];
    }
}
