#if MONO_BUILD
using System;
using System.IO;
using Mono.Unix.Native;

namespace ManagedHell.MMF
{
  public class PosixMemoryMappedFile : AbstractMemoryMappedFile
  {
    private int _fd;
    internal static readonly uint ALLOCATION_GRANULARITY;
    internal static readonly uint PAGE_SIZE;
    
    static PosixMemoryMappedFile()
    {
      PAGE_SIZE = (uint) PosixNative.getpagesize();
      ALLOCATION_GRANULARITY = PAGE_SIZE;
    }
    
    public override uint PageSize { get { return PAGE_SIZE; } }
    public override uint AllocationGranularity { get { return ALLOCATION_GRANULARITY; } }
    
    public PosixMemoryMappedFile(string fileName, long size)
      : base(fileName, size)
    {
      // Ensure our file is "that" big
      using (var fs = new FileStream(fileName, FileMode.OpenOrCreate)) {
        if (fs.Length < size)
          fs.SetLength(size);
      }

      _fd = -1;
      var flags = OpenFlags.O_CREAT | OpenFlags.O_RDWR;
      _fd = Syscall.open(fileName, flags);

      if (_fd < 0)
        throw new MemoryMapException(String.Format("Could not open \"{0}\" for memory mapping", fileName));
    }

    public override IntPtr MapView(MapProtection protection, long offset, long size, IntPtr desiredAddress)
    {
      var unixProt = MmapProts.PROT_NONE;
      if (protection == MapProtection.PageRead)
        unixProt = MmapProts.PROT_READ;
      if (protection == MapProtection.PageReadWrite)
        unixProt = MmapProts.PROT_READ | MmapProts.PROT_WRITE;

      var flags = MmapFlags.MAP_FILE | MmapFlags.MAP_SHARED;

      System.Console.WriteLine("Attempting to map fd={0} @ size={1}", _fd, size);
            
      var baseAddress = Syscall.mmap(desiredAddress, (ulong) size, unixProt, flags, _fd, offset);
      if (baseAddress.ToInt64() <= 0)
        throw new MemoryMapException(String.Format("mmap() failed reason={0}", Mono.Unix.Native.Stdlib.GetLastError()));
      _mappings.Add(baseAddress, size);
      return baseAddress;
    }

    public override void UnMapView(IntPtr mapBaseAddr)
    {
      Mono.Unix.Native.Syscall.munmap(mapBaseAddr, (ulong) _mappings[mapBaseAddr]);
      _mappings.Remove(mapBaseAddr);
    }

    public override void Flush(IntPtr viewBaseAddr, long length)
    {
      Mono.Unix.Native.Syscall.msync(viewBaseAddr, (ulong) _mappings[viewBaseAddr], MsyncFlags.MS_SYNC);
    }
    
    public override void Dispose()
    {
      Dispose(true);
    }

    public override void Dispose(bool disposing)
    {
      if (disposing)
        GC.SuppressFinalize(this);
    }
  }
}
#endif // MONO_BUILD
