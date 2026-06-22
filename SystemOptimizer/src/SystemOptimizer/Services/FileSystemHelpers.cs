using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace SystemOptimizer.Services;

public static partial class FileSystemHelpers
{
    /// <summary>The directory this app runs from (for single-file builds, the
    /// runtime-extraction temp dir). We must never delete anything in here.</summary>
    private static readonly string SelfDir = EnsureTrailingSep(
        Path.GetFullPath(AppContext.BaseDirectory));

    /// <summary>True if the path belongs to this application's own files.</summary>
    public static bool IsSelfPath(string path)
    {
        try { return Path.GetFullPath(path).StartsWith(SelfDir, StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    /// <summary>
    /// Enumerates files under a root without throwing on folders we can't access
    /// (System Volume Information, locked dirs, etc.). Built-in EnumerateFiles
    /// aborts the whole walk on the first UnauthorizedAccessException, so we
    /// recurse manually and skip what we can't read.
    /// </summary>
    public static IEnumerable<string> SafeEnumerateFiles(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            string dir = stack.Pop();

            string[] subDirs;
            try { subDirs = Directory.GetDirectories(dir); }
            catch { subDirs = Array.Empty<string>(); }
            foreach (var sub in subDirs)
            {
                // Skip reparse points (junctions/symlinks) to avoid loops.
                try
                {
                    var attrs = File.GetAttributes(sub);
                    if (attrs.HasFlag(FileAttributes.ReparsePoint)) continue;
                }
                catch { continue; }
                stack.Push(sub);
            }

            string[] files;
            try { files = Directory.GetFiles(dir); }
            catch { continue; }
            foreach (var f in files)
                yield return f;
        }
    }

    public static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Deletes a file, sending it to the Recycle Bin (recoverable) unless
    /// permanent is requested. Never deletes the app's own files, silently skips
    /// locked/in-use files (no shell dialogs), and returns the size freed (0 on
    /// skip/failure).
    /// </summary>
    public static long DeleteFile(string path, bool permanent)
    {
        try
        {
            if (IsSelfPath(path)) return 0;            // never delete our own runtime
            var fi = new FileInfo(path);
            if (!fi.Exists) return 0;
            long size = fi.Length;

            // Skip in-use files so we never block on an interactive dialog.
            if (IsLocked(fi))
            {
                Logger.Warn($"Skipped (in use): {path}");
                return 0;
            }

            if (permanent)
            {
                File.Delete(path);
            }
            else if (!SendToRecycleBin(path))
            {
                Logger.Warn($"Recycle Bin delete failed: {path}");
                return 0;
            }
            return size;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Could not delete {path}: {ex.Message}");
            return 0;
        }
    }

    private static bool IsLocked(FileInfo fi)
    {
        try
        {
            using var s = fi.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
    }

    /// <summary>Best-effort directory size; ignores files it can't read.</summary>
    public static long DirectorySize(string dir)
    {
        long total = 0;
        foreach (var f in SafeEnumerateFiles(dir))
        {
            try { total += new FileInfo(f).Length; }
            catch { /* skip */ }
        }
        return total;
    }

    private static string EnsureTrailingSep(string path)
        => path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;

    // ---- Recycle Bin via shell, with all UI suppressed --------------------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;     // route to Recycle Bin
    private const ushort FOF_NOERRORUI = 0x0400;     // suppress the "File In Use" dialog
    private const ushort FOF_NOCONFIRMMKDIR = 0x0200;

    private static bool SendToRecycleBin(string path)
    {
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path + "\0\0", // pFrom must be double-null-terminated
            fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION |
                              FOF_SILENT | FOF_NOERRORUI | FOF_NOCONFIRMMKDIR),
        };
        return SHFileOperation(ref op) == 0;
    }
}
