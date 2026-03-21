using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MapoTofu;

internal class Weather : IDisposable
{
    public ushort weather = 0;
    public event Action<ushort, ushort>? OnWeatherChanged;

    public Weather()
    {
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
    }

    private unsafe void OnFrameworkUpdate(IFramework framework)
    {
        var weatherManager = WeatherManager.Instance();
        if (weatherManager == null) return;
        var newWeather = weatherManager->GetCurrentWeather();
        if (weather == newWeather) return;

        Plugin.Log.Debug($"Weather changed: {weather} -> {newWeather}");
        OnWeatherChanged?.Invoke(weather, newWeather);
        weather = newWeather;
    }
}
