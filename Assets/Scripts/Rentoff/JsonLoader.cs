using System.Collections.Generic;
using UnityEngine;

public class JsonLoader : IJsonLoader
{
    private List<FrameData> _frames;

    public void LoadJson(string filePath)
    {
        string jsonString = System.IO.File.ReadAllText(filePath);
        _frames = JsonUtility.FromJson<List<FrameData>>(jsonString);
    }

    public List<FrameData> GetFrames()
    {
        return _frames;
    }
}

public class FrameProcessor : IFrameProcessor
{
    private int _currentFrameIndex = 0;
    private List<FrameData> _frames;
    private float _currentSpeed = 1f;
    private float _fps;

    public void Init(List<FrameData> frames, float fps)
    {
        _frames = frames;
        _fps = fps;
    }

    public void FixedUpdate()
    {
        if (_frames == null || _frames.Count == 0) return;

        float frameDuration = 1f / _fps;
        float timePerFrame = frameDuration / _currentSpeed;

        if (_currentFrameIndex < _frames.Count)
        {
            FrameData currentFrame = _frames[_currentFrameIndex];
            Debug.Log($"Frame: {currentFrame.Frame}, Pixels: {currentFrame.Pixels.Count}");
            _currentFrameIndex++;
        }
    }
}

[System.Serializable]
public class FrameData : IFrameData
{
    public int Frame { get; set; }
    public List<List<List<int>>> Pixels { get; set; }
}