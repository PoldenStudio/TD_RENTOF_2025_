using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace InitializationFramework
{
    public interface ISwitchable
    {
        IEnumerator Switch( Object obj , System.Action<Object> OnFinished = null );

    }

}