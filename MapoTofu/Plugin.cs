using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using MapoTofu.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static MapoTofu.Common;
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
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;

    private const string CommandName = "/mptf";
    
    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("Mapo Tofu");
    private ConfigWindow ConfigWindow { get; init; }

    private readonly Queue<Func<bool>> actionQueue = new();
    private uint actionDelay = 0;
    private int actionRetries = 0;

    private readonly Stopwatch sw = new();
    private readonly Stopwatch encounterTimer = new();
    private SortedDictionary<int, StrategyConfigEntry>? activeEntry = null;
    private SortedDictionary<int, StrategyConfigEntry>.Enumerator currentEntry;
    private ushort weather = 0;
    private bool? combatState = null;
    private bool skipInitial = false;

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
        Condition.ConditionChange += OnConditionChange;
        ClientState.TerritoryChanged += OnTerritoryChanged;
        DutyState.DutyStarted += DutyState_DutyStarted;
        DutyState.DutyRecommenced += DutyState_DutyRecommenced;

        // probably make this an option
        if (DutyState.IsDutyStarted)
        {
            SearchAndRunInitState(ClientState.TerritoryType);
        }
#if DEBUG
        ConfigWindow.Toggle();
#endif
    }

    private void OnTerritoryChanged(ushort obj)
    {
        combatState = null;
        skipInitial = false;
        activeEntry = null;
        encounterTimer.Stop();
        sw.Stop();
        actionQueue.Clear();
        actionDelay = 0;
        actionRetries = 0;
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

    private void DutyState_DutyRecommenced(object? sender, ushort e)
    {
        SearchAndRunInitState(ClientState.TerritoryType);
    }

    private void DutyState_DutyStarted(object? sender, ushort e)
    {
        SearchAndRunInitState(ClientState.TerritoryType);
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag != ConditionFlag.InCombat) return;

        if (value)
        {
            Log.Info("COMBAT START");
            encounterTimer.Restart();
        } else
        {
            Log.Info("COMBAT STOP");
            encounterTimer.Stop();
        }
        combatState = value;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        HandleActionQueue();
        HandleWeather();
        HandleEncounterTimer();
    }

    private void HandleEncounterTimer()
    {
        if (!encounterTimer.IsRunning) return;
        if (activeEntry == null) return;
        if (skipInitial) return;
        var curr = currentEntry.Current;
        if (encounterTimer.Elapsed.Seconds < curr.Key) return;
        Log.Debug("Hallo");
        if (curr.Value.Enabled)
        {
            Log.Debug("handleEncounterTimer");
            actionQueue.Enqueue(() => {
                if (OpenStrategy(curr.Value))
                {
                    if (!currentEntry.MoveNext()) activeEntry = null;
                    return true;
                }
                return false;
            });
        }
    }

    private void HandleActionQueue()
    {
        if (!sw.IsRunning && actionDelay > 0) sw.Restart();
        if (actionQueue.Count == 0 || (actionDelay > 0 && sw.ElapsedMilliseconds < actionDelay)) return;

        var oldDelay = actionDelay;
        actionDelay = 0;
        sw.Stop();

        try
        {
            if (actionQueue.TryPeek(out var next))
            {
                if (next())
                {
                    actionRetries = 0;
                    actionQueue.Dequeue();
                }
                else
                {
                    actionRetries++;
                    actionDelay = Math.Clamp(oldDelay * 2, 200, 5000);
                    if (actionRetries > 10)
                    {
                        actionRetries = 0;
                        actionQueue.Clear();
                        Log.Error("Last action failed too many times. Aborting entire queue.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Action failed {ex.Message}: {ex.StackTrace}");
        }
    }

    private unsafe void HandleWeather()
    {
        var weatherManager = WeatherManager.Instance();
        if (weatherManager == null) return;
        var currentWeather = weatherManager->GetCurrentWeather();
        if (weather == currentWeather) return;

        Log.Debug($"Weather changed: {weather} -> {currentWeather}");
        if (encounterTimer.IsRunning && DutyState.IsDutyStarted)
        {
            var changedBoards = false;
            var territory = ClientState.TerritoryType;

            if (Configuration.StrategyBoardTriggerOptions.ContainsKey(territory))
            {
                // only consider weather triggers, prioritize ones with a oldweather check
                var bestMatch = Configuration.StrategyBoardTriggerOptions[territory].FirstOrDefault(e =>
                        e.Type == ConfigTriggerType.Weather &&
                        e.OldWeatherEnabled &&
                        e.OldWeatherId == weather &&
                        e.NewWeather == currentWeather)
                    ?? Configuration.StrategyBoardTriggerOptions[territory].FirstOrDefault(e =>
                        e.Type == ConfigTriggerType.Weather &&
                        e.NewWeather == currentWeather);

                if (bestMatch != null)
                {
                    activeEntry = bestMatch.Boards;
                    changedBoards = true;
                    Log.Info("Weather: active entry changed!");
                }
            }

            if (changedBoards) RunActiveBoard(true);
        }

        weather = currentWeather;
        if (encounterTimer.IsRunning)
        {
            Log.Debug("Restarted Encounter Timer");
            encounterTimer.Restart();
        }
    }

    // gets the set of triggers that should be shown at the beginning/prepull
    public void SearchAndRunInitState(ushort territory)
    {
        activeEntry = null;
        if (Configuration.StrategyBoardTriggerOptions.ContainsKey(territory))
        {
            var list = Configuration.StrategyBoardTriggerOptions[territory];
            // prioritize triggers with weather then timer triggers
            var bestMatch = list.FirstOrDefault(e => e.Type == ConfigTriggerType.Weather && !e.OldWeatherEnabled && e.NewWeather == weather)
             ?? list.FirstOrDefault(e => e.Type == ConfigTriggerType.Timer);
            if (bestMatch != null)
            {
                activeEntry = bestMatch.Boards;
                Log.Info($"SearchAndRunInitState: {bestMatch.Boards.Count} total boards");
                RunActiveBoard(false);
            }
        }
    }

    private void RunActiveBoard(bool isWeatherHandler = false)
    {
        if (activeEntry != null)
        {
            currentEntry = activeEntry.GetEnumerator();
            if (currentEntry.MoveNext())
            {
                var curr = currentEntry.Current;
                if (curr.Value.Enabled && curr.Key <= 0)
                {
                    // stupid way to avoid the weather handler and encounter timer handler
                    // both running at the same time and triggering the initial strategy twice
                    // i prob should redesign this but it works:tm:
                    if (isWeatherHandler) skipInitial = true;
                    actionDelay = 200;
                    actionQueue.Enqueue(() => {
                        if (OpenStrategy(curr.Value))
                        {
                            if (!currentEntry.MoveNext()) activeEntry = null;
                            return true;
                        }
                        return false;
                    });
                    if (isWeatherHandler)
                    {
                        actionQueue.Enqueue(() =>
                        {
                            skipInitial = false;
                            return true;
                        });
                    }
                }
            }
            else
            {
                activeEntry = null;
            }
        }
    }

    private bool OpenStrategy(StrategyConfigEntry entry)
    {
        try
        {
            if (Utility.ShouldWait()) return false;
            Utility.HideTofu();
            Utility.ShowTofu();
            Log.Info($"Opening strategy board: {entry.Strategy.Title}");
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
        catch (Exception e)
        {
            Log.Error($"{e.Message}: {e.StackTrace}");
            return false;
        }
    }

    private void OnCommand(string command, string args) => ConfigWindow.Toggle();
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
