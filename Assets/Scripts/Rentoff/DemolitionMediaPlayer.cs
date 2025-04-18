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


    public int VideoCurrentFrame
    {
        get { return _media.VideoCurrentFrame; }
    }
}