using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Animations.Interfaces;
using Animations.Parameters;
using Animations.Utils;
using UnityEngine;

namespace Animations.AnimationDataBase
{
    public static class AnimationDB
    {
        private static Dictionary<string, Type> _animationTypes = null;

        private static Dictionary<string, AnimationComposition> _preloaded =
            new Dictionary<string, AnimationComposition>();

        public static void Init()
        {
            _animationTypes = typeof(AnimationDB).Assembly.GetTypes()
                .Where(AnimationTypeUtils.IsAnimation)
                .ToDictionary(type => ((IAnimationFromFile)Activator.CreateInstance(type)).FileExtension);
        }

        public static void Preload(string[] animationNames)
        {
            animationNames ??= Enumerable.Empty<string>().ToArray();
            
            _preloaded = animationNames
                .Select(name => Path.Combine(Application.streamingAssetsPath, "Animations", name))
                .Select(path => (path, Load(path)))
                .Where(tuple => tuple.Item2 != null)
                .ToDictionary(
                    tuple => tuple.Item1,
                    tuple => new AnimationComposition(tuple.Item1, new CachedAnimation(tuple.Item2.Animation), tuple.Item2.Parameters)
                );
        }

        public static AnimationComposition Load(string path)
        {
            if (_preloaded.ContainsKey(path))
            {
                return _preloaded[path];
            }
            else
            {
                return LoadInternal(path);
            }
        }

        private static AnimationComposition LoadInternal(string path)
        {
            var extension = Path.GetExtension(path).TrimStart('.');
            if (!_animationTypes.ContainsKey(extension))
            {
                return null;
            }

            if (!File.Exists(path))
            {
                return null;
            }

            var animation = (IAnimationFromFile)Activator.CreateInstance(_animationTypes[extension]);
            var parameters = AnimationParametersLoader.Load(path, animation);

            animation.Load(File.OpenText(path));

            Debug.Log($"Animation loaded: {path}");

            return new AnimationComposition(path, animation, parameters);
        }

        public static IEnumerable<AnimationComposition> GetLoaded()
        {
            return _preloaded.Select(pair => pair.Value);
        }
    }

    public class AnimationComposition
    {
        public readonly string Path;
        public readonly IAnimation Animation;
        public readonly AnimationParameters Parameters;

        public AnimationComposition(string path, IAnimation animation, AnimationParameters parameters)
        {
            Animation = animation;
            Parameters = parameters;
            Path = path;
        }
    }
}