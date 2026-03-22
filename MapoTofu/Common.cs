using MapoTofu.Structs;
using System.Collections.Generic;
using System.Linq;
using static MapoTofu.Windows.ConfigWindow;

namespace MapoTofu;

public class Common
{
    public class Strategy(int Index, string Title, bool IsFolder)
    {
        public int Index = Index;
        public string Title = Title;
        public bool IsFolder = IsFolder;
    }

    public enum ConfigTriggerType
    {
        Timer,
        Weather
    }

    public enum ConfigWeatherSetting
    {
        Any,
        OnlyInCombat,
        OnlyOutCombat
    }

    public class StrategyConfigEntry(Strategy Strategy)
    {
        public bool Enabled = true;
        public Strategy Strategy = Strategy;
    };


    public class TriggerEntry
    {
        public bool Enabled = true;
        public string Label = "";
        public ConfigTriggerType Type = ConfigTriggerType.Timer;
        public int NewWeather = 0;
        public bool OldWeatherEnabled = false;
        public int OldWeatherId = 0;
        public SortedDictionary<int, StrategyConfigEntry> Boards = [];
        public ConfigWeatherSetting WeatherSetting = ConfigWeatherSetting.Any;
        public bool IsInterruptable = true;

        public TriggerEntry() { }

        public TriggerEntry(TriggerEntry triggerEntry)
        {
            Enabled = triggerEntry.Enabled;
            Label = triggerEntry.Label;
            Type = triggerEntry.Type;
            NewWeather = triggerEntry.NewWeather;
            OldWeatherEnabled = triggerEntry.OldWeatherEnabled;
            OldWeatherId = triggerEntry.OldWeatherId;
            Boards = new(triggerEntry.Boards);
        }
    }

    // maps the strategy board index to TofuList entry # to view the corresponding board
    public static unsafe int FindBoardPosition(Strategy strategy)
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
        }
        else
        {
            // if selected is a folder, just get the first board in the folder
            // should be the same thing (hopefully)
            var folder = tofuChild->SavedFolders[strategy.Index];
            //Log.Debug($"Folder {folder.Index}: {folder.Title}, {folder.PositionInList}");
            var lowestInFolder = 100;
            foreach (var board in tofuChild->SavedBoards)
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
}
