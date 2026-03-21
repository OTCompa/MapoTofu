using Dalamud.Plugin.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MapoTofu;

internal class ActionManager : IDisposable
{
    public readonly Queue<Func<bool>> actionQueue = new();
    public uint actionDelay = 0;
    public int actionRetries = 0;
    internal readonly Stopwatch sw = new();

    public ActionManager()
    {
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!sw.IsRunning && actionDelay > 0) sw.Restart();
        if (actionQueue.Count == 0 || (actionDelay > 0 && sw.ElapsedMilliseconds < actionDelay)) return;

        var oldDelay = actionDelay;
        actionDelay = 0;
        sw.Stop();

        try
        {
            if (actionQueue.TryPeek(out var next))
            {
                if (next())
                {
                    actionRetries = 0;
                    actionQueue.Dequeue();
                }
                else
                {
                    actionRetries++;
                    actionDelay = Math.Clamp(oldDelay * 2, 200, 5000);
                    if (actionRetries > 10)
                    {
                        actionRetries = 0;
                        actionQueue.Clear();
                        Log.Error("Last action failed too many times. Aborting entire queue.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Action failed {ex.Message}: {ex.StackTrace}");
        }
    }
}
