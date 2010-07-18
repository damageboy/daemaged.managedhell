using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ManagedHell.MMF
{
  public class MemoryMapException : IOException
  {
    // construction
    public MemoryMapException()
      : base()
    { }
    public MemoryMapException(string message)
      : base(message)
    { }
    public MemoryMapException(string message, Exception innerException)
      : base(message, innerException)
    { }
  }
}