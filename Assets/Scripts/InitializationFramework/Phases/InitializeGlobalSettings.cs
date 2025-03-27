using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class InitializeGlobalSettings : MonoBehaviour 
{
    [SerializeField]
    GlobalSettings gs;

    private static GlobalSettings globalSettings;

    public static GlobalSettings GlobalSettings
    { get { return globalSettings; }  }

    private void Start()
    {
        globalSettings = gs;
    }


}

