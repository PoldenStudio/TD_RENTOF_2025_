using System;
using System.Linq;
using UnityEngine;

namespace Animations
{
    [Serializable]
    public class AnimationKey
    {
        public byte[] data;

        public AnimationKey(byte[] bytes)
        {
            data = bytes.Skip(1).Take(Mathf.Min(100, bytes.Length)).ToArray();
        }

        // public static AnimationKey operator *(AnimationKey anim, ChannelMultipliers mul)
        //     => new AnimationKey(anim.RX * mul.RX, anim.RZ * mul.RZ, anim.Height * mul.Height);
    }
}