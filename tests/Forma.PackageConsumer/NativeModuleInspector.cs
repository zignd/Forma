using System.Diagnostics;
using System.Runtime.InteropServices;

internal static class NativeModuleInspector
{
    public static IReadOnlyList<string> GetLoadedModulePaths()
    {
        if (OperatingSystem.IsMacOS()) return GetMacOSModulePaths();
        if (OperatingSystem.IsLinux())
        {
            return File.ReadLines("/proc/self/maps")
                .Select(line =>
                {
                    var pathStart = line.IndexOf('/');
                    return pathStart >= 0 ? line[pathStart..] : null;
                })
                .Where(path => path is not null)
                .Select(path => path!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
        return Process.GetCurrentProcess().Modules
            .Cast<ProcessModule>()
            .Select(module => module.FileName)
            .Where(fileName => !string.IsNullOrEmpty(fileName))
            .ToArray();
    }

    private static IReadOnlyList<string> GetMacOSModulePaths()
    {
        var paths = new List<string>();
        var count = DyldImageCount();
        for (uint index = 0; index < count; index++)
        {
            var path = Marshal.PtrToStringUTF8(DyldGetImageName(index));
            if (!string.IsNullOrEmpty(path)) paths.Add(path);
        }
        return paths;
    }

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_dyld_image_count")]
    private static extern uint DyldImageCount();

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_dyld_get_image_name")]
    private static extern IntPtr DyldGetImageName(uint imageIndex);
}