using System.Runtime.InteropServices;
using System;

namespace ManagedHell.MMF
{  
  internal static class PosixNative
  {
    [DllImport("libc", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int getpagesize();
    
    [DllImport("librt", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int shm_open(string name, int oflag, int mode);

    [DllImport("librt", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int shm_unlink(string name);    
  }
}
