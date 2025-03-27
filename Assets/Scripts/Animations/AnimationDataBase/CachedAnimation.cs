using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Animations.Interfaces;

namespace Animations.AnimationDataBase
{
    public class CachedAnimation : IAnimation
    {
        private readonly List<AnimationKey> _keys;

        public CachedAnimation(IAnimation animation)
        {
            _keys = animation.ToList();
        }

        public IEnumerator<AnimationKey> GetEnumerator()
        {
            return _keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}