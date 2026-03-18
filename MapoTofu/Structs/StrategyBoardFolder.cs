using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MapoTofu.Structs;

[StructLayout(LayoutKind.Explicit, Size = 0x47)]
public unsafe struct StrategyBoardFolder
{
    [FieldOffset(0x0)] [MarshalAs(UnmanagedType.I1)] public bool IsValid;
    [FieldOffset(0x1)] public byte Index;
    [FieldOffset(0x2)] public byte PositionInList;
    [FieldOffset(0x3)] private fixed byte title[20];
    [FieldOffset(0x43)] [MarshalAs(UnmanagedType.I1)] public bool IsSingleItem;

    public string Title
    {
        get
        {
            fixed (byte* ptr = title)
            {
                var len = 0;
                while (len < 20 && ptr[len] != 0)
                {
                    len++;
                }
                if (len == 0) return string.Empty;
                var temp = new ReadOnlySpan<byte>(ptr, len);
                return Encoding.UTF8.GetString(temp);
            }
        }
    }
}
