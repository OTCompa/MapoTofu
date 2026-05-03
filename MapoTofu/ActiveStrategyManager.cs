using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
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

    public void SearchAndRunInitState(bool encounterManagerShouldSkip = false) => SearchAndRunInitState(Plugin.DutyState.ContentFinderCondition.RowId, encounterManagerShouldSkip);

    public void SearchAndRunInitState(uint cfcId, bool encounterManagerShouldSkip)
    {
        activeEntry = null;
        if (configuration.StrategyBoardTriggerOptions.ContainsKey(cfcId))
        {
            var list = configuration.StrategyBoardTriggerOptions[cfcId];
            var inCombat = Plugin.Condition[ConditionFlag.InCombat];

            // prioritize triggers with weather then timer triggers
            // this LINQ query was written by AI based on my original logic
            var bestMatch = list.Where(e => e.Type == ConfigTriggerType.Weather && e.NewWeather == weather.weather)
                .Where(e => !e.OldWeatherEnabled)
                .FirstOrDefault(e => e.WeatherSetting switch
                {
                    ConfigWeatherSetting.OnlyInCombat => inCombat,
                    ConfigWeatherSetting.OnlyOutCombat => !inCombat,
                    _ => true
                }) ??
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
            Plugin.Log.Debug($"Triggers for territory {cfcId} not found");
        }
    }

    public void InitializeActiveBoard(bool encounterManagerShouldSkip = false)
    {
        if (activeEntry == null) return;

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

    public unsafe bool OpenStrategy(StrategyConfigEntry entry)
    {
        try
        {
            if (Utility.ShouldWait()) return false;
            Plugin.Log.Info($"Opening strategy board: {entry.Strategy.Title}");

            // TofuList must be "visible" for TofuPreview to show, so always ensure TofuList is visible
            var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("TofuList").Address;
            if (addon == null || !addon->IsVisible)
            {
                Utility.ShowTofu();
                // if ReviewBoard() is called too fast, TofuList will actually be visible instead of hidden during preview
                actionManager.actionDelay = 100;
            }

            var idx = FindBoardPosition(entry.Strategy);
            if (idx < -1 || idx > 49)
            {
                Plugin.Log.Error($"An error occurred in calculating the strategy position.\nCalculated position: {idx}");
                Debug();
                return false;
            }

            var tofu = AgentTofuList.Instance();
            if (tofu == null) return false;
            if (tofu->Data == null) return false;
            tofu->Data->SavedSelectedIndex = idx;

            // Doing the following only once toggles TofuPreview if it's already active, so toggle it twice to refresh to new board if needed
            actionManager.actionQueue.Enqueue(Utility.ReviewBoard2);
            actionManager.actionQueue.Enqueue(() =>
            {
                var tofuPreview = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("TofuPreview").Address;
                if (tofuPreview == null || !tofuPreview->IsVisible) Utility.ReviewBoard2();
                return true;
            });
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.Error($"{e.Message}: {e.StackTrace}");
            return false;
        }
    }

    // check plugin's understanding of strategy board order
    public unsafe void Debug()
    {
        var tofu = FFXIVClientStructs.FFXIV.Client.UI.Misc.TofuModule.Instance();
        if (tofu == null) return;

        var sb = new StringBuilder();
        // this LINQ query was written by AI based on my original logic
        var sortedTree = tofu->SavedFolderData->Folders.ToArray()
            .Where(f => f.IsValid)
            .GroupJoin(
                tofu->SavedBoardData->Boards.ToArray().Where(b => b.IsValid),
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
            if (entry == null) continue;

            if (entry.Folder.IsBoard)
            {
                var first = entry.SortedBoards.FirstOrDefault();
                sb.AppendLine($"{first.NameString} ({entry.Folder.Index})");
            }
            else
            {
                sb.AppendLine($"{entry.Folder.NameString}/ ({entry.Folder.Index})");
                foreach (var board in entry.SortedBoards)
                {
                    sb.AppendLine($"├─{board.NameString} ({board.Index})");
                }
            }
        }

        Plugin.Log.Debug(sb.ToString());
    }
}
