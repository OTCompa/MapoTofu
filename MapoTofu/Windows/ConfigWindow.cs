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

namespace MapoTofu.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    private readonly List<Common.Strategy> strategyData = [];
    private readonly Dictionary<ushort, string> territoryLUT = [];

    private int territoryInput = Plugin.ClientState.TerritoryType;
    private Common.Strategy? selectedStrategy = null;
    private bool addNewEntryFailed = false;

    public ConfigWindow(Plugin plugin) : base("Mapo Tofu###MPTFConfig")
    {
        var constraint = new WindowSizeConstraints();
        constraint.MinimumSize =  new Vector2(600, 250);
        SizeConstraints = constraint;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var change = false;
        ImGui.Separator();
        ImGui.Columns(6);
        var s = ImGui.GetIO().FontGlobalScale;
        ImGui.SetColumnWidth(0, 20);
        ImGui.SetColumnWidth(1, 60 * s);
        ImGui.SetColumnWidth(2, 150 * s);
        ImGui.SetColumnWidth(3, 250 * s);
        ImGui.SetColumnWidth(4, 100 * s);
        ImGui.NextColumn();
        ImGui.Text("\nEnabled");
        ImGui.NextColumn();
        ImGui.Text("\nTerritory");
        ImGui.NextColumn();
        ImGui.Text("\nStrategy");
        ImGui.NextColumn();
        ImGui.Text("Add/Remove\nEntry");
        ImGui.NextColumn();
        ImGui.NextColumn();
        ImGui.Separator();

        var dict = configuration.TerritoryInitialStrategy;

        foreach (var key in configuration.TerritoryInitialStrategy.Keys)
        {
            ImGui.NextColumn();
            change = ImGui.Checkbox($"###MPTFInitialSlide{key}", ref configuration.TerritoryInitialStrategy[key].Enabled);
            ImGui.NextColumn();
            if (!territoryLUT.ContainsKey(key))
            {
                territoryLUT[key] = Plugin.DataManager.GetExcelSheet<TerritoryType>().GetRow(key)
                    .PlaceName.Value.Name.ToString() ?? "Unknown";
            }
            ImGui.TextWrapped($"{territoryLUT[key]} ({key.ToString()})");
            ImGui.NextColumn();
            ComboEntry(key, ref dict, ref change);
            ImGui.NextColumn();
            using (var font = ImRaii.PushFont(UiBuilder.IconFont))
            {
                if (ImGui.Button($"{FontAwesomeIcon.Trash.ToIconString()}###MPTFTrash{key}"))
                {
                    dict.Remove(key);
                    change = true;
                }
            }
            ImGui.NextColumn();
            ImGui.NextColumn();
            ImGui.Separator();
        }

        ImGui.NextColumn();
        ImGui.Text("New:");
        ImGui.NextColumn();
        ImGui.InputInt("", ref territoryInput);
        ImGui.NextColumn();

        ComboNewEntry();

        ImGui.NextColumn();

        using (var font = ImRaii.PushFont(UiBuilder.IconFont))
        {
            if (ImGui.Button($"{FontAwesomeIcon.Plus.ToIconString()}###MPTFAddNewEntry"))
            {
                if (territoryInput > -1 && selectedStrategy != null)
                {
                    var key = (ushort)territoryInput;
                    if (!dict.ContainsKey(key))
                    {
                        addNewEntryFailed = false;
                        dict[key] = new(selectedStrategy);
                        territoryInput = Plugin.ClientState.TerritoryType;
                        selectedStrategy = null;
                        change = true;
                    } else
                    {
                        addNewEntryFailed = true;
                    }
                } 
            }
        }

        ImGui.NextColumn();
        ImGui.NextColumn();
        ImGui.Separator();
        ImGui.Columns(1);

        if (addNewEntryFailed)
        {
            ImGui.Text("Failed to add new entry. An entry with the same territory already exists");
        }

        ImGui.Text($"Current territory ID: {Plugin.ClientState.TerritoryType}");

        if (ImGui.Button("Refresh strategy boards###MPTFRefresh"))
        {
            strategyData.Clear();
            PopulateStrategyData();
        }

        if (change)
        {
            configuration.Save();
        }
    }

    private void ComboEntry(ushort key, ref Dictionary<ushort, Common.StrategyConfigEntry> dict, ref bool change)
    {
        using (var combo = ImRaii.Combo($"###MPTFSlideSelection{key}", dict[key].Strategy.Title.Trim()))
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
                if (ImGui.Selectable($"{strategy.Title}###MPTFCombo{key}_{idx}"))
                {
                    dict[key].Strategy = strategy;
                    change = true;
                }
            }
        }
    }

    private void ComboNewEntry()
    {
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
