using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace MapoTofu;

internal class EncounterManager : IDisposable
{
    private readonly ActionManager actionManager;
    private readonly ActiveStrategyManager activeStrategyManager;
    private readonly Configuration configuration;
    private readonly Weather weather;

    public readonly Stopwatch encounterTimer = new();

    public EncounterManager(ActionManager actionManager, ActiveStrategyManager activeStrategyManager, Configuration configuration, Weather weather)
    {
        this.actionManager = actionManager;
        this.activeStrategyManager = activeStrategyManager;
        this.configuration = configuration;
        this.weather = weather;

        Plugin.Framework.Update += OnFrameworkUpdate;
        Plugin.DutyState.DutyRecommenced += OnDutyRecommenced;
        Plugin.DutyState.DutyStarted += OnDutyStarted;
        Plugin.Condition.ConditionChange += OnConditionChange;
        Plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
        weather.OnWeatherChanged += OnWeatherChanged;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        Plugin.DutyState.DutyRecommenced -= OnDutyRecommenced;
        Plugin.DutyState.DutyStarted -= OnDutyStarted;
        Plugin.Condition.ConditionChange -= OnConditionChange;
        Plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
        weather.OnWeatherChanged -= OnWeatherChanged;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!encounterTimer.IsRunning) return;
        if (activeStrategyManager.activeEntry == null) return;
        if (activeStrategyManager.encounterManagerShouldSkip) return;
        var curr = activeStrategyManager.currentEntry.Current;
        if (encounterTimer.Elapsed.Seconds < curr.Key) return;
        if (curr.Value.Enabled)
        {
            Log.Debug("handleEncounterTimer");
            actionManager.actionQueue.Enqueue(() => {
                if (activeStrategyManager.OpenStrategy(curr.Value))
                {
                    if (!activeStrategyManager.currentEntry.MoveNext()) activeStrategyManager.activeEntry = null;
                    return true;
                }
                return false;
            });
        }
    }


    internal void OnTerritoryChanged(ushort obj)
    {
        encounterTimer.Stop();
        activeStrategyManager.encounterManagerShouldSkip = false;
        activeStrategyManager.activeEntry = null;
        actionManager.sw.Stop();
        actionManager.actionQueue.Clear();
        actionManager.actionDelay = 0;
        actionManager.actionRetries = 0;
    }

    private void OnDutyRecommenced(object? sender, ushort e)
    {
        activeStrategyManager.SearchAndRunInitState();
    }

    private void OnDutyStarted(object? sender, ushort e)
    {
        activeStrategyManager.SearchAndRunInitState();
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag != ConditionFlag.InCombat) return;

        if (value)
        {
            Plugin.Log.Debug("COMBAT START");
            encounterTimer.Restart();
            if (activeStrategyManager.activeEntry == null)
            {
                activeStrategyManager.SearchAndRunInitState(true);
            }
        }
        else
        {
            Plugin.Log.Debug("COMBAT STOP");
            encounterTimer.Stop();
        }
    }

    private void OnWeatherChanged(ushort oldWeather, ushort newWeather)
    {
        if (!encounterTimer.IsRunning) return;
        var territory = Plugin.ClientState.TerritoryType;
        if (configuration.StrategyBoardTriggerOptions.ContainsKey(territory))
        {
            // only consider weather triggers, prioritize ones with a oldweather check
            var bestMatch = configuration.StrategyBoardTriggerOptions[territory].FirstOrDefault(e =>
                    e.Type == Common.ConfigTriggerType.Weather &&
                    e.OldWeatherEnabled &&
                    e.OldWeatherId == oldWeather &&
                    e.NewWeather == newWeather)
                ?? configuration.StrategyBoardTriggerOptions[territory].FirstOrDefault(e =>
                    e.Type == Common.ConfigTriggerType.Weather &&
                    e.NewWeather == newWeather);

            if (bestMatch != null)
            {
                activeStrategyManager.activeEntry = bestMatch.Boards;
                Plugin.Log.Debug("Weather: active entry changed!");
                activeStrategyManager.InitializeActiveBoard(true);
            }
        }
        Plugin.Log.Debug("Restarted Encounter Timer");
        encounterTimer.Restart();
    }
}
