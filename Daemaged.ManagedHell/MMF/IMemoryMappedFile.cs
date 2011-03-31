using System;
using System.Collections.Generic;

namespace Daemaged.ManagedHell.MMF
{
  public enum MapProtection
  {
    PageRead,
    PageReadWrite,
    PageWriteCopy,
  }

  public interface IMemoryMappedFile : IDisposable
  {
    /// <summary>
    /// The filename of the file backing this memory map
    /// </summary>
    string FileName { get; }

    /// <summary>
    /// The maximal size that can be accessed through this mapping object
    /// </summary>
    long MaxSize { get; }

    /// <summary>
    /// The minimal size that will be paged in when ever a page fault occurs
    /// </summary>
    uint PageSize { get; }

    /// <summary>
    /// The minimal allocation granularity as defined for this type of file/device/os
    /// </summary>
    uint AllocationGranularity { get; }

    /// <summary>
    ///   Close this File Mapping object
    ///   From here on, You can't do anything with it
    ///   but the open views remain valid.
    /// </summary>
    void Close();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="protection"></param>
    /// <param name="offset"></param>
    /// <param name="size"></param>
    /// <param name="desiredAddress"></param>
    /// <returns></returns>
    IntPtr MapView(MapProtection protection, long offset, long size, IntPtr desiredAddress);

    /// <summary>
    ///   Map a view of the file mapping object
    ///   This returns a stream, giving you easy access to the memory,
    ///   as you can use StreamReaders and StreamWriters on top of it
    /// </summary>
    void UnMapView(IntPtr mapBaseAddr);
    void Flush(IntPtr viewBaseAddr, long length);
  }

  //public class IntPtrComparer: IComparer<IntPtr>
  //{
  //  public int Compare(IntPtr x, IntPtr y)
  //  {
  //    long result = x.ToInt64() - y.ToInt64();
  //    return result > 0 ? 1 : result < 0 ? -1 : 0;
  //  }
  //}

  public abstract class AbstractMemoryMappedFile : IMemoryMappedFile
  {
    private readonly string _fileName;
    protected long _maxSize;
    protected SortedList<ulong, long> _mappings;

    protected AbstractMemoryMappedFile(string fileName, long maxSize)
    {
      _fileName = fileName;
      _maxSize = maxSize;

      _mappings = new SortedList<ulong, long>(Comparer<ulong>.Default);
    }

    public string FileName { get { return _fileName;  } }
    public long MaxSize { get { return _maxSize; } }

    public abstract uint PageSize { get; }

    public abstract uint AllocationGranularity { get; }


    protected void UnmapAll()
    {
      ulong[] tmp = new ulong[_mappings.Count];
      _mappings.Keys.CopyTo(tmp, 0);
      foreach (var p in tmp) {
        UnMapView((IntPtr) p);
      }
    }
    /// <summary>
    ///   Close this File Mapping object
    ///   From here on, You can't do anything with it
    ///   but the open views remain valid.
    /// </summary>
    public virtual void Close()
    { Dispose(true); }

    public abstract IntPtr MapView(MapProtection protection, long offset, long size, IntPtr desiredAddress);
    public abstract void UnMapView(IntPtr mapBaseAddr);
    public abstract void Flush(IntPtr viewBaseAddr, long length);

    public abstract void Dispose();
    public abstract void Dispose(bool disposing);
  }
}