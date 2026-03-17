using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using MapoTofu.Windows;
using MapoTofu.Structs;
using System.Text;
using System;

namespace MapoTofu;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/mptf";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("Mapo Tofu");
    private ConfigWindow ConfigWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Configure strategy boards to open automatically"
        });

        // Tell the UI system that we want our windows to be drawn through the window system
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // This adds a button to the plugin installer entry of this plugin which allows
        // toggling the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

    }

    public void Dispose()
    {
        // Unregister all actions to not leak anything during disposal of plugin
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private unsafe void OnCommand(string command, string args)
    {
        var tofu = (TofuModule*)FFXIVClientStructs.FFXIV.Client.UI.Misc.TofuModule.Instance();
        Log.Debug($"{(nint)tofu:X02}");
        if (tofu == null) return;
        var tofuChild = tofu->TofuModuleChild;
        Log.Debug($"{(nint)tofuChild:X02}");
        if (tofuChild == null) return;
        var sb = new StringBuilder();
        sb.AppendLine("\n-----SavedBoards-----");
        foreach (var board in tofuChild->SavedBoards)
        {
            if (board.IsValid)
            {
                sb.AppendLine($"{Encoding.UTF8.GetString(board.Title)}");
            }
        }
        sb.AppendLine("-----SavedFolders-----");
        foreach (var board in tofuChild->SavedFolders)
        {
            if (board.IsValid && !board.IsSingleItem)
            {
                sb.AppendLine($"{Encoding.UTF8.GetString(board.Title)}");
            }
        }
        sb.AppendLine("-----SharedBoards-----");
        foreach (var board in tofuChild->SharedBoards)
        {
            if (board.IsValid)
            {
                sb.AppendLine($"{Encoding.UTF8.GetString(board.Title)}");
            }
        }
        sb.AppendLine("-----SharedFolders-----");
        foreach (var board in tofuChild->SharedFolders)
        {
            if (board.IsValid && !board.IsSingleItem)
            {
                sb.AppendLine($"{Encoding.UTF8.GetString(board.Title)}");
            }
        }
        Log.Debug(sb.ToString());
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
}
