using System;
using System.Runtime.InteropServices;

namespace MapoTofu.Structs;

// Dynamis says that the struct's size is 0x28 from the dtor
// This is actually an array with 4 elements of the struct
// but doing it this way makes it a little easier for me
[StructLayout(LayoutKind.Explicit, Size = 0xA0)]
public unsafe struct TofuModuleChild
{
    [FieldOffset(0x10)] private nint savedBoardOrder;
    [FieldOffset(0x1C)] public byte TotalSavedBoards;
    [FieldOffset(0x20)] private nint savedBoards;

    [FieldOffset(0x38)] private nint savedFolderOrder;
    [FieldOffset(0x44)] public byte TotalSavedFolders;
    [FieldOffset(0x48)] private nint savedFolders;

    [FieldOffset(0x60)] private nint sharedBoardOrder;
    [FieldOffset(0x6C)] public byte TotalSharedBoards;
    [FieldOffset(0x70)] private nint sharedBoards;

    [FieldOffset(0x88)] private nint sharedFolderOrder;
    [FieldOffset(0x94)] public byte TotalSharedFolders;
    [FieldOffset(0x98)] private nint sharedFolders;

    public readonly ReadOnlySpan<byte> SavedBoardOrder
    {
        get
        {
            if (savedBoardOrder == nint.Zero) return [];
            return new ReadOnlySpan<byte>((byte*)savedBoardOrder, 50);
        }
    }

    public readonly ReadOnlySpan<StrategyBoardEntry> SavedBoards
    {
        get
        {
            if (savedBoards == nint.Zero) return [];
            return new ReadOnlySpan<StrategyBoardEntry>((byte*)savedBoards, 50);
        }
    }

    public readonly ReadOnlySpan<byte> SavedFolderOrder
    {
        get
        {
            if (savedFolderOrder == nint.Zero) return [];
            return new ReadOnlySpan<byte>((byte*)savedFolderOrder, 55);
        }
    }

    public readonly ReadOnlySpan<StrategyBoardFolder> SavedFolders
    {
        get
        {
            if (savedFolders == nint.Zero) return [];
            return new ReadOnlySpan<StrategyBoardFolder>((byte*)savedFolders, 55);
        }
    }

    public readonly ReadOnlySpan<byte> SharedBoardOrder
    {
        get
        {
            if (sharedBoardOrder == nint.Zero) return [];
            return new ReadOnlySpan<byte>((byte*)sharedBoardOrder, 20);
        }
    }

    public readonly ReadOnlySpan<StrategyBoardEntry> SharedBoards
    {
        get
        {
            if (sharedBoards == nint.Zero) return [];
            return new ReadOnlySpan<StrategyBoardEntry>((byte*)sharedBoards, 20);
        }
    }

    public readonly ReadOnlySpan<byte> SharedFolderOrder
    {
        get
        {
            if (sharedFolderOrder == nint.Zero) return [];
            return new ReadOnlySpan<byte>((byte*)sharedFolderOrder, 20);
        }
    }

    public readonly ReadOnlySpan<StrategyBoardFolder> SharedFolders
    {
        get
        {
            if (sharedFolders == nint.Zero) return [];
            return new ReadOnlySpan<StrategyBoardFolder>((byte*)sharedFolders, 20);
        }
    }
}
