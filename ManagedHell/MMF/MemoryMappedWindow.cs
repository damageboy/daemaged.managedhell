using System;
using System.Diagnostics;
using System.IO;

namespace ManagedHell.MMF
{
  /// <summary>
  /// Provide with a partial window into a memory mapped file
  /// and the functionality to "slide" the window back and forth
  /// at the user's request
  /// </summary>
  public class MemoryMappedWindow : IDisposable
  {
    public static readonly long DEF_ALLOC_GRANULARITY;
    public static readonly long DEF_VIEW_SIZE;
    public static readonly long MIN_VIEW_SIZE;
    public const long MB = 1024 * 1024;
    private const MapProtection DEFAULT_PROTECTION = MapProtection.PageRead;
    protected static readonly long MAX_VIEW_SIZE;

    #region Private Fields
    // Pointer to the base address of the currently mapped view
    private IntPtr _viewBaseAddr;
    private bool _isOpen;
    #endregion

    #region Protected Fields
    protected IMemoryMappedFile _backingFile;
    protected MapProtection _access = DEFAULT_PROTECTION;
    protected bool _isWriteable;
    protected long _viewPosition;
    protected long _mapSize = 0;
    protected long _viewStartIdx = long.MaxValue;
    protected long _desiredViewSize = DEF_VIEW_SIZE;
    protected long _allocGranularity = DEF_ALLOC_GRANULARITY;
    protected long _viewSize = long.MaxValue;
    protected long _mapStartIdx = 0;
    private bool _isMMFOwned;
    protected long MinMapStartIdx { get { return (_mapStartIdx / _allocGranularity) * _allocGranularity; } }
    #endregion


    #region Map / Unmap View
    #region Unmap View
    protected void UnmapView()
    {
      if (IsViewMapped) {
        _backingFile.UnMapView(_viewBaseAddr);
        _viewStartIdx = long.MaxValue;
        _viewSize = long.MaxValue;
        _viewPosition = long.MaxValue;
      }
    }
    #endregion
    #region Map View
    protected void MapView(ref long viewStartIdx, ref long viewSize)
    {
      if ((viewStartIdx < _mapStartIdx) || (viewStartIdx > (_mapStartIdx + _mapSize)))
        throw new ArgumentException
          (String.Format("viewStartIdx is invalid.  viewStartIdx=={0}, _mapStartIdx=={1}, _mapSize=={2}",
                         viewStartIdx, _mapStartIdx, _mapSize));
      
      if ((viewSize < 1) || (viewSize > _desiredViewSize))
        throw new ArgumentException(
          String.Format("viewSize is invalid.  viewSize=={0}, _desiredViewSize=={1}", 
                        viewSize, _desiredViewSize));

      // Trim End
      if ((viewStartIdx + viewSize) > (_mapStartIdx + _mapSize))
        viewSize = (_mapStartIdx + _mapSize) - viewStartIdx;

      long positionAdjustment = viewStartIdx % _allocGranularity;

      if (positionAdjustment != 0) {
        //viewSize = viewSize + positionAdjustment;
        viewStartIdx = viewStartIdx - positionAdjustment;
      }

      // Unmap existing view if different from this view..
      if (IsViewMapped && ((viewStartIdx != _viewStartIdx) || (viewSize != _viewSize)))
        UnmapView();
      
      // Now map the view
      _viewBaseAddr = _backingFile.MapView(_access, viewStartIdx, viewSize, IntPtr.Zero);
      _viewStartIdx = viewStartIdx;
      _viewSize = viewSize;
    }

    protected void MapView(ref long viewStartIdx)
    {
      long viewSize = _desiredViewSize;
      MapView(ref viewStartIdx, ref viewSize);
    }

