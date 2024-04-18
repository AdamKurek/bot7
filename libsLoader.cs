using System;
using System.Reflection;
using System.Runtime.InteropServices;

public class LibOpusLoader
{
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string libname);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern bool FreeLibrary(IntPtr hModule);

    private static IntPtr _opusHandle = IntPtr.Zero;
    private static IntPtr _sodiumHandle = IntPtr.Zero;

    public static void Init()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        string bitness = Environment.Is64BitProcess ? "win64" : "win32";
        string opusDirPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "nativelibs", bitness);

        if (_opusHandle == IntPtr.Zero)
        {
            _opusHandle = LoadLibrary(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "opus.dll"));
            _sodiumHandle = LoadLibrary(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "libsodium.dll"));
            Console.WriteLine(_opusHandle.ToString(),"  undo  ", _sodiumHandle.ToString()); ;

        }
    }

    public static void Dispose()
    {
        if (_opusHandle != IntPtr.Zero)
        {
            FreeLibrary(_opusHandle);
            FreeLibrary(_sodiumHandle);

        }
    }
}