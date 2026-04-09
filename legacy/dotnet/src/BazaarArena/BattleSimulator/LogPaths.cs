namespace BazaarArena.BattleSimulator;

/// <summary>运行期日志目录与时间戳命名。日志写入该目录，文件名含当前时间戳。</summary>
public static class LogPaths
{
    /// <summary>获取日志根目录（若不存在则会在首次写入时创建）。优先当前目录下的 Logs，否则为程序目录下的 Logs。</summary>
    public static string GetLogDirectory()
    {
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        if (!Directory.Exists(dir))
            dir = Path.Combine(AppContext.BaseDirectory, "Logs");
        return dir;
    }

    /// <summary>生成带当前时间戳的日志文件名（不含路径），格式：yyyyMMdd-HHmmss.log。</summary>
    public static string GetTimestampedLogFileName() =>
        DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log";

    /// <summary>生成带当前时间戳的完整日志文件路径，目录不存在时会创建。</summary>
    public static string GetTimestampedLogPath()
    {
        var logDir = GetLogDirectory();
        Directory.CreateDirectory(logDir);
        return Path.Combine(logDir, GetTimestampedLogFileName());
    }
}
