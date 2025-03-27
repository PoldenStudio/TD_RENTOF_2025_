using System;
using Animations.Interfaces;

namespace Animations.Parameters
{
    [Serializable]
    public class AnimationParameters
    {
        public float FrameRate = 60f;
        public int ShiftFrames = 0;
        public ChannelMultipliers ChannelMultipliers = new ChannelMultipliers();
    }

    [Serializable]
    public class AnimationParameters<TAnimationParameter> : AnimationParameters
        where TAnimationParameter : IAnimationParameter
    {
        public TAnimationParameter SpecificParameters;
    }
}