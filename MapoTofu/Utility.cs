using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace MapoTofu;

internal static class Utility
{
    private static unsafe void GenerateCallback(AtkUnitBase* unitBase, params object[] values)
    {
        if (unitBase == null) throw new Exception("Null UnitBase");
        var atkValues = (AtkValue*)Marshal.AllocHGlobal(values.Length * sizeof(AtkValue));
        if (atkValues == null) return;
        try
        {
            for (var i = 0; i < values.Length; i++)
            {
                var v = values[i];
                switch (v)
                {
                    case uint uintValue:
                        atkValues[i].Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.UInt;
                        atkValues[i].UInt = uintValue;
                        break;

                    case int intValue:
                        atkValues[i].Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int;
                        atkValues[i].Int = intValue;
                        break;

                    case float floatValue:
                        atkValues[i].Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Float;
                        atkValues[i].Float = floatValue;
                        break;

                    case bool boolValue:
                        atkValues[i].Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Bool;
                        atkValues[i].Byte = (byte)(boolValue ? 1 : 0);
                        break;

                    case string stringValue:
                        {
                            atkValues[i].Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String;
                            var stringBytes = Encoding.UTF8.GetBytes(stringValue);
                            var stringAlloc = Marshal.AllocHGlobal(stringBytes.Length + 1);
                            Marshal.Copy(stringBytes, 0, stringAlloc, stringBytes.Length);
                            Marshal.WriteByte(stringAlloc, stringBytes.Length, 0);
                            atkValues[i].String = (byte*)stringAlloc;
                            break;
                        }
                    default:
                        throw new ArgumentException($"Unable to convert type {v.GetType()} to AtkValue");
                }
            }

            unitBase->FireCallback((uint)values.Length, atkValues);
        }
        finally
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (atkValues[i].Type == FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String)
                {
                    Marshal.FreeHGlobal(new IntPtr(atkValues[i].String));
                }
            }
            Marshal.FreeHGlobal(new IntPtr(atkValues));
        }
    }

    private static unsafe bool CallbackHelper(string addonName, params object[] values)
    {
        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(addonName).Address;
        if (addon == null || addon->IsVisible == false || !addon->IsReady || !addon->IsFullyLoaded()) return false;
        GenerateCallback(addon, values);
        return true;
    }

    public static unsafe bool HideTofu()
    {
        var agent = Plugin.GameGui.GetAgentById((int)AgentId.TofuList);
        if (agent == null) return false;
        var agentPtr = (AgentInterface*)agent.Address;
        agentPtr->VirtualTable->Hide(agentPtr);
        return true;
    }
    
    public static unsafe bool ShowTofu()
    {
        var agent = Plugin.GameGui.GetAgentById((int)AgentId.TofuList);
        if (agent == null) return false;
        var agentPtr = (AgentInterface*)agent.Address;
        agentPtr->VirtualTable->Show(agentPtr);
        return true;
    }

    public static unsafe bool ShouldWait()
    {
        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("FadeMiddle").Address;
        if (addon == null || addon->IsVisible) return true;
        return false;
    }

    public static bool PrevBoard() => CallbackHelper("TofuPreview", 0, 5);
    public static bool NextBoard() => CallbackHelper("TofuPreview", 0, 6);
    public static bool ReturnToList() => CallbackHelper("TofuPreview", 0, 1);
    public static bool PeekBoard(uint boardNum) => CallbackHelper("TofuList", 3, boardNum);
    public static bool ReviewBoard() => CallbackHelper("TofuList", 0, 9);
    public static bool ExitPreview() => CallbackHelper("TofuPreview", -1);
}
