using System.Collections.ObjectModel;
using Microsoft.Xna.Framework.Media;

namespace Tka.Compatibility;

public sealed class XnaVisualizationData
{
    public ReadOnlyCollection<float> Frequencies { get; } = Array.AsReadOnly(new float[256]);
    public ReadOnlyCollection<float> Samples { get; } = Array.AsReadOnly(new float[256]);
}

// MonoGame WindowsDX removed XNA's visualization API. Music playback and timing
// still use its MediaPlayer. An actual spectrum implementation remains required;
// the baseline reports the gap instead of inventing frequencies.
public static class XnaMediaPlayer
{
    private static bool reported;
    public static bool IsVisualizationEnabled { get; set; }
    public static bool IsRepeating { get => MediaPlayer.IsRepeating; set => MediaPlayer.IsRepeating = value; }
    public static float Volume { get => MediaPlayer.Volume; set => MediaPlayer.Volume = value; }
    public static TimeSpan PlayPosition => MediaPlayer.PlayPosition;
    public static void Play(Song song) => MediaPlayer.Play(song);
    public static void Pause() => MediaPlayer.Pause();
    public static void Resume() => MediaPlayer.Resume();
    public static void Stop() => MediaPlayer.Stop();
    public static void GetVisualizationData(XnaVisualizationData data)
    {
        if (!reported) { LocalServices.Log("BASELINE LIMITATION: music spectrum not yet implemented; visualization bins are zero."); reported = true; }
    }
}
