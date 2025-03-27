using System.Collections.Generic;
using System.IO;

namespace Animations.Interfaces
{
    public interface IAnimation : IEnumerable<AnimationKey>
    {
    }
    
    public interface IAnimationFromFile : IAnimation
    {
        string FileExtension { get; }
        void Load(StreamReader file);
    }

    public interface IAnimationFromFile<TAnimationParameter> : IAnimationFromFile where TAnimationParameter : IAnimationParameter
    {
        TAnimationParameter Parameters { get; set; }
    }
}