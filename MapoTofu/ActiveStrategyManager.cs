using Dalamud.Game.ClientState.Conditions;
using MapoTofu.Structs;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static MapoTofu.Common;

namespace MapoTofu;

internal class ActiveStrategyManager
{
    private readonly ActionManager actionManager;
    private readonly Configuration configuration;
    private readonly Weather weather;

    public TriggerEntry? activeEntry = null;
    public SortedDictionary<int, StrategyConfigEntry>.Enumerator currentEntry;
    public bool encounterManagerShouldSkip = false;

    public ActiveStrategyManager(ActionManager actionManager, Configuration configuration, Weather weather)
    {
        this.actionManager = actionManager;
        this.configuration = configuration;
        this.weather = weather;

        if (configuration.CheckOnPluginLoad)
        {
            SearchAndRunInitState();
        }
    }

    public void SearchAndRunInitState(bool encounterManagerShouldSkip = false) => SearchAndRunInitState(Plugin.ClientState.TerritoryType, encounterManagerShouldSkip);

    public void SearchAndRunInitState(ushort territory, bool encounterManagerShouldSkip)
    {
        activeEntry = null;
        if (configuration.StrategyBoardTriggerOptions.ContainsKey(territory))
        {
            var list = configuration.StrategyBoardTriggerOptions[territory];
            var inCombat = Plugin.Condition[ConditionFlag.InCombat];
            // prioritize triggers with weather then timer triggers
            var bestMatch = list.Where(e => e.Type == ConfigTriggerType.Weather && e.NewWeather == weather.weather)
                .Where(e => !e.OldWeatherEnabled)
                .Where(e => e.WeatherSetting switch
                {
                    ConfigWeatherSetting.OnlyInCombat => inCombat,
                    ConfigWeatherSetting.OnlyOutCombat => !inCombat,
                    _ => true
                })
                .FirstOrDefault() ??
                list.FirstOrDefault(e => e.Type == ConfigTriggerType.Timer);

            if (bestMatch != null)
            {
                activeEntry = bestMatch;
                Plugin.Log.Debug($"SearchAndRunInitState: {bestMatch.Boards.Count} total boards");
                InitializeActiveBoard(encounterManagerShouldSkip);
            } else
            {
                Plugin.Log.Debug("No matching triggers found");
            }
        } else
        {
            Plugin.Log.Debug($"Triggers for territory {territory} not found");
        }
    }

    public void InitializeActiveBoard(bool encounterManagerShouldSkip = false)
    {
        if (activeEntry != null)
        {
            currentEntry = activeEntry.Boards.GetEnumerator();
            if (currentEntry.MoveNext())
            {
                var curr = currentEntry.Current;
                if (curr.Value.Enabled && curr.Key <= 0)
                {
                    // stupid way to avoid the weather handler and encounter timer handler
                    // both running at the same time and triggering the initial strategy twice
                    // i prob should redesign this but it works:tm:
                    if (encounterManagerShouldSkip) this.encounterManagerShouldSkip = true;
                    actionManager.actionDelay = 200;
                    actionManager.actionQueue.Enqueue(() => {
                        if (OpenStrategy(curr.Value))
                        {
                            if (!currentEntry.MoveNext()) activeEntry = null;
                            return true;
                        }
                        return false;
                    });
                    if (encounterManagerShouldSkip)
                    {
                        actionManager.actionQueue.Enqueue(() =>
                        {
                            this.encounterManagerShouldSkip = false;
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

    public bool OpenStrategy(StrategyConfigEntry entry)
    {
        try
        {
            if (Utility.ShouldWait()) return false;
            Utility.HideTofu();
            Utility.ShowTofu();
            Plugin.Log.Info($"Opening strategy board: {entry.Strategy.Title}");
            var idx = FindBoardPosition(entry.Strategy);
            if (idx < -1 || idx > 49)
            {
                Plugin.Log.Error($"An error occurred in calculating the strategy position.\nCalculated position: {idx}");
                Debug();
                return false;
            }
            actionManager.actionQueue.Enqueue(() => Utility.PeekBoard((uint)idx));
            actionManager.actionQueue.Enqueue(Utility.ReviewBoard);
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.Error($"{e.Message}: {e.StackTrace}");
            return false;
        }
    }

    // check plugin's understanding of strategy board order
    private unsafe void Debug()
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
}
