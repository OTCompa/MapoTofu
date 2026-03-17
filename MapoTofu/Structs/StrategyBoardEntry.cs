using System;
using System.Runtime.InteropServices;

namespace MapoTofu.Structs;

[StructLayout(LayoutKind.Explicit, Size = 0x10B4)]
public unsafe struct StrategyBoardEntry
{
    [FieldOffset(0x0)] private fixed byte strategyObjects[75 * Constants.StrategyBoardObjectSize];
    [FieldOffset(0x1068)] [MarshalAs(UnmanagedType.I1)] public bool IsValid;
    [FieldOffset(0x1069)] public byte Index;
    [FieldOffset(0x106A)] public byte PositionInList;
    [FieldOffset(0x106B)] public byte Folder;
    [FieldOffset(0x106C)] private fixed byte title[20];

    public readonly ReadOnlySpan<StrategyBoardObject> StrategyBoardObjects
    {
        get
        {
            fixed (byte* ptr = strategyObjects)
            {
                var byteSpan = new ReadOnlySpan<byte>(ptr, 75 * Constants.StrategyBoardObjectSize);
                return MemoryMarshal.Cast<byte, StrategyBoardObject>(byteSpan);
            }
        }
    }

    public readonly ReadOnlySpan<byte> Title
    {
        get
        {
            fixed (byte* ptr = title)
            {
                int len = 0;
                while (len < 20 && ptr[len] != 0)
                {
                    len++;
                }
                if (len == 0) return [];
                return new ReadOnlySpan<byte>(ptr, len);
            }
        }
    }
}
