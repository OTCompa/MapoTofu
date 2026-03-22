using Dalamud.Game.ClientState.Conditions;
using System;
using System.Collections.Generic;
using System.Text;

namespace MapoTofu;

internal class HistoryManager : IDisposable
{
    public record struct HistoryEntry(ushort Territory, ushort Weather, bool InCombat, DateTime Timestamp, int MsSinceLastWeather = -1);

    private Configuration configuration;
    private EncounterManager encounterManager;
    private Weather weather;

    public readonly LinkedList<HistoryEntry> HistoryEntries = [];
    private HistoryEntry? lastElement = null;

    public HistoryManager(Configuration configuration, EncounterManager encounterManager, Weather weather)
    {
        this.configuration = configuration;
        this.encounterManager = encounterManager;
        this.weather = weather;

        weather.OnWeatherChanged += OnWeatherChanged;
        Plugin.Condition.ConditionChange += OnConditionChange;
    }

    public void Dispose()
    {
        weather.OnWeatherChanged -= OnWeatherChanged;
    }

    private void OnWeatherChanged(ushort oldWeather, ushort newWeather)
    {
        var newEntry = new HistoryEntry(
            Plugin.ClientState.TerritoryType,
            newWeather,
            encounterManager.combatState ?? false,
            DateTime.Now
            );

        if (lastElement.HasValue && lastElement.Value.Territory == Plugin.ClientState.TerritoryType)
        {
            newEntry.MsSinceLastWeather = (int)(DateTime.Now - lastElement.Value.Timestamp).TotalMilliseconds;
        }

        HistoryEntries.AddLast(newEntry);
        lastElement = newEntry;

        if (HistoryEntries.Count > configuration.MaxHistoryEntries) HistoryEntries.RemoveFirst();
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag != ConditionFlag.InCombat) return;

        if (value)
        {
            var newEntry = new HistoryEntry(
                Plugin.ClientState.TerritoryType,
                weather.weather,
                value,
                DateTime.Now
                );

            if (lastElement.HasValue && lastElement.Value.Territory == Plugin.ClientState.TerritoryType)
            {
                newEntry.MsSinceLastWeather = (int)(DateTime.Now - lastElement.Value.Timestamp).TotalMilliseconds;
            }

            HistoryEntries.AddLast(newEntry);
            lastElement = newEntry;

            if (HistoryEntries.Count > configuration.MaxHistoryEntries) HistoryEntries.RemoveFirst();
        }
    }
}
