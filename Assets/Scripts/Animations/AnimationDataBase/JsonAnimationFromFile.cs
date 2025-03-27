using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Animations.Interfaces;
using UnityEngine;

namespace Animations.AnimationDataBase
{
    public class JsonAnimationFromFile : IAnimationFromFile
    {
        public string FileExtension { get; } = "json";

        private Numerator.AnimationData _animationData;

        public void Load(StreamReader file)
        {
            _animationData = JsonUtility.FromJson<Numerator.AnimationData>(file.ReadToEnd());
        }

        IEnumerator<AnimationKey> IEnumerable<AnimationKey>.GetEnumerator()
        {
            return new Numerator(_animationData);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<AnimationKey>)this).GetEnumerator();
        }

        public class Numerator : IEnumerator<AnimationKey>
        {
            private int _index = 0;

            private AnimationData _animationData = null;

            public Numerator(AnimationData animationData)
            {
                _animationData = animationData;
                // MoveNext();
            }

            public bool MoveNext()
            {
                Current = (0 <= _index && _index < _animationData.Data.Length)
                    ? _animationData.Data[_index]
                    : null;
                return Current != null;
            }

            public void Reset()
            {
                _index = 0;
            }

            object IEnumerator.Current => Current;

            public AnimationKey Current { get; private set; } = null;

            public void Dispose()
            {
            }

            [Serializable]
            public class AnimationData
            {
                public float FrameRate;
                public JsonAnimationKey[] Data;
            }

            [Serializable]
            public class JsonAnimationKey
            {
                public byte[] data;
            
                public static implicit operator AnimationKey(JsonAnimationKey anim) =>
                    new AnimationKey(anim.data);
            }
        }
    }
}