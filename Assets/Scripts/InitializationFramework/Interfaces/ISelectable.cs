using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace InitializationFramework
{
    public interface ISelectable
    {

        IEnumerator Select();

        IEnumerator Unselect();

    }

}