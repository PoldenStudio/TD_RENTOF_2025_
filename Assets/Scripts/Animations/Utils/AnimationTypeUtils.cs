using System;
using System.Linq;
using Animations.Interfaces;
using Animations.Parameters;

namespace Animations.Utils
{
    public static class AnimationTypeUtils
    {
        private static Type ANIMATION_INTERFACE_TYPE = typeof(IAnimationFromFile);
        private static Type ANIMATION_WITH_PARAMETERS_INTERFACE_TYPE = typeof(IAnimationFromFile<>);
    
        public static bool IsAnimation(Type animation)
        {
            return !animation.IsAbstract && !animation.IsInterface && animation.GetInterfaces().Any(interfaceType => interfaceType == ANIMATION_INTERFACE_TYPE);
        }

        public static bool IsAnimationWithParameters(Type animation)
        {
            return animation.GetInterfaces().Any(interfaceType => interfaceType == ANIMATION_WITH_PARAMETERS_INTERFACE_TYPE);
        }

        public static void ApplySpecificParameters(this IAnimationFromFile animationFromFile, AnimationParameters parameters)
        {
            
        }
    }
}