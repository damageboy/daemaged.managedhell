using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ManagedHell
{
  public class BluePill
  {
    public static readonly int PrologueSize = IntPtr.Size;
    public static readonly int PrologueArraySize = PrologueSize * 2;
  }

  public class BluePill<T> : BluePill
  {
    private static readonly Type _type;
    private static readonly Type _elementType;
    private static readonly IntPtr _rtth;
    private static readonly int _size;
    private static readonly int _elementSize;
    private static IAllocator _defaultAllocator;

    static BluePill()
    {
      _type = typeof(T);
      _rtth = _type.TypeHandle.Value;

      if (_type.IsArray) {
        _elementType = _type.GetElementType();
        if (!_elementType.IsPrimitive)
          throw new ArgumentException("Cannot use BluePill on non primitive arrays");
        _elementSize = Marshal.SizeOf(_elementType);
      }

      if (_type.IsArray || _type.IsPrimitive)
        return;
      if (!_type.IsLayoutSequential && !_type.IsExplicitLayout)
        throw new ArgumentException("Cannot use BluePill on non primitive types WITHOUT Explicit/Sequential layout");

      _size = Marshal.SizeOf(_type) + PrologueSize;
    }

    public static IAllocator DefaultAllocator
    {
      get { return _defaultAllocator; }
      set
      {
        if (_defaultAllocator != null)
          throw new InvalidOperationException("Allocator cannot be set twice");

        _defaultAllocator = value;

      }
    }

    /// <summary>
    /// Get a live unsafe pointer to a reference
    /// </summary>
    /// <remarks>The pointer is acquired without pinning the memory therefore this is a very unsafe 
    /// method to use on native .NET object, use this only on referenced obtained through BluePill</remarks>
    /// <param name="o"> The reference</param>
    /// <returns>An unsafe pointer to the objects memory</returns>
    public static unsafe void* ToPointer(T o)
    {
      if (_type.IsValueType)
        throw new ArgumentException("Cannot cast a pointer to an object into an array");

      var x = o;
      IntPtr *marker;
      var pmarker = (&marker) - 1;
      return (*pmarker) + 1;
    }

    /// <summary>
    /// "Converts" a pointer pointing to unmanaged memory to a managed reference to a class of type T
    /// </summary>
    /// <param name="p">the pointer to the unmanaged memory</param>
    /// <remarks>The pointer must point to a memory area with at least ProgolgueSize 
    /// allocated bytes BEFORE the pointer passed to this function</remarks>

    /// <returns>A managed reference to T</returns>
    public static unsafe T FromPointer(void* p)
    {
      if (_type.IsArray)
        throw new ArgumentException("Cannot cast a pointer to an object into an array");

      var poof = default(T);
      IntPtr marker;

      var rtthPtr = (IntPtr *) (((byte*)p) - PrologueSize);
      *rtthPtr = _rtth;

      var pmarker = (&marker) - 1;
      *pmarker = (IntPtr) rtthPtr;
      return poof;
    }
    
    /// <summary>
    /// "Converts" a pointer pointing to an unmanaged array, to a managed reference to a primitive array
    /// </summary>
    /// <param name="p">the pointer to the unmanaged memory</param>
    /// <param name="numElements">Number of elements in the array</param>
    /// <remarks>The pointer must point to a memory area with at least ProgolgueArraySize 
    /// allocated bytes BEFORE the pointer passed to this function</remarks>
    /// <returns>The Primitive Array</returns>
    public static unsafe T FromPointer(void* p, IntPtr numElements)
    {
      if (!_type.IsArray)
        throw new ArgumentException("Cannot cast a pointer to an array into an object");

      var poof = default(T);
      IntPtr marker;

      var lenPtr = (IntPtr*)(((byte*)p) - IntPtr.Size);
      *lenPtr = numElements;
      var rtthPtr = lenPtr - 1;
      *rtthPtr = _rtth;

      var pmarker = (&marker) - 1;
      *pmarker = (IntPtr)rtthPtr;
      return poof;
    }

    public static unsafe T CreateUnmanagedPrimitiveArray(int i)
    {
      if (!_type.IsArray)
        throw new ArgumentException("Type is not an array");
      
      var allocSize = (i * _elementSize) + PrologueArraySize;
      var p = (byte*)Marshal.AllocHGlobal(allocSize).ToPointer();

      p += PrologueArraySize;
      return FromPointer(p, (IntPtr)i);
    }



    public static unsafe void* CreateUnmanagedClassArray(int numElements)
    {
      var p = (byte*)Marshal.AllocHGlobal(_size * numElements);
      FixupRtthArray(p, numElements);
      return p;
    }

    public static unsafe T CreateUnmanaged()
    {
      var p = (byte*)Marshal.AllocHGlobal(_size);
      p += PrologueSize;
      return FromPointer(p);
    }

    /// <summary>
    /// Copy a
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public static unsafe T CopyUnmanaged(T o)
    {
      var p = ToPointer(o);
      var np = (byte*)Marshal.AllocHGlobal(_size);
      np += PrologueSize;
      Mem.Cpy((byte*) p, np, _size - PrologueSize);

      return FromPointer(np);
    }

    private static unsafe void FixupRtthArray(byte *p, long numElements)
    {      
      for (var i = 0; i < numElements; i++) {
        var thisRtth = (IntPtr*)p;
        p += _size;
        // We always test before assignment, so that if this memory comes
        // from a memory mapped region, with COW/private semantics, we won't
        // cause a page/fault COW operation needlessly
        if (*thisRtth != _rtth)
          *thisRtth = _rtth;
      }
  }

    public static unsafe IEnumerable<T> AsEnumerable(void *p, long len)
    {
      return new BluePillEnumerable(p, len);
    }

    public class BluePillEnumerable : IEnumerable<T>
    {
      private readonly unsafe byte* _p;
      private readonly long _numElements;

      public unsafe BluePillEnumerable(void* p, long numNumElements)
      {
        _p = (byte*) p;
        _numElements = numNumElements;
      }

      public unsafe IEnumerator<T> GetEnumerator()
      {
        FixupRtthArray(_p, _numElements);
        return new BluePillEnumerator(_p, _numElements);
      }

      IEnumerator IEnumerable.GetEnumerator()
      {        
        return GetEnumerator();
      }
    }

    public class BluePillEnumerator : IEnumerator<T>
    {
      private readonly unsafe byte* _origP;
      private unsafe byte* _p;
      private readonly long _numElements;
      private readonly unsafe byte* _end;

      public unsafe BluePillEnumerator(void* p, long numElements)
      {        
        _origP = (byte*) p;
        _numElements = numElements;
        _end = _origP + _numElements*_size;
        Reset();
      }

      public void Dispose()
      { }

      public unsafe bool MoveNext()
      {
        if (_p == _end)
          return false;

        _p += _size;
        return true;
      }

      public unsafe void Reset()
      {
        _p = _origP - _size;
      }

      public unsafe T Current
      {
        get { 
          var poof = default(T);
          IntPtr marker;

          var pmarker = (&marker) - 1;
          *pmarker = (IntPtr) _p;
          return poof;
        }
      }

      object IEnumerator.Current
      { get { return Current; } }
    }

  }
}