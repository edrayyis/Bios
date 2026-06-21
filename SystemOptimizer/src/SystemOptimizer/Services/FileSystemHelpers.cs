using System.IO;
using System.Security.Cryptography;
using Microsoft.VisualBasic.FileIO;

namespace SystemOptimizer.Services;

public static partial class FileSystemHelpers
{
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
    /// Deletes a file to the Recycle Bin (recoverable) rather than permanently,
    /// unless permanent is requested. Returns the size freed, 0 on failure.
    /// </summary>
    public static long DeleteFile(string path, bool permanent)
    {
        try
        {
            long size = new FileInfo(path).Length;
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs,
                permanent ? RecycleOption.DeletePermanently : RecycleOption.SendToRecycleBin);
            return size;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Could not delete {path}: {ex.Message}");
            return 0;
        }
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
}
