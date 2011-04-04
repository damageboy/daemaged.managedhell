using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Daemaged.ManagedHell.MMF;

namespace Daemaged.ManagedHell
{
  public class BluePill
  {
    public static readonly int PrologueSize = IntPtr.Size;
    public static readonly int PrologueArraySize = PrologueSize * 2;

    public static unsafe void Free(void* p)
    {
      Marshal.FreeHGlobal(new IntPtr(p));
    }

  }

  public class BluePill<T> : BluePill
  {
    private static readonly Type _type;
    private static readonly Type _elementType;
    private static readonly IntPtr _rtth;
    private static readonly int _size;
    private static readonly int _elementSize;
    private static IAllocator _defaultAllocator;

    public class BluePillEnumerable : IEnumerable<T>
    {
      private readonly unsafe byte* _p;
      private readonly int _numElements;

      public unsafe BluePillEnumerable(void* p, int numNumElements)
      {
        _p = (byte*)p;
        _numElements = numNumElements;
      }

      public unsafe IEnumerator<T> GetEnumerator()
      {
        FixupRtthArray(_p, _numElements);
        return new BluePillEnumerator(_p, _numElements);
      }

      IEnumerator IEnumerable.GetEnumerator()
      { return GetEnumerator(); }
    }

    public class BluePillEnumerator : IEnumerator<T>
    {
      private readonly unsafe byte* _origP;
      private unsafe byte* _p;
      private readonly int _numElements;
      private readonly unsafe byte* _end;

      public unsafe BluePillEnumerator(void* p, int numElements)
      {
        _origP = (byte*)p;
        _numElements = numElements;
        _end = _origP + (_numElements - 1) * _size;
        Reset();
      }

      public void Dispose() { }

      public unsafe bool MoveNext()
      {
        if (_p == _end)
          return false;

        _p += _size;
        return true;
      }

      public unsafe void Reset()
      { _p = _origP - _size; }
      

