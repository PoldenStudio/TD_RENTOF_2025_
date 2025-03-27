using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace InitializationFramework
{
    public interface IInitializable 
    {

        IEnumerator Initialize( System.Action<Object> OnFinished = null );

        IEnumerator Deinitialize(System.Action<Object> OnFinished = null);

    }

}