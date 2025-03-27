using System.Collections.Generic;

public interface IJsonLoader
{
    void LoadJson(string filePath);
    List<FrameData> GetFrames();
}

public interface IFrameProcessor
{
    void Init(List<FrameData> frames, float fps);
    void FixedUpdate();
}

public interface IFrameData
{
    int Frame { get; }
    List<List<List<int>>> Pixels { get; }
}