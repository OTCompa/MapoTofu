using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Common.Lua;
using Lumina.Excel.Sheets;
using MapoTofu.Structs;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Channels;
using static MapoTofu.Configuration;

namespace MapoTofu.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    internal class Strategy(int Index, string Title, bool IsFolder)
    {
        public int Index = Index;
        public string Title = Title;
        public bool IsFolder = IsFolder;
    }

    private readonly List<Strategy> strategyData = [];
    private readonly Dictionary<ushort, string> territoryLUT = [];

    private int selectedSlide = -1;
    private string selectedSlideTitle = "";
    private int territoryInput = Plugin.ClientState.TerritoryType;
    private bool selectedIsFolder = false;

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
                if (selectedSlide != -1 && territoryInput > -1)
                {
                    var key = (ushort)territoryInput;
                    dict[key] = new(true, selectedSlideTitle.Replace("\t",""), selectedSlide, selectedIsFolder);
                    territoryInput = Plugin.ClientState.TerritoryType;
                    selectedSlide = -1;
                    selectedSlideTitle = "";
                    change = true;
                } 
            }
        }

        ImGui.NextColumn();
        ImGui.NextColumn();
        ImGui.Separator();
        ImGui.Columns(1);
        ImGui.Text($"Current territory ID: {Plugin.ClientState.TerritoryType}");
        if (change)
        {
            configuration.Save();
        }
    }

    private void ComboEntry(ushort key, ref Dictionary<ushort, SlideConfigEntry> dict, ref bool change)
    {
        using (var combo = ImRaii.Combo($"###MPTFSlideSelection{key}", dict[key].Title))
        {
            if (combo.Success)
            {
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
                        dict[key].Index = strategy.Index;
                        dict[key].IsFolder = strategy.IsFolder;
                        dict[key].Title = strategy.Title.Trim();
                        change = true;
                    }
                }
            }
        }
    }

    private void ComboNewEntry()
    {
        using (var combo = ImRaii.Combo($"###MPTFSlideSelectionNew", selectedSlideTitle))
        {
            if (combo.Success)
            {
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
                        selectedSlide = (int)strategy.Index;
                        selectedSlideTitle = strategy.Title.Trim();
                        selectedIsFolder = strategy.IsFolder;
                    }
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
