using System;
using System.Linq;
using Animations.AnimationDataBase;
using UnityEngine;

namespace Animations
{
    public class AnimationDBInitializer : MonoBehaviour
    {
        [SerializeField] private string[] _preloadAnimations;
        private void Awake()
        {
            AnimationDB.Init();
            AnimationDB.Preload(_preloadAnimations);
            
            Debug.Log(String.Join("\n",AnimationDB.GetLoaded().Select(composition => composition.Path)));
        }
    }
}