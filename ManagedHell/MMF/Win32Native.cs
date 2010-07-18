#if NETFX_BUILD
using System;
using System.Runtime.InteropServices;

namespace ManagedHell.MMF
{
  [StructLayout(LayoutKind.Sequential)]
  public struct SYSTEM_INFO
  {
    public ushort processorArchitecture;
    ushort reserved;
    public uint pageSize;
    public IntPtr minimumApplicationAddress;
    public IntPtr maximumApplicationAddress;
    public IntPtr activeProcessorMask;
    public uint numberOfProcessors;
    public uint processorType;
    public uint allocationGranularity;
    public ushort processorLevel;
    public ushort processorRevision;
  }

  [Flags]
  public enum Win32MapProtection
  {
    PageNone = 0x00000000,
    // protection - mutually exclusive, do not or
    PageReadOnly = 0x00000002,
    PageReadWrite = 0x00000004,
    PageWriteCopy = 0x00000008,
    // attributes - or-able with protection
    SecImage = 0x01000000,
    SecReserve = 0x04000000,
    SecCommit = 0x08000000,
    SecNoCache = 0x10000000,
  }

  /// <summary>
  ///   Specifies access for the mapped file.
  ///   These correspond to the FILE_MAP_XXX
  ///   constants used by MapViewOfFile[Ex]()
  /// </summary>
  enum Win32FileMapAccessType : uint
  {
    Unspecified = 0x00,
    Copy = 0x01,
    Write = 0x02,
    Read = 0x04,
    AllAccess = 0x08,
    Execute = 0x20,    
  }

  [Flags]
  public enum Win32FileAccess : uint
  {
    /// <summary>
    /// Generic Read Permission
    /// </summary>
    GenericRead = 0x80000000,
    /// <summary>
    /// Generic Write Permission
    /// </summary>
    GenericWrite = 0x40000000,
    /// <summary>
    /// Generic Execute Permission
    /// </summary>
    GenericExecute = 0x20000000,
    /// <summary>
    /// Generic All Permissions
    /// </summary>
    GenericAll = 0x10000000
  }

  [Flags]
  public enum Wind32FileShare : uint
  {
    /// <summary>
    ///
    /// </summary>
    None = 0x00000000,
    /// <summary>
    /// Enables subsequent open operations on an object to request read access.
    /// Otherwise, other processes cannot open the object if they request read access.
    /// If this flag is not specified, but the object has been opened for read access, the function fails.
    /// </summary>
    Read = 0x00000001,
    /// <summary>
    /// Enables subsequent open operations on an object to request write access.
    /// Otherwise, other processes cannot open the object if they request write access.
    /// If this flag is not specified, but the object has been opened for write access, the function fails.
    /// </summary>
    Write = 0x00000002,
    /// <summary>
    /// Enables subsequent open operations on an object to request delete access.
    /// Otherwise, other processes cannot open the object if they request delete access.
    /// If this flag is not specified, but the object has been opened for delete access, the function fails.
    /// </summary>
    Delete = 0x00000004
  }

  public enum Win32CreationDisposition : uint
  {
    /// <summary>
    /// Creates a new file. The function fails if a specified file exists.
    /// </summary>
    New = 1,
    /// <summary>
    /// Creates a new file, always.
    /// If a file exists, the function overwrites the file, clears the existing attributes, combines the specified file attributes,
    /// and flags with FILE_ATTRIBUTE_ARCHIVE, but does not set the security descriptor that the SECURITY_ATTRIBUTES structure specifies.
    /// </summary>
    CreateAlways = 2,
    /// <summary>
    /// Opens a file. The function fails if the file does not exist.
    /// </summary>
    OpenExisting = 3,
    /// <summary>
    /// Opens a file, always.
    /// If a file does not exist, the function creates a file as if dwCreationDisposition is CREATE_NEW.
    /// </summary>
    OpenAlways = 4,
    /// <summary>
    /// Opens a file and truncates it so that its size is 0 (zero) bytes. The function fails if the file does not exist.
    /// The calling process must open the file with the GENERIC_WRITE access right.
    /// </summary>
    TruncateExisting = 5
  }

  [Flags]
  public enum Win32FileAttributes : uint
  {
    Readonly = 0x00000001,
    Hidden = 0x00000002,
    System = 0x00000004,
    Directory = 0x00000010,
    Archive = 0x00000020,
    Device = 0x00000040,
    Normal = 0x00000080,
    Temporary = 0x00000100,
    SparseFile = 0x00000200,
    ReparsePoint = 0x00000400,
    Compressed = 0x00000800,
    Offline = 0x00001000,
    NotContentIndexed = 0x00002000,
    Encrypted = 0x00004000,
    Write_Through = 0x80000000,
    Overlapped = 0x40000000,
    NoBuffering = 0x20000000,
    RandomAccess = 0x10000000,
    SequentialScan = 0x08000000,
    DeleteOnClose = 0x04000000,
    BackupSemantics = 0x02000000,
    PosixSemantics = 0x01000000,
    OpenReparsePoint = 0x00200000,
    OpenNoRecall = 0x00100000,
    FirstPipeInstance = 0x00080000
  }



  /// <summary>Win32 APIs used by the library</summary>
  /// <remarks>
  ///   Defines the PInvoke functions we use
  ///   to access the FileMapping Win32 APIs
  /// </remarks>
  internal static class Win32Native
  {
    public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
    public static readonly IntPtr NULL_HANDLE = IntPtr.Zero;
    //public const uint GENERIC_READ = 0x80000000;
    //public const uint GENERIC_WRITE = 0x40000000;
    //public const uint OPEN_ALWAYS = 4;

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr CreateFile(
       String lpFileName, uint dwDesiredAccess, uint dwShareMode,
       IntPtr lpSecurityAttributes, uint dwCreationDisposition,
       int dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32")]
    public static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr CreateFileMapping(
       IntPtr hFile, IntPtr lpAttributes, int flProtect,
       int dwMaximumSizeLow, int dwMaximumSizeHigh,
       String lpName);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool FlushViewOfFile(
       IntPtr lpBaseAddress, IntPtr dwNumBytesToFlush);

    [DllImport("kernel32")]
    public static extern IntPtr MapViewOfFileEx(IntPtr hFileMappingObject,
       Win32FileMapAccessType dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow,
       UIntPtr dwNumberOfBytesToMap, IntPtr lpBaseAddress);

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr OpenFileMapping(
       int dwDesiredAccess, bool bInheritHandle, String lpName);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr handle);


    public const uint FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100;
    public const uint FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
    public const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;

    // the parameters can also be passed as a string array:
    [DllImport("Kernel32", SetLastError = true)]
    public static extern uint FormatMessage(uint dwFlags, IntPtr lpSource,
      uint dwMessageId, uint dwLanguageId, ref IntPtr lpBuffer,
      uint nSize, string[] Arguments);
  }

}
#endif // NETFX_BUILD
