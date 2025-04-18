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

    float StartTime { set;  }

    int VideoCurrentFrame { get; }

}