    protected void MapCentredView(ref long viewCentreIdx, ref long viewSize)
    {
      if ((viewCentreIdx < _mapStartIdx) || (viewCentreIdx > (_mapStartIdx + _mapSize)))
        throw new ArgumentException(
          String.Format("viewCentreIdx is invalid.  viewCentreIdx=={0}, _mapStartIdx=={1}, _mapSize=={2}",
                        viewCentreIdx, _mapStartIdx, _mapSize));
      
      if ((viewSize < 1) || (viewSize > _desiredViewSize))
        throw new ArgumentException(
          String.Format("viewSize is invalid.  viewSize=={0}, _desiredViewSize=={1}", 
                        viewSize, _desiredViewSize));

      // Centre
      long viewStartIdx = viewCentreIdx - (viewSize / 2);

      // Trim Start
      if (viewStartIdx < _mapStartIdx)
        viewStartIdx = _mapStartIdx;

      // Trim End
      if ((viewStartIdx + viewSize) > (_mapStartIdx + _mapSize))
        viewSize = (_mapStartIdx + _mapSize) - viewStartIdx;

      MapView(ref viewStartIdx, ref viewSize);

      // Sanity check..
      Debug.Assert(viewStartIdx >= _mapStartIdx);
      Debug.Assert((viewStartIdx + viewSize) <= (_mapStartIdx + _mapSize));
      Debug.Assert((viewStartIdx <= viewCentreIdx) && (viewCentreIdx <= (viewStartIdx + viewSize)));
      // Assign refs
      viewCentreIdx = viewStartIdx + (viewSize / 2);
    }

    protected void MapCentredView(ref long viewCentreIdx)
    {
      long viewSize = _desiredViewSize;
      MapCentredView(ref viewCentreIdx, ref viewSize);
    }
    #endregion
    #endregion

    #region Constructors
    /// <summary>
    /// Initialize some static runtime constans
    /// </summary>
    static MemoryMappedWindow()
    {
      // Defined by the system... (Win32 for now...?)
#if MONO_BUILD
      DEF_ALLOC_GRANULARITY = PosixMemoryMappedFile.ALLOCATION_GRANULARITY;
#endif
#if NETFX_BUILD
      DEF_ALLOC_GRANULARITY = Win32MemoryMappedFile.ALLOCATION_GRANULARITY;
#endif

      
      // Must be at LEAST twice the size of the allocation granularity...
      // so that we can remap a window that will contain a pointer from the last
      // windows into a new window
      MIN_VIEW_SIZE = 2*DEF_ALLOC_GRANULARITY;  
      DEF_VIEW_SIZE = 32*MIN_VIEW_SIZE; // For he said "32 is a good number"

      // For 32 bit systems we limit the max size to just below 2GB,
      // which is the loweset common denominator between Linux / Windows in 32 bit:
      // Linux defaults to 3GB/1GB user/kernel space split
      // Windows defaults to 2GB/2GB user/kernel space split
      if (IntPtr.Size == 4)
        MAX_VIEW_SIZE = (2048 - 512) * MB;

      // We're in 64 bit land, just use something propostrous for now 
      if (IntPtr.Size == 8)
        MAX_VIEW_SIZE = MB * MB;


    }

    /// <summary>
    /// Constructor used internally by MemoryMappedFile.
    /// </summary>
    /// <param name="backingFile">Preconstructed MemoryMappedFile</param>
    /// <param name="mapStartIdx">Index in the backingFile at which the view starts</param>
    /// <param name="isWriteable">True if Read/Write access is desired, False otherwise</param>
    public MemoryMappedWindow(IMemoryMappedFile backingFile, long mapStartIdx, bool isWriteable) :
      this(backingFile, mapStartIdx, 0, isWriteable, DEF_VIEW_SIZE)
    { }

    /// <summary>
    /// Constructor used internally by MemoryMappedFile.
    /// </summary>
    /// <param name="backingFile">Preconstructed MemoryMappedFile</param>
    /// <param name="isWriteable">True if Read/Write access is desired, False otherwise</param>
    public MemoryMappedWindow(IMemoryMappedFile backingFile, bool isWriteable) :
      this(backingFile, 0, 0, isWriteable, DEF_VIEW_SIZE)
    { }

