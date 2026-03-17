
using System.Runtime.InteropServices;

namespace MapoTofu.Structs;

[StructLayout(LayoutKind.Explicit, Size = 0x50)]
public unsafe struct TofuModule
{
    [FieldOffset(0x48)] public TofuModuleChild* TofuModuleChild;
}