      public unsafe T Current
      {
        get
        {
          T poof;
#if IL
          ldloca 0
          ldarg.0
          ldfld      uint8* class Daemaged.ManagedHell.BluePill`1/BluePillEnumerator<!T>::_p
          stobj      uint8*
          ldloc.0
          ret
#endif
          // Doesn't really matter, the inline IL will replace this at any rate...
          return default(T);
        }
      }

      object IEnumerator.Current
      { get { return Current; } }
    }

    public class BluePillList : IList<T>, IReadOnlyList<T>
    {
      private unsafe void* _p;
      private readonly int _numElements;

      public unsafe BluePillList(void* p, int numElements)
      {
        _p = p;
        _numElements = numElements;
      }

      public unsafe IEnumerator<T> GetEnumerator()
      { return new BluePillEnumerator(_p, _numElements); }
      IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

      public void Add(T item) { throw new NotImplementedException(); }
      public void Clear() { throw new NotImplementedException(); }
      public bool Remove(T item) { throw new NotImplementedException(); }
      public void Insert(int index, T item) { throw new NotImplementedException(); }
      public void RemoveAt(int index) { throw new NotImplementedException(); }

      public bool Contains(T item) { throw new NotImplementedException(); }
      public void CopyTo(T[] array, int arrayIndex) { throw new NotImplementedException(); }

      public int Count { get { return _numElements; } }

      public bool IsReadOnly { get { return true; } }

      public int IndexOf(T item) { throw new NotImplementedException(); }
      public unsafe T this[int index]
      {
        get
        {
          T poof;
#if IL          
          ldloca 0
          ldarg.0
          ldfld      void* class Daemaged.ManagedHell.BluePill`1/BluePillList<!T>::_p
          ldarg.1
          ldsfld     int32 class Daemaged.ManagedHell.BluePill`1<!T>::_size
          mul
          add
          stobj      uint8*
          ldloc.0
          ret
#endif
          // Doesn't really matter, the inline IL will replace this at any rate...
          return default(T);
        }
        set { throw new NotImplementedException(); }
      }
    }

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

    public int SizeOf { get { return _size; } }

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
      //if (_type.IsValueType)
      //  throw new ArgumentException("Cannot cast a pointer to an object into an array");

#if USE_CSHARP
      var x = o;
      IntPtr* marker;
      var pmarker = (&marker) - 1;
      return *pmarker;
#endif

#if IL    
      ldarg.0
      ret
#endif

      return null;
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
      //if (_type.IsArray)
      //  throw new ArgumentException("Cannot cast a pointer to an object into an array");
      T poof;
#if IL
      ldarg.0
    
      ldobj      native int
      ldsfld     native int class Daemaged.ManagedHell.BluePill`1<!T>::_rtth
      beq.s  SkipRtth
    
      ldarg.0
      ldsfld     native int class Daemaged.ManagedHell.BluePill`1<!T>::_rtth
      stobj      native int
      SkipRtth:
      ldloca.s   0
      ldarg.0
      stobj      void *
      ldloc.0
      ret
#endif
      return default(T);
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
      //if (!_type.IsArray)
      //  throw new ArgumentException("Cannot cast a pointer to an array into an object");

      T poof;
#if IL
      ldarg.0
      sizeof     native int
      add
      ldarg.1
      stobj      native int
      ldarg.0
      ldobj      native int
      ldsfld     native int class Daemaged.ManagedHell.BluePill`1<!T>::_rtth
      beq.s  SkipRtth
      
      ldarg.0
      ldsfld     native int class Daemaged.ManagedHell.BluePill`1<!T>::_rtth      
      stobj      native int
      SkipRtth:
      ldloca.s   0
      ldarg.0
      stobj      void *
      ldloc.0
      ret
#endif
      return default(T);

    }

    public static unsafe T CreateUnmanagedPrimitiveArray(int i)
    {
      if (!_type.IsArray)
        throw new ArgumentException("Type is not an array");
      
      var allocSize = (i * _elementSize) + PrologueArraySize;
      var p = (byte*)Marshal.AllocHGlobal(allocSize).ToPointer();
      
      return FromPointer(p, (IntPtr)i);
    }

    public static  int GetUnmanagedClassArraySize(int numElements) { return _size*numElements; }

    public static unsafe void* CreateUnmanagedClassArray(byte[] bytes, int numElements)
    {
      var gch = GCHandle.Alloc(bytes, GCHandleType.Pinned);
      var p = (byte *) gch.AddrOfPinnedObject();
      p = (byte*) CreateUnmanagedClassArray(p, numElements);
      gch.Free();
      return p;
    }


    public static unsafe void* CreateUnmanagedClassArray(int numElements)
    {
      var p = (byte*)Marshal.AllocHGlobal(GetUnmanagedClassArraySize(numElements));
      return CreateUnmanagedClassArray(p, numElements);      
    }


    public static unsafe void* CreateUnmanagedClassArray(byte* p, int numElements)
    {
      FixupRtthArray(p, numElements);
      return p;
    }


    public static unsafe T CreateUnmanaged()
    {
      var p = (byte*)Marshal.AllocHGlobal(_size);
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
      Mem.Cpy((byte*) p, np, _size);
      return FromPointer(np);
    }

    /// <summary>
    /// Copy a
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public static unsafe void CopyTo(T o, void *dest)
    {
      var p = ToPointer(o);
      Mem.Cpy((byte*)p, (byte*) dest, _size);
    }

    /// <summary>
    /// Copy a
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public static unsafe void CopyTo(IEnumerable<T> src, int numElements, void* dest)
    {
      var n = 0;
      var d = (byte*) dest;
      foreach (var t in src) {
        if (n++ > numElements)
          return;
        Mem.Cpy((byte*) ToPointer(t), d, _size);
        d += _size;
      }
    }

    /// <summary>
    /// Copy a
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public static unsafe void CopyTo(IEnumerable<T> src, int numElements, byte[] bytes)
    {
      var gch = GCHandle.Alloc(bytes, GCHandleType.Pinned);
      var p = (byte*)gch.AddrOfPinnedObject();
      CopyTo(src, numElements, p);
      gch.Free();
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

    public static unsafe IEnumerable<T> AsEnumerable(void *p, int len)
    { return new BluePillEnumerable(p, len); }

    public static unsafe IList<T> AsList(void* p, int len)
    { return new BluePillList(p, len); }

    public static unsafe IReadOnlyList<T> AsReadonlyList(void* p, int len)
    { return new BluePillList(p, len); }


  }


}