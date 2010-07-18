using System;
using System.Runtime.InteropServices;

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

  class Program
  {
    static unsafe void Main(string[] args)
    {
      //
      var fooBar = new Trade { DateTime = DateTime.Now, Price = 1435.25, Size = 100 };
      var fooBarNative = BluePill<Trade>.CreateUnmanaged();
      fooBarNative.DateTime = fooBar.DateTime;
      fooBarNative.Price    = fooBar.Price;
      fooBarNative.Size     = fooBar.Size;
      PrintTrade(fooBar);
      PrintTrade(fooBarNative);

      var fooBar2 = new Trade2 { DateTime = DateTime.Now, Price = 1435.25, Size = 100 };
      var fooBarNative2 = BluePill<Trade2>.CreateUnmanaged();
      fooBarNative2.DateTime = fooBar2.DateTime;
      fooBarNative2.Price    = fooBar2.Price;
      fooBarNative2.Size     = fooBar2.Size;
      PrintTrade(fooBar2);
      PrintTrade(fooBarNative2);

      var unamangeTradeArray = BluePill<Trade>.CreateUnmanagedClassArray(100);
      var rnd = new Random(666);
      foreach (var t in BluePill<Trade>.AsEnumerable(unamangeTradeArray, 100)) {
        t.Price = 1000 + rnd.Next() % 500 / 4.0;
        t.Size = rnd.Next() % 100;
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

  }
}
