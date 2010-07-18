using System;
using System.Collections.Generic;
using System.Text;

namespace ManagedHell.MMF
{
  public static class MemoryMappedFileFactory
  {
    public static IMemoryMappedFile Create(string fileName)
    { return Create(fileName, 0); }
    public static IMemoryMappedFile Create(string fileName, long maxSize)
    {
#if NETFX_BUILD
      // You can either be windows, linux or fucked.
      if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        return new Win32MemoryMappedFile(fileName, maxSize);
#endif // NETFX_BUILD

#if MONO_BUILD
            // Assume we are running in Unix under Mono,
            // so we should have Mono.Posix assembly available to us
            if (Environment.OSVersion.Platform == PlatformID.Unix)
                return new PosixMemoryMappedFile(fileName, maxSize);
#endif // MONO_BUILD
      throw new PlatformNotSupportedException("Only Unix and Windows platforms are supported at this time");
    }

    public static IMemoryMappedFile CreateAnonymous(long maxSize)
    {
#if NETFX_BUILD
      // You can either be windows, linux or fucked.
      if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        return new Win32MemoryMappedFile(null, maxSize);
#endif // NETFX_BUILD

#if MONO_BUILD
            // Assume we are running in Unix under Mono,
            // so we should have Mono.Posix assembly available to us
            if (Environment.OSVersion.Platform == PlatformID.Unix)
                return new PosixMemoryMappedFile(null, maxSize);
#endif // MONO_BUILD
      throw new PlatformNotSupportedException("Only Unix and Windows platforms are supported at this time");
      
    }
  }
}
