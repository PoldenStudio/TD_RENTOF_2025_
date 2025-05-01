using DemolitionStudios.DemolitionMedia;

public interface IMediaPlayer
{
    bool Open(string url, SyncMode syncMode = SyncMode.SyncAudioMaster);
    void Close();
    void Play();
    void Pause();
    void SeekToTime(float seconds);
    void SeekToFrame(int frame);
    float PlaybackSpeed { get; set; }
    bool IsPlaying { get; }
    float DurationSeconds { get; }
    float CurrentTime { get; }

    float StartTime { set; }
    float EndTime { set; }
    int StartFrame { set; }
    int EndFrame { set; }

    int VideoCurrentFrame { get; }

    int Loops { get; set; }

    int LoopsSinceStart { get; }

    bool FramedropEnabled { get; set; }
    void GetFramedropCount(out int earlyDrops, out int lateDrops);

    SyncMode SyncMode { get; set; }

    PixelFormat VideoPixelFormat { get; }
    bool RequiresColorConversion { get; }
    int VideoWidth { get; }
    int VideoHeight { get; }
    float VideoFramerate { get; }
    float VideoAspectRatio { get; }
    int VideoNumFrames { get; }
}