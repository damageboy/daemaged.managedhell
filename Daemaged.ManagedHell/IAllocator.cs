using System;
using System.Runtime.InteropServices;

namespace Daemaged.ManagedHell
{
  public interface IAllocator
  {
    IntPtr Allocate(int size);
    void Free(IntPtr p, int size);
  }

  public class HeapAllocator : IAllocator
  {
    public IntPtr Allocate(int size)
    { return Marshal.AllocHGlobal(size); }

    public void Free(IntPtr p, int size)
    { Marshal.FreeHGlobal(p); }
  }
}
