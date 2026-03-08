namespace BazaarArena.Core;

/// <summary>帧与时间换算。1 帧 = 50ms；存储用毫秒，界面显示秒。</summary>
public static class TimeUtil
{
    public const int MsPerFrame = 50;
    public const int MsPerSecond = 1000;

    /// <summary>帧数转毫秒。</summary>
    public static int FramesToMs(int frames) => frames * MsPerFrame;

    /// <summary>毫秒转帧数（整除）。</summary>
    public static int MsToFrames(int ms) => ms / MsPerFrame;

    /// <summary>秒转毫秒。</summary>
    public static int SecondsToMs(double seconds) => (int)(seconds * MsPerSecond);
}
