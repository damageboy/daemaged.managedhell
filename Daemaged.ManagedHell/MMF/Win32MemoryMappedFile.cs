#if NETFX_BUILD
//
// Win32MemoryMappedFile.cs
//    
//    Implementation of a library to use Win32 Memory Mapped
//    Files from within .NET applications
//

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace Daemaged.ManagedHell.MMF
{
  /// <summary>Wrapper class around the Win32 memory mapping APIs</summary>
  public class Win32MemoryMappedFile : AbstractMemoryMappedFile
  {
    //! handle to Win32MemoryMappedFile object
    private IntPtr _mapHandle = IntPtr.Zero;
    private Win32MapProtection _protection = Win32MapProtection.PageNone;
    internal static readonly uint ALLOCATION_GRANULARITY;
    internal static readonly uint PAGE_SIZE;
    private readonly bool _isAnonymous;

    static Win32MemoryMappedFile()
    {
      SYSTEM_INFO si;
      Win32Native.GetSystemInfo(out si);
      PAGE_SIZE = si.pageSize;
      ALLOCATION_GRANULARITY = si.allocationGranularity;
    }

    private bool IsOpen
    { get { return (_mapHandle != Win32Native.NULL_HANDLE); } }

    public override uint PageSize
    { get { return PAGE_SIZE; } }

    public override uint AllocationGranularity
    { get { return ALLOCATION_GRANULARITY; } }

    public Win32MemoryMappedFile(string fileName, long maxSize, MapProtection mapProtection)
      : base(fileName, maxSize)
    {
      // open file first
      var fileHandle = Win32Native.INVALID_HANDLE_VALUE;

      FileInfo backingFileInfo = null;

      if (!String.IsNullOrEmpty(fileName))
        backingFileInfo = new FileInfo(fileName);

      // Anonymous file mapping?
      _isAnonymous = fileName == null;

      if (String.Empty == fileName)
        throw new ArgumentException("Filename must be specified");

      if (maxSize == 0) {
        if (_isAnonymous || !File.Exists(fileName))
          throw new ArgumentException(
            String.Format("Win32MemoryMappedFile.Create - \"{0}\" does not exist ==> Unable to map entire file",
                          fileName));

        maxSize = backingFileInfo.Length;

        if (maxSize == 0)
          throw new ArgumentException(
            string.Format("Win32MemoryMappedFile.Create - \"{0}\" is zero bytes ==> Unable to map entire file",
                          fileName));
      }

      _maxSize = maxSize;

      // determine file access needed
      // we'll always need generic read access
      var desiredAccess = Win32FileAccess.None;
      switch (mapProtection) {
        case MapProtection.PageRead:
          desiredAccess = Win32FileAccess.GenericRead;
          break;
        case MapProtection.PageReadWrite:
          desiredAccess = Win32FileAccess.GenericRead | Win32FileAccess.GenericWrite;
          break;
      }
      
      string mapName;

      // open or create the file
      // if it doesn't exist, it gets created
      if (!_isAnonymous) {
        fileHandle = Win32Native.CreateFile(
          fileName, (uint)desiredAccess, (uint) (Wind32FileShare.Read),
          IntPtr.Zero, (uint) Win32CreationDisposition.OpenAlways, 0, IntPtr.Zero);


        if (fileHandle == Win32Native.INVALID_HANDLE_VALUE)
          throw new Win32Exception(Marshal.GetLastWin32Error());

        mapName = backingFileInfo.Name;
      }
      else mapName = "Anonymous";


      // We always create a read-write mapping object
      // the individual map obtained through MapView will be able to restrict
      // the access
      var win32MapProtection = Win32MapProtection.PageReadOnly;
      switch (mapProtection) {
        case MapProtection.PageRead:
          win32MapProtection = Win32MapProtection.PageReadOnly;
          break;
        case MapProtection.PageReadWrite:
          win32MapProtection = Win32MapProtection.PageReadWrite;
          break;
        case MapProtection.PageWriteCopy:
          win32MapProtection = Win32MapProtection.PageWriteCopy;
          break;
       }

      _mapHandle = Win32Native.CreateFileMapping(
        fileHandle, IntPtr.Zero, (int) win32MapProtection,
        (int) ((maxSize >> 32) & 0xFFFFFFFF),
        (int) (maxSize & 0xFFFFFFFF), mapName);

      // close file handle, we don't need it
      if (fileHandle != Win32Native.INVALID_HANDLE_VALUE) 
        Win32Native.CloseHandle(fileHandle);
      if (_mapHandle == Win32Native.NULL_HANDLE)
        throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    ~Win32MemoryMappedFile()
    { Dispose(false); }

    public override IntPtr MapView(MapProtection protection, long offset, long size, IntPtr desiredAddress)
    {
      if (!IsOpen)
        throw new ObjectDisposedException("Win32MemoryMappedFile already closed!");

      var mapSize = new IntPtr(size);

      var win32Access = Win32FileMapAccessType.Unspecified;

      switch (protection) {
        case MapProtection.PageRead:
          win32Access = Win32FileMapAccessType.Read;
          break;
        case MapProtection.PageReadWrite:
          win32Access = Win32FileMapAccessType.Write;
          break;
        case MapProtection.PageWriteCopy:
          win32Access = Win32FileMapAccessType.Copy;
          break;

      }

      var baseAddress = Win32Native.MapViewOfFileEx(
        _mapHandle, win32Access,
        (uint)((offset >> 32) & 0xFFFFFFFF),
        (uint)(offset & 0xFFFFFFFF), 
        new UIntPtr((ulong) mapSize), desiredAddress);

      if (baseAddress == IntPtr.Zero)
        throw new Win32Exception(Marshal.GetLastWin32Error());

      _mappings.Add((ulong) baseAddress, size);

      return baseAddress;
    }

    public override void UnMapView(IntPtr mapBaseAddr)
    {
      _mappings.Remove((ulong) mapBaseAddr);
      Win32Native.UnmapViewOfFile(mapBaseAddr);      
    }

    public override void Flush(IntPtr viewBaseAddr, long length)
    {
      var flushLength = new IntPtr(length);
      Win32Native.FlushViewOfFile(viewBaseAddr, flushLength);
    }

    public override void Dispose()
    { Dispose(true); }

    public override void Dispose(bool disposing)
    {
      UnmapAll();
      
      if (IsOpen)
        Win32Native.CloseHandle(_mapHandle);
      
      _mapHandle = Win32Native.NULL_HANDLE;

      if (disposing)
        GC.SuppressFinalize(this);
    }
  }
}
#endif // NETFX_BUILD
