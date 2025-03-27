using System;
using System.IO;
using Animations.Interfaces;
using UnityEngine;

namespace Animations.Parameters
{
    public static class AnimationParametersLoader
    {
        public static AnimationParameters Load(string animationPath, Type specificParameterType = null)
        {
            var animationParametersPath = animationPath + ".json";

            AnimationParameters parameters = new AnimationParameters();

            if (!File.Exists(animationParametersPath))
            {
                File.WriteAllText(animationParametersPath, JsonUtility.ToJson(parameters, true));
            }
            else
            {
                var json = File.ReadAllText(animationParametersPath);
                parameters = JsonUtility.FromJson<AnimationParameters>(json);
            }

            return parameters;
        }
    
        public static AnimationParameters Load(string animationPath, IAnimationFromFile animationFromFile)
        {
            var animationType = animationFromFile.GetType();
            if (animationType.IsGenericType)
            {
                // animationType.
                // if (animation is IAnimation<>)
                // {
                //
                // }
                //
                // return Load(animationPath, );
                
                // animation.ApplySpecificParameters(parameters);
                return null;
            }
            else
            {
                return Load(animationPath);
            }
        }
        
        // public static AnimationParameters Load(string animationPath)
        // {
        //     var animationParametersPath = animationPath + ".json";
        //
        //     AnimationParameters parameters = new AnimationParameters();
        //
        //     if (!File.Exists(animationParametersPath))
        //     {
        //         File.WriteAllText(animationParametersPath, JsonUtility.ToJson(parameters, true));
        //     }
        //     else
        //     {
        //         var json = File.ReadAllText(animationParametersPath);
        //         parameters = JsonUtility.FromJson<AnimationParameters>(json);
        //     }
        //
        //     return parameters;
        // }
    }
}