    /// <summary>
    /// Constructor used internally by MemoryMappedFile.
    /// </summary>
    /// <param name="backingFile">Preconstructed MemoryMappedFile</param>
    /// <param name="mapStartIdx">Index in the backingFile at which the view starts</param>
    /// <param name="mapSize">Size of the view, in bytes.</param>
    /// <param name="isWriteable">True if Read/Write access is desired, False otherwise</param>
    public MemoryMappedWindow(IMemoryMappedFile backingFile, long mapStartIdx, long mapSize, bool isWriteable) :
      this(backingFile, mapStartIdx, mapSize, isWriteable, DEF_VIEW_SIZE)
    { }

    /// <summary>
    /// Constructor used internally by MemoryMappedFile.
    /// </summary>
    /// <param name="backingFile">Preconstructed MemoryMappedFile</param>
    /// <param name="mapStartIdx">Index in the backingFile at which the view starts</param>
    /// <param name="mapSize">Size of the view, in bytes.</param>
    /// <param name="isWriteable">True if Read/Write access is desired, False otherwise</param>
    /// <param name="viewSize">The desired view size</param>
    public MemoryMappedWindow(IMemoryMappedFile backingFile, long mapStartIdx, long mapSize, bool isWriteable, long viewSize)
    {
      SetupMapping(backingFile, mapStartIdx, mapSize, viewSize, isWriteable);
      _isMMFOwned = false;
    }

    /// <summary>
    /// Constructor used internally by MemoryMappedFile.
    /// </summary>
    /// <param name="fileName">The name of the file to map</param>
    /// <param name="mapStartIdx">Index in the backingFile at which the view starts</param>
    /// <param name="isWriteable">True if Read/Write access is desired, False otherwise</param>
    public MemoryMappedWindow(string fileName, long mapStartIdx, bool isWriteable) :
      this(fileName, mapStartIdx, 0, isWriteable, DEF_VIEW_SIZE)
    { }

    /// <summary>
    /// Constructor used internally by MemoryMappedFile.
    /// </summary>
    /// <param name="fileName">The name of the file to map</param>
    /// <param name="isWriteable">True if Read/Write access is desired, False otherwise</param>
    public MemoryMappedWindow(string fileName, bool isWriteable) :
      this(fileName, 0, 0, isWriteable, DEF_VIEW_SIZE)
    { }


    /// <summary>
    /// Constructor used internally by MemoryMappedFile.
    /// </summary>
    /// <param name="fileName">The name of the file to map</param>
    /// <param name="mapStartIdx">Index in the backingFile at which the view starts</param>
    /// <param name="mapSize">Size of the view, in bytes.</param>
    /// <param name="isWriteable">True if Read/Write access is desired, False otherwise</param>
    public MemoryMappedWindow(string fileName, long mapStartIdx, long mapSize, bool isWriteable) :
      this(fileName, mapStartIdx, mapSize, isWriteable, DEF_VIEW_SIZE)
    {}

    /// <summary>
    /// Constructor used internally by MemoryMappedFile.
    /// </summary>
    /// <param name="fileName">The name of the file to map</param>
    /// <param name="mapStartIdx">Index in the backingFile at which the view starts</param>
    /// <param name="mapSize">Size of the view, in bytes.</param>
    /// <param name="isWriteable">True if Read/Write access is desired, False otherwise</param>
    /// <param name="viewSize">The desired view size</param>
    public MemoryMappedWindow(string fileName, long mapStartIdx, long mapSize, bool isWriteable, long viewSize)
    {
      IMemoryMappedFile mmf = MemoryMappedFileFactory.Create(fileName, mapSize);
      SetupMapping(mmf, mapStartIdx, mapSize, viewSize, isWriteable);
      _isMMFOwned = true;
    }

