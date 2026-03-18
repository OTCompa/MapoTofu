using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using MapoTofu.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using TofuModule = MapoTofu.Structs.TofuModule;

namespace MapoTofu;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    private const string CommandName = "/mptf";
    
    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("Mapo Tofu");
    private ConfigWindow ConfigWindow { get; init; }

    private readonly Queue<Func<bool>> actionQueue = new();
    private uint delay = 0;
    private int retries = 0;
    private readonly Stopwatch sw = new();

#if DEBUG
    private const string DebugName = "/mptfd";
    private const string DebugPrintName = "mptfdprint";
#endif

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Configure strategy boards to open automatically"
        });

#if DEBUG
        CommandManager.AddHandler(DebugName, new CommandInfo(DebugTerritory)
        {
            HelpMessage = ""
        });
#endif

        // Tell the UI system that we want our windows to be drawn through the window system
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // This adds a button to the plugin installer entry of this plugin which allows
        // toggling the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        Framework.Update += OnFrameworkUpdate;
        ClientState.TerritoryChanged += OnTerritoryChanged;

    }

    public void Dispose()
    {
        // Unregister all actions to not leak anything during disposal of plugin
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

#if DEBUG
        CommandManager.RemoveHandler(DebugName);
#endif

    }

    private void OnTerritoryChanged(ushort obj)
    {
        if (Configuration.TerritoryInitialStrategy.ContainsKey(obj))
        {
            var val = Configuration.TerritoryInitialStrategy[obj];
            if (!val.Enabled) return;
            delay = 200;
            actionQueue.Enqueue(() => OpenTofuPreviewOnTerritoryChange(val));
        }
    }

    private unsafe bool OpenTofuPreviewOnTerritoryChange(Common.StrategyConfigEntry entry)
    {
        Utility.HideTofu();
        Utility.ShowTofu();

        var idx = FindBoardPosition(entry.Strategy);
        if (idx < -1 || idx > 49)
        {
            Log.Error($"An error occurred in calculating the strategy position.\nCalculated position: {idx}");
            OnDebug("", "");
            return false;
        }
        actionQueue.Enqueue(() => Utility.PeekBoard((uint)idx));
        actionQueue.Enqueue(Utility.ReviewBoard);
        return true;
    }

    // maps strategy board the index to TofuList entry # to view the corresponding board
    private unsafe int FindBoardPosition(Common.Strategy strategy)
    {
        var tofuModule = (TofuModule*)FFXIVClientStructs.FFXIV.Client.UI.Misc.TofuModule.Instance();
        if (tofuModule == null) return -1;
        var tofuChild = tofuModule->TofuModuleChild;
        if (tofuChild == null) return -1;
        if (!strategy.IsFolder)
        {
            // if selected is a singular board, only need to get position in list
            // and add number of the folders that came before it
            var board = tofuChild->SavedBoards[strategy.Index];
            var parentFolder = tofuChild->SavedFolders[board.Folder];
            return board.PositionInList + parentFolder.PositionInList;
        } else
        {
            // if selected is a folde,r just get the first board in the folder
            // should be the same thing (hopefully)
            var folder = tofuChild->SavedFolders[strategy.Index];
            //Log.Debug($"Folder {folder.Index}: {folder.Title}, {folder.PositionInList}");
            var lowestInFolder = 100;
            foreach(var board in tofuChild->SavedBoards)
            {
                if (!board.IsValid) continue;
                if (board.Folder != strategy.Index) continue;
                if (board.PositionInList < lowestInFolder)
                {
                    lowestInFolder = board.PositionInList;
                    //Log.Debug($"Lowest board {board.Index}: {board.Title}, {board.PositionInList}");
                }
            }
            return lowestInFolder + folder.PositionInList;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!sw.IsRunning && delay > 0)
        {
            sw.Restart();
        }

        if (actionQueue.Count == 0 || (delay > 0 && sw.ElapsedMilliseconds < delay))
        {
            return;
        }
        var oldDelay = delay;
        delay = 0;
        sw.Stop();

        try
        {
            if (actionQueue.TryPeek(out var next))
            {
                if (next())
                {
                    retries = 0;
                    actionQueue.Dequeue();
                } else
                {
                    retries++;
                    delay = oldDelay;
                    if (retries > 10)
                    {
                        retries = 0;
                        actionQueue.Clear();
                        Log.Error("Last action failed too many times. Aborting entire queue.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Failed: {ex.ToString()}");
        }
    }

    private unsafe void OnCommand(string command, string args) => ConfigWindow.Toggle();
    private void DebugTerritory(string command, string args) => OnTerritoryChanged(ClientState.TerritoryType);
    private unsafe void OnDebug(string command, string args)
    {
        var tofu = (TofuModule*)FFXIVClientStructs.FFXIV.Client.UI.Misc.TofuModule.Instance();
        Log.Debug($"{(nint)tofu:X02}");
        if (tofu == null) return;
        var tofuChild = tofu->TofuModuleChild;
        Log.Debug($"{(nint)tofuChild:X02}");
        if (tofuChild == null) return;
        var sb = new StringBuilder();

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
                sb.AppendLine($"{first.Title} ({entry.Folder.Index})");
            }
            else
            {
                sb.AppendLine($"{entry.Folder.Title}/ ({entry.Folder.Index})");
                foreach (var board in entry.SortedBoards)
                {
                    sb.AppendLine($"├─{board.Title} ({board.Index})");
                }
            }
        }

        Log.Debug(sb.ToString());
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
}
