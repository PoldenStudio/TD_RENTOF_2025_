using UnityEngine;


public class DontDestroyables : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

    }


}
