using System;
using System.IO;

namespace Daemaged.ManagedHell.MMF
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