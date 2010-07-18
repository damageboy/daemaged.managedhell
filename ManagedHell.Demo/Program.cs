using System;
using System.IO;
using System.Runtime.InteropServices;
using ManagedHell.MMF;

namespace ManagedHell.Demo
{
  // Dummy Class to test
  [StructLayout(LayoutKind.Sequential)]
  public class Trade
  {
    private DateTime _dateTime;
    private double _price;
    private int _size;

    public int Size
    {
      get { return _size; }
      set { _size = value; }
    }

    public double Price
    {
      get { return _price; }
      set { _price = value; }
    }

    public DateTime DateTime
    {
      get { return _dateTime; }
      set { _dateTime = value; }
    }
  }

  [StructLayout(LayoutKind.Sequential)]
  public class Trade2
  {
    public DateTime DateTime { get; set; }
    public double Price { get; set; }
    public int Size { get; set; }
  }



  public class StupidMemMapAllocator : IAllocator
  {
    private readonly IMemoryMappedFile _mmap;
    private long _offset;
    private long _size;
    private long _fileSize;
    private long _currentOffset;
    private unsafe byte *_ptr;

    public unsafe StupidMemMapAllocator(string path, long fileSize, long offset, long size)
    {
      _mmap = MemoryMappedFileFactory.Create(path, fileSize);
      _fileSize = fileSize;
      _size = size;
      _offset = offset;
      _ptr = (byte*) _mmap.MapView(MapProtection.PageReadWrite, _offset, _size, IntPtr.Zero);
    }
    public unsafe IntPtr Allocate(int size)
    {
      var p = _ptr;
      _ptr += size;
      return (IntPtr) p;
    }

    public void Free(IntPtr p, int size)
    {      
    }
  }

  class Program
  {
    static unsafe void Main(string[] args)
    {
      Console.WriteLine("Create a class, copy it to an unamanaged class copy, print both:");
      var fooBar = new Trade { DateTime = DateTime.Now, Price = 1435.25, Size = 100 };
      var fooBarNative = BluePill<Trade>.CreateUnmanaged();
      fooBarNative.DateTime = fooBar.DateTime;
      fooBarNative.Price    = fooBar.Price;
      fooBarNative.Size     = fooBar.Size;
      PrintTrade(fooBar);
      PrintTrade(fooBarNative);

      Console.WriteLine("Same, but with automatic properties");
      var fooBar2 = new Trade2 { DateTime = DateTime.Now, Price = 1435.25, Size = 100 };
      var fooBarNative2 = BluePill<Trade2>.CreateUnmanaged();
      fooBarNative2.DateTime = fooBar2.DateTime;
      fooBarNative2.Price    = fooBar2.Price;
      fooBarNative2.Size     = fooBar2.Size;
      PrintTrade(fooBar2);
      PrintTrade(fooBarNative2);

      Console.WriteLine("Create and initialize an unmanaged array, convert it to a managed array:");
      var magic = BluePill<int[]>.CreateUnmanagedPrimitiveArray(30);
      for (var i = 0; i < 30; i++)
        magic[i] = i;

      Console.WriteLine("The array's .Length is: {0}", magic.Length);

      Console.WriteLine("The array's elements are:");
      foreach (var t in magic)
        Console.Write("{0},", t);
      Console.WriteLine();


      Console.WriteLine("Create and initialize an unmanaged array of classes");
      var unamangeTradeArray = BluePill<Trade>.CreateUnmanagedClassArray(100);
      var rnd = new Random(666);
      foreach (var t in BluePill<Trade>.AsEnumerable(unamangeTradeArray, 100)) {
        t.Price = 1000 + rnd.Next() % 500 / 4.0;
        t.Size = rnd.Next() % 100;
        t.DateTime = DateTime.Now;
      }

      Console.WriteLine("Treat the unmanaged memory as an IEnumerable<T>, print contents:");
      foreach (var t in BluePill<Trade>.AsEnumerable(unamangeTradeArray, 100))
        PrintTrade(t);


      Console.WriteLine("Treat the unmanaged memory as an IList<T>, print contents:");
      var list = BluePill<Trade>.AsList(unamangeTradeArray, 100);
      for (var i = 0; i < 100; i++)
        PrintTrade(list[i]);

      var mapExists = File.Exists("xxx.mmap");
      var a = new StupidMemMapAllocator("xxx.mmap", 1024*1024, 0, 1024*1024);
      var p = a.Allocate(1024 * 1024);

      if (!mapExists) {
        Console.WriteLine("Writing 1024 classes to xxx.mmap");
        
        foreach (var t in BluePill<Trade>.AsEnumerable((void*)p, 1024))
        {
          t.Price = 1000 + rnd.Next() % 500 / 4.0;
          t.Size = rnd.Next() % 100;
          t.DateTime = DateTime.Now;
        }
      } else {
        Console.WriteLine("Accessing previously stored 1024 classes from xxx.mmap");

        foreach (var t in BluePill<Trade>.AsEnumerable((void*)p, 1024))
          PrintTrade(t);        
      }


      Console.ReadLine();

    }

    private static void PrintTrade(Trade2 t)
    {
      Console.WriteLine("{0}: {1}/{2}", t.DateTime, t.Price, t.Size);
    }

    private static void PrintTrade(Trade t)
    {
      Console.WriteLine("{0}: {1}/{2}", t.DateTime, t.Price, t.Size);
    }

  }
}
