using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using MapoTofu.Windows;

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

    internal ActionManager ActionManager { get; init; }
    internal ActiveStrategyManager ActiveStrategyManager { get; init; }
    internal EncounterManager EncounterManager { get; init; }
    internal Weather Weather { get; init; }
    internal HistoryManager HistoryManager { get; init; }

#if DEBUG
    private const string DebugName = "/mptfd";
    private const string PrintName = "/mptfp";
#endif

    public Plugin()
    {
        Log.MinimumLogLevel = Serilog.Events.LogEventLevel.Error;

#if DEBUG
        Log.MinimumLogLevel = Serilog.Events.LogEventLevel.Verbose;
#endif

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Configure strategy boards to open automatically"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        ActionManager = new();
        Weather = new();
        ActiveStrategyManager = new(ActionManager, Configuration, Weather);
        EncounterManager = new(ActionManager, ActiveStrategyManager, Configuration, Weather);
        HistoryManager = new(Configuration, EncounterManager, Weather);

#if DEBUG
        CommandManager.AddHandler(DebugName, new CommandInfo(DebugTerritory)
        {
            HelpMessage = ""
        });
        CommandManager.AddHandler(PrintName, new CommandInfo(DebugPrint)
        {
            HelpMessage = ""
        });
        ConfigWindow.Toggle();
#endif
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

        EncounterManager.Dispose();
        Weather.Dispose();
        ActionManager.Dispose();
#if DEBUG
        CommandManager.RemoveHandler(DebugName);
        CommandManager.RemoveHandler(PrintName);
#endif
    }

    private void OnCommand(string command, string args) => ConfigWindow.Toggle();
    private void DebugTerritory(string command, string args) => EncounterManager.OnTerritoryChanged(ClientState.TerritoryType);
    private void DebugPrint(string command, string args) => ActiveStrategyManager.Debug();
    public void ToggleConfigUi() => ConfigWindow.Toggle();
}
