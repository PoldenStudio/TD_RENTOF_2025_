using Sirenix.Serialization;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DMXTest : MonoBehaviour
{

    byte[] data;

    DMXCommunicator cmn;

    // Start is called before the first frame update
    void Start()
    {

        data = new byte[513];

        for ( int i = 0; i< 513; i+=1 )
        {
            data[i] = 0;


        }

        cmn = new DMXCommunicator("COM15",250000);



    }


    [ContextMenu("Test_255")]
    void Test_255()
    {

        data[509] = 255;

        cmn.SetBytes(data);


    }


    [ContextMenu("Test_0")]
    void Test_0()
    {

        data[509] = 0;

        cmn.SetBytes(data);


    }


}
