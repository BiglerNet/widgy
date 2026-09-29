namespace UrDeck.Core.Diagnostics;

/// <summary>Minimal file + debug-output logger. Writes to urdeck.log next to the executable.</summary>
public static class UrDeckLog
{
    private static readonly object Gate = new();

    public static string LogPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "urdeck.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex == null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] {level} {message}";
        System.Diagnostics.Debug.WriteLine(line);
        try
        {
            lock (Gate)
                File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
