using DemolitionStudios.DemolitionMedia;

public class DemolitionMediaPlayer : IMediaPlayer
{
    private Media _media;

    public DemolitionMediaPlayer(Media media)
    {
        _media = media;
    }

    public bool Open(string url, SyncMode syncMode = SyncMode.SyncAudioMaster)
    {
        return _media.Open(url, syncMode);
    }

    public void Close()
    {
        _media.Close();
    }

    public void Play()
    {
        _media.Play();
    }

    public void Pause()
    {
        _media.Pause();
    }

    public float DurationSeconds
    {
        get { return _media.DurationSeconds; }
    }

    public float CurrentTime
    {
        get { return _media.CurrentTime; }
    }

    public void SeekToTime(float seconds)
    {
        _media.SeekToTime(seconds);
    }

    public void SeekToFrame(int frame)
    {
        _media.SeekToFrame(frame);
    }

    public int Loops
    {
        get { return _media.Loops; }
        set { _media.Loops = value; }
    }

    public int LoopsSinceStart
    {
        get { return _media.LoopsSinceStart; }
    }

    public float PlaybackSpeed
    {
        get { return _media.PlaybackSpeed; }
        set { _media.PlaybackSpeed = value; }
    }

    public bool IsPlaying
    {
        get { return _media.IsPlaying; }
    }

    public float StartTime
    {
        set { _media.StartTime = value; }
    }

    public float EndTime
    {
        set { _media.EndTime = value; }
    }

    public int StartFrame
    {
        set { _media.StartFrame = value; }
    }

    public int EndFrame
    {
        set { _media.EndFrame = value; }
    }

    public int VideoCurrentFrame
    {
        get { return _media.VideoCurrentFrame; }
    }

    public bool FramedropEnabled
    {
        get { return _media.FramedropEnabled; }
        set { _media.FramedropEnabled = value; }
    }

    public void GetFramedropCount(out int earlyDrops, out int lateDrops)
    {
        _media.GetFramedropCount(out earlyDrops, out lateDrops);
    }

    public SyncMode SyncMode
    {
        get { return _media.SyncMode; }
        set { _media.SyncMode = value; }
    }

    public PixelFormat VideoPixelFormat
    {
        get { return _media.VideoPixelFormat; }
    }

    public bool RequiresColorConversion
    {
        get { return _media.RequiresColorConversion; }
    }

    public int VideoWidth
    {
        get { return _media.VideoWidth; }
    }

    public int VideoHeight
    {
        get { return _media.VideoHeight; }
    }

    public float VideoFramerate
    {
        get { return _media.VideoFramerate; }
    }

    public float VideoAspectRatio
    {
        get { return _media.VideoAspectRatio; }
    }

    public int VideoNumFrames
    {
        get { return _media.VideoNumFrames; }
    }
}