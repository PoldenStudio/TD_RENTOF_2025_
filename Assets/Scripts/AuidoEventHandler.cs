using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AuidoEventHandler : MonoBehaviour
{
    [SerializeField]
    AudioSource tgSource;


    [SerializeField]
    List<AudioClip> clips;

    public void HandleAudioEvent( int index = 0 )
    { 
        var clip = clips[index];
        
        tgSource.clip = clip;

        tgSource.Play();    

    }



}
