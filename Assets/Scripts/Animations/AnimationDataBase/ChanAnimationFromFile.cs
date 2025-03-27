using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Animations.Interfaces;
using UnityEngine;

namespace Animations.AnimationDataBase
{
    public class ChanAnimationFromFile : IAnimationFromFile
    {
        public string FileExtension { get; } = "chan";

        private static CultureInfo CULTURE_INFO_ENG = new CultureInfo("en-US");

        private StreamReader _animationReader = null;
        
        public void Load(StreamReader file)
        {
            _animationReader = file;
        }

        IEnumerator<AnimationKey> IEnumerable<AnimationKey>.GetEnumerator()
        {
            return new Numerator(_animationReader);
        }
        
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<AnimationKey>) this).GetEnumerator();
        }

        public class Numerator : IEnumerator<AnimationKey>
        {
            private int _index = 0;

            private StreamReader _animationReader = null;

            public Numerator(StreamReader file)
            {
                _animationReader = file;
                // MoveNext();
            }

            public bool MoveNext()
            {
                string readLine = null;
                if (!_animationReader.EndOfStream)
                {
                    readLine = _animationReader.ReadLine();
                }

                while (
                    readLine != null && !_animationReader.EndOfStream
                                     && readLine.Length > 0 && readLine.StartsWith("#")
                )
                {
                    readLine = _animationReader.ReadLine();
                }

                if (readLine != null && (readLine.Length <= 0 || readLine.StartsWith("#")))
                {
                    readLine = null;
                }

                try
                {
                    if (readLine == null)
                    {
                        Current = null;
                    }
                    else
                    {
                        var array = readLine
                            .Replace('\t', ' ')
                            .Split(' ')
                            // .Select(s => (byte)Mathf.FloorToInt(float.Parse(s)))
                            .Select(s => (byte)Mathf.FloorToInt(float.Parse(s, CULTURE_INFO_ENG))).ToArray();
                        Current = new AnimationKey(array);
                    }
                }
                catch (Exception e)
                {
                    Current = null;
                    Debug.LogException(e);
                }

                return Current != null;
            }

            public void Reset()
            {
                _index = 0;
                _animationReader?.Dispose();
                _animationReader = null;
            }

            object IEnumerator.Current => Current;

            public AnimationKey Current { get; private set; } = null;

            public void Dispose()
            {
            }
        }
    }
}