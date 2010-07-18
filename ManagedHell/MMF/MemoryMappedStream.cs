using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using ManagedHell.MMF;

namespace ManagedHell.MMF
{
  /// <summary>
  ///   Allows you to read/write from/to
  ///   a view of a memory mapped file.
  /// </summary>
  public class MemoryMappedStream : Stream, IDisposable
  {
    #region Consants
    public const long DEF_ALLOC_GRANULARITY = 0x20000;
    public const long DEF_VIEW_SIZE = 32 * 1024 * 1024;
    public const long MIN_VIEW_SIZE = DEF_ALLOC_GRANULARITY;
    public const long MB = 1024*1024;
    protected static readonly long MAX_VIEW_SIZE;
    #endregion

    #region Private Fields
    //! our current position in the stream buffer
    private long _position = 0;
    // Pointer to the base address of the currently mapped view
    private IntPtr _viewBaseAddr = IntPtr.Zero;
    private bool _isOpen = false;
    #endregion

    #region Protected Fields
    protected IMemoryMappedFile _backingFile = null;
    protected MapProtection _access = MapProtection.PageReadWrite;
    protected bool _isWriteable;
    protected long _viewPosition = long.MaxValue;
    protected long _mapSize = 0;
    protected long _viewStartIdx = long.MaxValue;
    protected long _desiredViewSize = DEF_VIEW_SIZE;
    protected long _allocGranularity = DEF_ALLOC_GRANULARITY;
    protected long _viewSize = long.MaxValue;
    protected long _mapStartIdx = 0;
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
        viewSize = viewSize + positionAdjustment;
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
    static MemoryMappedStream()
    {
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
    /// <param name="mapSize">Size of the view, in bytes.</param>
    /// <param name="isWriteable">True if Read/Write access is desired, False otherwise</param>
    /// <param name="viewSize">The desired view size</param>
    public MemoryMappedStream(IMemoryMappedFile backingFile, long mapStartIdx, long mapSize, bool isWriteable, long viewSize)
    {
      if (backingFile == null) {
        throw new ArgumentException("backingFile is null");
      }
      if ((mapStartIdx < 0) || (mapStartIdx > (long)backingFile.MaxSize))
        throw new ArgumentException(
          String.Format("mapStartIdx is invalid.  mapStartIdx=={0}, backingFile.MaxSize=={1}", 
                        mapStartIdx, backingFile.MaxSize));
     
      if ((mapSize < 1) || (((mapStartIdx) + mapSize) > (long)backingFile.MaxSize))
        throw new ArgumentException(
          String.Format("mapSize is invalid.  mapStartIdx=={0}, mapSize=={1}, backingFile.MaxSize=={2}", 
                        mapStartIdx, mapSize, backingFile.MaxSize));
     
      if ((viewSize < MIN_VIEW_SIZE) || (viewSize > MAX_VIEW_SIZE))
        throw new ArgumentException(
          String.Format("viewSize is invalid.  viewSize=={0}", viewSize));

      _backingFile = backingFile;
      _isWriteable = isWriteable;
      _access = isWriteable ? MapProtection.PageReadWrite : MapProtection.PageRead;

      _desiredViewSize = viewSize;
      _mapStartIdx = mapStartIdx;
      _mapSize = (long)mapSize;

      _isOpen = true;

      // Map the first view
      Seek(0, SeekOrigin.Begin);
    }

    public MemoryMappedStream(IMemoryMappedFile backingFile, long mapStartIdx, long mapSize, bool isWriteable) :
      this(backingFile, mapStartIdx, mapSize, isWriteable, DEF_VIEW_SIZE)
    {
    }
    #endregion

    #region Stream Overrides
    
    #region Stream Properties
    public override bool CanRead { get { return true; } }
    public override bool CanSeek { get { return true; } }
    public override bool CanWrite { get { return _isWriteable; } }
    public override long Length { get { return _mapSize; } }
    public override long Position
    {
      get { return _position; }
      set { Seek(value, SeekOrigin.Begin); }
    }
    #endregion // Stream Properties

    #region Stream Methods

    /// <summary>
    /// When overridden in a derived class, clears all buffers for this stream and causes any buffered data to be written to the underlying device.
    /// </summary>
    /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    public override void Flush()
    {
      if (!IsOpen)
        throw new ObjectDisposedException("Stream is closed");

      // flush the view but leave the buffer intact
      _backingFile.Flush(_viewBaseAddr, _viewSize);
    }

    /// <summary>
    /// When overridden in a derived class, reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
    /// </summary>
    /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between <paramref name="offset"/> and (<paramref name="offset"/> + <paramref name="count"/> - 1) replaced by the bytes read from the current source.</param>
    /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin storing the data read from the current stream.</param>
    /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
    /// <returns>
    /// The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
    /// </returns>
    /// <exception cref="T:System.ArgumentException">The sum of <paramref name="offset"/> and <paramref name="count"/> is larger than the buffer length. </exception>
    /// <exception cref="T:System.ArgumentNullException">
    /// 	<paramref name="buffer"/> is null. </exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    /// 	<paramref name="offset"/> or <paramref name="count"/> is negative. </exception>
    /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    /// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
    /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
    public override int Read(byte[] buffer, int offset, int count)
    {
      if (!IsOpen)
        throw new ObjectDisposedException("Stream is closed");

      if (buffer.Length - offset < count)
        throw new ArgumentException("Invalid Offset");

      long bytesToRead = Math.Min(Length - _position, count);
      long numBytesRemainingInCurMap = ViewSize - _viewPosition;

      if (bytesToRead <= numBytesRemainingInCurMap) {
        // Required data is contained completely in currently mapped view so Read data from map
        Marshal.Copy((IntPtr)(_viewBaseAddr.ToInt64() + _viewPosition), buffer, offset, (int)bytesToRead);
        _viewPosition += bytesToRead;
        _position += bytesToRead;
      } else {
        // Required data is only partly contained in currently mapped view ==> remap required
        long bytesToReadInCurMap = numBytesRemainingInCurMap;
        long bytesToReadInLastReMap = (bytesToRead - numBytesRemainingInCurMap) % ViewSize;
        int numReMapsReqd = (int)((bytesToRead - bytesToReadInCurMap) / ViewSize) + ((bytesToReadInLastReMap != 0) ? 2 : 1);

        for (int i = 0; i < numReMapsReqd; i++) {
          // Read data from map
          if (i != 0) {
            if ((bytesToReadInLastReMap > 0) && (i == (numReMapsReqd - 1)))
              bytesToReadInCurMap = bytesToReadInLastReMap;
            else
              bytesToReadInCurMap = ViewSize;
          }
          Marshal.Copy((IntPtr)(_viewBaseAddr.ToInt64() + _viewPosition), buffer, offset, (int)bytesToReadInCurMap);
          _position += bytesToReadInCurMap;
          _viewPosition += bytesToReadInCurMap;
          offset += (int)bytesToReadInCurMap;
          // Remap
          Seek(ViewStopIdx + 1, SeekOrigin.Begin, true, false);
        }
      }

      return (int)bytesToRead;
    }

    /// <summary>
    /// When overridden in a derived class, writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
    /// </summary>
    /// <param name="buffer">An array of bytes. This method copies <paramref name="count"/> bytes from <paramref name="buffer"/> to the current stream.</param>
    /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin copying bytes to the current stream.</param>
    /// <param name="count">The number of bytes to be written to the current stream.</param>
    /// <exception cref="T:System.ArgumentException">The sum of <paramref name="offset"/> and <paramref name="count"/> is greater than the buffer length. </exception>
    /// <exception cref="T:System.ArgumentNullException">
    /// 	<paramref name="buffer"/> is null. </exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    /// 	<paramref name="offset"/> or <paramref name="count"/> is negative. </exception>
    /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    /// <exception cref="T:System.NotSupportedException">The stream does not support writing. </exception>
    /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
    public override void Write(byte[] buffer, int offset, int count)
    {
      if (!IsOpen)
        throw new ObjectDisposedException("Stream is closed");

      if (!CanWrite)
        throw new MemoryMapException("Stream cannot be written to");

      if (buffer.Length - offset < count)
        throw new ArgumentException("Invalid Offset");

      long bytesToWrite = Math.Min(Length - _position, count);

      if (bytesToWrite == 0)
        return;

      long numBytesRemainingInCurMap = ViewSize - _viewPosition;

      if (bytesToWrite <= numBytesRemainingInCurMap) {
        // Data is contained completely in currently mapped view so process with write
        Marshal.Copy(buffer, offset, (IntPtr)(_viewBaseAddr.ToInt64() + _viewPosition), (int)bytesToWrite);
        _viewPosition += bytesToWrite;
        _position += bytesToWrite;
      } else {
        // Data is only partly contained in currently mapped view ==> remap required
        long bytesToWriteInCurMap = numBytesRemainingInCurMap;
        long bytesToWriteInLastReMap = (bytesToWrite - numBytesRemainingInCurMap) % ViewSize;
        int numReMapsReqd = (int)((bytesToWrite - bytesToWriteInCurMap) / ViewSize + ((bytesToWriteInLastReMap != 0) ? 2 : 1));

        for (int i = 0; i < numReMapsReqd; i++) {
          // Write data to map
          if (i != 0) {
            if ((bytesToWriteInLastReMap > 0) && (i == (numReMapsReqd - 1)))
              bytesToWriteInCurMap = bytesToWriteInLastReMap;
            else
              bytesToWriteInCurMap = ViewSize;
          }
            
          Marshal.Copy(buffer, offset, (IntPtr)(_viewBaseAddr.ToInt64() + _viewPosition), (int)bytesToWriteInCurMap);
          _position += bytesToWriteInCurMap;
          _viewPosition += bytesToWriteInCurMap;
          offset += (int)bytesToWriteInCurMap;
          // Remap
          Seek(ViewStopIdx + 1, SeekOrigin.Begin, true, false);
        }
      }
    }

    /// <summary>
    /// Seeks the specified offset.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="origin">The origin.</param>
    /// <param name="ForceRemap">if set to <c>true</c> [force remap].</param>
    /// <param name="CentreRemap">if set to <c>true</c> [centre remap].</param>
    /// <returns></returns>
    public long Seek(long offset, SeekOrigin origin, bool ForceRemap, bool CentreRemap)
    {
      long newpos = 0;
      switch (origin) {
        case SeekOrigin.Begin: newpos = offset; break;
        case SeekOrigin.Current: newpos = Position + offset; break;
        case SeekOrigin.End: newpos = Length + offset; break;
      }

      // sanity check
      if (newpos < 0 || newpos > Length)
        throw new MemoryMapException("Invalid Seek Offset");

      // Check if we need to remap view
      if (ForceRemap || (newpos < ViewStartIdx) || (newpos > ViewStopIdx) || !IsViewMapped) {
        if (CentreRemap) {
          long viewCentreIdx = newpos;
          MapCentredView(ref viewCentreIdx);
        } else {
          long viewStartIdx = newpos;
          MapView(ref viewStartIdx);
        }
      }
      _position = newpos;
      _viewPosition = _position - ViewStartIdx;
      return newpos;
    }

    /// <summary>
    /// When overridden in a derived class, sets the position within the current stream.
    /// </summary>
    /// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter.</param>
    /// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin"/> indicating the reference point used to obtain the new position.</param>
    /// <returns>
    /// The new position within the current stream.
    /// </returns>
    /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    /// <exception cref="T:System.NotSupportedException">The stream does not support seeking, such as if the stream is constructed from a pipe or console output. </exception>
    /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
    public override long Seek(long offset, SeekOrigin origin)
    { return Seek(offset, origin, false, true); }

    /// <summary>
    /// When overridden in a derived class, sets the length of the current stream.
    /// </summary>
    /// <param name="value">The desired length of the current stream in bytes.</param>
    /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    /// <exception cref="T:System.NotSupportedException">The stream does not support both writing and seeking, such as if the stream is constructed from a pipe or console output. </exception>
    /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
    public override void SetLength(long value)
    { throw new NotSupportedException("Can't change map size"); }

    /// <summary>
    /// Closes the current stream and releases any resources (such as sockets and file handles) associated with the current stream.
    /// </summary>
    public override void Close()
    { Dispose(true); }

    #endregion // Stream methods
    
    #endregion

    #region IDisposable Implementation
    public bool IsOpen { get { return _isOpen; } }

    public new void Dispose()
    {
      Dispose(true);
    }

    protected new virtual void Dispose(bool disposing)
    {
      if (IsOpen) {
        Flush();
        UnmapView();
        _isOpen = false;
      }

      if (disposing)
        GC.SuppressFinalize(this);
    }

    ~MemoryMappedStream()
    {
      Dispose(false);
    }

    #endregion // IDisposable Implementation

    #region Public Properties
    public IntPtr ViewBaseAddr { get { return _viewBaseAddr; } }
    public long ViewStartIdx { get { return _viewStartIdx; } }
    public long ViewStopIdx { get { return (_viewStartIdx + _viewSize - 1); } }
    public long ViewSize { get { return _viewSize; } }
    public long ViewPosition { get { return _viewPosition; } }
    public bool IsViewMapped { get { return (_viewStartIdx >= _mapStartIdx) && ((_viewStartIdx + _viewSize) <= (_mapStartIdx + _mapSize)); } }
    #endregion

  }
}