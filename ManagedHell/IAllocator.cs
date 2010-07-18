using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ManagedHell.MMF;

namespace ManagedHell
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

  public class MemMapAllocator : IAllocator
  {
    private readonly IMemoryMappedFile _mmap;
    private long _offset;
    private long _size;
    private long _fileSize;

    public MemMapAllocator(string path, long fileSize, long offset, long size)
    {
      _mmap = MemoryMappedFileFactory.Create(path, fileSize);
      _fileSize = fileSize;
      _size = size;
      _offset = offset;
      _mmap.MapView(MapProtection.PageReadWrite, _offset, _size, IntPtr.Zero);

    }
    public IntPtr Allocate(int size)
    {
      throw new NotImplementedException();
    }

    public void Free(IntPtr p, int size)
    {
      throw new NotImplementedException();
    }
  }
}
