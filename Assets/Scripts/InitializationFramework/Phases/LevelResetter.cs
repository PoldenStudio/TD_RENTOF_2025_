using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RenderHeads.Media.AVProVideo;
using UnityEngine.SceneManagement;

namespace InitializationFramework
{
    public class LevelResetter : MonoBehaviour, IInitializable 
    {
        [SerializeField]
        bool bypassOnInit = true;

        public IEnumerator Initialize(System.Action<Object> OnFinished)
        {
            gameObject.SetActive(true);
            yield return null;
        }


        public IEnumerator Deinitialize(System.Action<Object> OnFinished)
        {
            SceneManager.LoadScene(0);
            yield return null;
        }

        private void Reset()
        {
            gameObject.SetActive(false);
        }
    }
}