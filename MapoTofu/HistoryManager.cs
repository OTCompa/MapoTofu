using Dalamud.Game.ClientState.Conditions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MapoTofu;

internal class HistoryManager : IDisposable
{
    public record struct HistoryEntry(ushort Territory, ushort Weather, bool InCombat, DateTime Timestamp, int MsSinceLastWeather = -1);

    private readonly Configuration configuration;
    private readonly EncounterManager encounterManager;
    private readonly Weather weather;

    public readonly LinkedList<HistoryEntry> HistoryEntries = [];
    private HistoryEntry? lastEntry = null;
    private readonly Stopwatch delayedEntry = new();

    private const int DELAY_MS = 1000;

    public HistoryManager(Configuration configuration, EncounterManager encounterManager, Weather weather)
    {
        this.configuration = configuration;
        this.encounterManager = encounterManager;
        this.weather = weather;

        weather.OnWeatherChanged += OnWeatherChanged;
        Plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
        Plugin.Condition.ConditionChange += OnConditionChange;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        weather.OnWeatherChanged -= OnWeatherChanged;
        Plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
        Plugin.Condition.ConditionChange -= OnConditionChange;
        Plugin.Framework.Update -= OnFrameworkUpdate;
    }

    private void OnTerritoryChanged(ushort obj)
    {
        // edge case where source and destination have the same weather, thus OnWeatherChanged isn't called
        // OnTerritoryChanged triggers before the weather is set for the destination so we delay the entry
        delayedEntry.Restart();
    }


    private void OnWeatherChanged(ushort oldWeather, ushort newWeather)
    {
        AddCurrentState();
    }

    private void AddCurrentState()
    {
        delayedEntry.Stop();

        var newEntry = new HistoryEntry(
            Plugin.ClientState.TerritoryType,
            weather.weather,
            Plugin.Condition[ConditionFlag.InCombat],
            DateTime.Now
        );

        if (lastEntry.HasValue && lastEntry.Value.Territory == Plugin.ClientState.TerritoryType)
        {
            newEntry.MsSinceLastWeather = (int)(DateTime.Now - lastEntry.Value.Timestamp).TotalMilliseconds;
        }
        
        HistoryEntries.AddLast(newEntry);
        lastEntry = newEntry;

        if (HistoryEntries.Count > configuration.MaxHistoryEntries)
        {
            while (HistoryEntries.Count > configuration.MaxHistoryEntries)
            {
                HistoryEntries.RemoveFirst();
            }
        }
    }

    private void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework framework)
    {
        if (!delayedEntry.IsRunning) return;
        if (delayedEntry.ElapsedMilliseconds < DELAY_MS) return;

        AddCurrentState();
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag != ConditionFlag.InCombat) return;

        if (value)
        {
            AddCurrentState();
        }
    }
}
