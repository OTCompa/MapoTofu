// taken from Wintermute, https://discord.com/channels/581875019861328007/653504487352303619/1458959023532544299 in the XIVlauncher discord
using System;
using System.Runtime.InteropServices;

namespace MapoTofu.Structs;

[StructLayout(LayoutKind.Explicit, Size = 0x38)]
public unsafe struct StrategyBoardObject
{
    [FieldOffset(0x0)] private fixed byte label[30];
    [FieldOffset(0x20)] public uint Color;
    [FieldOffset(0x24)] public ushort PosX;
    [FieldOffset(0x26)] public ushort PosY;
    [FieldOffset(0x28)] public ushort ObjectType;
    [FieldOffset(0x2A)] public ushort Flags;
    [FieldOffset(0x2C)] public ushort Angle;
    [FieldOffset(0x2E)] public ushort ArgsA;
    [FieldOffset(0x30)] public ushort ArgsB;
    [FieldOffset(0x32)] public ushort ArgsC;
    [FieldOffset(0x34)] public byte Scale;

    public readonly ReadOnlySpan<byte> Label
    {
        get
        {
            fixed (byte* ptr = label)
            {
                var len = 0;
                while (len < 30 && ptr[len] != 0)
                {
                    len++;
                }
                if (len == 0) return [];
                return new ReadOnlySpan<byte>(ptr, len);
            }
        }
    }
}
