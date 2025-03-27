/*using RenderHeads.Media.AVProVideo;

public class AVProMediaPlayer : IMediaPlayer
{
    private MediaPlayer _mediaPlayer;

    public AVProMediaPlayer(MediaPlayer mediaPlayer)
    {
        _mediaPlayer = mediaPlayer;
    }

    public bool Open(string url)
    {
        _mediaPlayer.OpenVideoFromFile(MediaPlayer.FileLocation.AbsolutePathOrURL, url);
        return _mediaPlayer.Control != null;
    }

    public void Close()
    {
        _mediaPlayer.CloseVideo();
    }

    public void Play()
    {
        _mediaPlayer.Play();
    }

    public void Pause()
    {
        _mediaPlayer.Pause();
    }

    public float DurationSeconds
    {
        get { return _mediaPlayer.Info.GetDurationMs() / 1000f; }
    }

    public float CurrentTime
    {
        get { return _mediaPlayer.Control.GetCurrentTimeMs() / 1000f; }
    }

    public void SeekToTime(float seconds)
    {
        _mediaPlayer.Control.Seek(seconds * 1000f);
    }

    public float PlaybackSpeed
    {
        get { return _mediaPlayer.Control.GetPlaybackRate(); }
        set { _mediaPlayer.Control.SetPlaybackRate(value); }
    }

    public bool IsPlaying
    {
        get { return _mediaPlayer.Control.IsPlaying(); }
    }
}*/