    private void SetupMapping(IMemoryMappedFile backingFile, long mapStartIdx, long mapSize, long viewSize, bool isWriteable)
    {

      if (backingFile == null)
        throw new ArgumentException("backingFile is null");

      if (mapSize == 0)
        mapSize = (long)backingFile.MaxSize;


      if ((mapStartIdx < 0) || (mapStartIdx > (long)backingFile.MaxSize))
        throw new ArgumentException(
          String.Format("mapStartIdx is invalid. mapStartIdx=={0}, backingFile.MaxSize=={1}",
                        mapStartIdx, backingFile.MaxSize));

      if ((mapSize < 0) || (((mapStartIdx) + mapSize) > (long)backingFile.MaxSize))
        throw new ArgumentException(
          String.Format("mapSize is invalid. mapStartIdx=={0}, mapSize=={1}, backingFile.MaxSize=={2}",
                        mapStartIdx, mapSize, backingFile.MaxSize));

      if ((viewSize < MIN_VIEW_SIZE) || (viewSize > MAX_VIEW_SIZE))
        throw new ArgumentException(
          String.Format("viewSize is invalid. viewSize=={0}", viewSize));

      _backingFile = backingFile;
      _isWriteable = isWriteable;
      _access = isWriteable ? MapProtection.PageReadWrite : MapProtection.PageRead;

      _viewBaseAddr = IntPtr.Zero;
      _viewPosition = long.MaxValue;
      _isOpen = false;

      _desiredViewSize = viewSize;
      _mapStartIdx = mapStartIdx;
      _mapSize = (long)mapSize;

      _isOpen = true;

      // Map the first view
      Slide(0, SeekOrigin.Begin);
    }
    #endregion

    /// <summary>
    /// When overridden in a derived class, clears all buffers for this stream and causes any buffered data to be written to the underlying device.
    /// </summary>
    /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    public void Flush()
    {
      if (!IsOpen)
        throw new ObjectDisposedException("Stream is closed");

      // flush the view but leave the buffer intact
      _backingFile.Flush(_viewBaseAddr, _viewSize);
    }

    public long Slide(long offset, SeekOrigin origin)
    { return Slide(offset, origin, false); }

    /// <summary>
    /// Seeks the specified offset.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="origin">The origin.</param>
    /// <param name="CentreRemap">if set to <c>true</c> [centre remap].</param>
    /// <returns></returns>
    public long Slide(long offset, SeekOrigin origin, bool CentreRemap)
    {
      long newpos = 0;
      long viewStartIdx = 0;
      switch (origin) {
        case SeekOrigin.Begin:
          newpos = offset;
          break;
        case SeekOrigin.Current:
          newpos = _viewStartIdx + offset;
          break;
        case SeekOrigin.End:
          newpos = Length + offset;
          break;
      }

      // sanity check
      if (newpos < 0 || newpos > Length)
        throw new MemoryMapException("Invalid Seek Offset");

      // Check if we need to remap view
      if (CentreRemap) {
        long viewCentreIdx = newpos;
        MapCentredView(ref viewCentreIdx);
      }
      else {
        viewStartIdx = newpos;
        MapView(ref viewStartIdx);
      }
      _viewPosition = newpos - viewStartIdx;
      return _viewPosition;
    }

    public long Length { get { return _mapSize; } }

    #region IDisposable Implementation   
    public bool IsOpen { get { return _isOpen; } }

    public void Dispose()
    {
      Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
      if (IsOpen) {
        UnmapView();
        _isOpen = false;
      }

      if (_isMMFOwned)
        _backingFile.Close();
      
      if (disposing)
        GC.SuppressFinalize(this);
    }

    ~MemoryMappedWindow()
    {
      Dispose(false);
    }
    #endregion // IDisposable Implementation

    #region Public Properties
    public IntPtr ViewBaseAddr { get { return _viewBaseAddr; } }
    public unsafe byte *ViewBasePtr { get { return (byte *) _viewBaseAddr.ToPointer(); } }
    public long ViewStartIdx { get { return _viewStartIdx; } }
    public long ViewStopIdx { get { return (_viewStartIdx + _viewSize - 1); } }
    public long ViewSize { get { return _viewSize; } }
    public long ViewPosition { get { return _viewPosition; } }
    public bool IsViewMapped { 
      get { 
        return (_viewStartIdx != long.MaxValue) && 
               (_viewStartIdx >= _mapStartIdx) && 
               ((_viewStartIdx + _viewSize) <= (_mapStartIdx + _mapSize)); 
      } 
    }
    #endregion

  }
}
