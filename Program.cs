using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ManagedHell
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


  class Program
  {
    static unsafe void Main(string[] args)
    {
      var fooBar = new Trade {DateTime = DateTime.Now, Price = 1435.25, Size = 100};
      var fooBarNative = BluePill<Trade>.CopyUnmanaged(fooBar);
      PrintTrade(fooBar);
      PrintTrade(fooBarNative);


      var fooBar2 = new Trade2 { DateTime = DateTime.Now, Price = 1435.25, Size = 100 };
      var fooBarNative2 = BluePill<Trade2>.CopyUnmanaged(fooBar2);
      PrintTrade(fooBar2);
      PrintTrade(fooBarNative2);

      var unamangeTradeArray = BluePill<Trade>.CreateUnmanagedClassArray(100);
      var rnd = new Random(666);
      foreach (var t in BluePill<Trade>.AsEnumerable(unamangeTradeArray, 100))
      {
        t.Price = 1000 + rnd.Next()%500/4.0;
        t.Size = rnd.Next()%100;
        t.DateTime = DateTime.Now;
      }

      foreach (var t in BluePill<Trade>.AsEnumerable(unamangeTradeArray, 100))
        PrintTrade(t);

      var magic = BluePill<int[]>.CreateUnmanagedPrimitiveArray(30);
      for (var i = 0; i < 30; i++)
        magic[i] = i;

      Console.WriteLine(magic.Length);

      foreach (var t in magic)
        Console.WriteLine(t);

    }

    private static void PrintTrade(Trade2 t)
    {
      Console.WriteLine("{0}: {1}/{2}", t.DateTime, t.Price, t.Size);
    }

    private static void PrintTrade(Trade t)
    {
      Console.WriteLine("{0}: {1}/{2}", t.DateTime, t.Price, t.Size);
    }

    private static unsafe RuntimeTypeHandle GetRtth(byte[] bytes)
    {
      RuntimeTypeHandle handle;
      fixed (byte* pMem = &bytes[0])
      {
        var pArrayBase = (int*)pMem;
        pArrayBase--;
        pArrayBase--;
        pArrayBase--;
        pArrayBase--;
        var rtth = *(long*)pArrayBase;
        // RTTH is a value-type whose only member is an IntPtr; can be set as a long on x64
        var pH = &handle;
        *((long*)pH) = rtth;
        return handle;
      }
    }


    private static unsafe void DumpInfo(byte[] bytes)
    {
      Type arrayType = null;
      RuntimeTypeHandle handle;
      fixed (byte* pMem = &bytes[0])
      {
        Console.WriteLine("{0:x16}", (long)pMem);
        var pArrayBase = (int*)pMem;
        Console.WriteLine("{0:x8}", *pArrayBase);
        pArrayBase--;
        Console.WriteLine("{0:x8}", *pArrayBase);
        pArrayBase--;
        Console.WriteLine("{0:x8}", *pArrayBase);
        pArrayBase--;
        Console.WriteLine("{0:x8}", *pArrayBase);
        pArrayBase--;
        Console.WriteLine("{0:x8}", *pArrayBase);
        var rtth = *(long*)pArrayBase;
        // RTTH is a value-type whose only member is an IntPtr; can be set as a long on x64
        var pH = &handle;
        *((long*)pH) = rtth;
        arrayType = Type.GetTypeFromHandle(handle);
      }

      if (arrayType != null)
      {
        Console.WriteLine(arrayType.Name);
      }

      Console.WriteLine("arrayType RTTH: {0:x16}", arrayType.TypeHandle.Value.ToInt64());

      var rtth2 = typeof (byte[]).TypeHandle;
      var rtth3 = typeof(Byte[]).TypeHandle;

      Console.WriteLine("byte[] RTTH: {0:x16}", rtth2.Value.ToInt64());
      Console.WriteLine("Byte[] RTTH: {0:x16}", rtth3.Value.ToInt64());
      var a = 1;
      var b = 2;
      var pA = &a;
      var pB = &b;
      Console.WriteLine(*pB);
      Console.WriteLine(*(pB - 1));
    }

  }
}