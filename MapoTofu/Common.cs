namespace MapoTofu;

public class Common
{
    public class Strategy(int Index, string Title, bool IsFolder)
    {
        public int Index = Index;
        public string Title = Title;
        public bool IsFolder = IsFolder;
    }

    public class StrategyConfigEntry(Strategy Strategy)
    {
        public bool Enabled = true;
        public Strategy Strategy = Strategy;
    };
}
