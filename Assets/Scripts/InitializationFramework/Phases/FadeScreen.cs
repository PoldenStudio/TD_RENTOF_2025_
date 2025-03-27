using DemolitionStudios.DemolitionMedia;
using DemolitionStudios.DemolitionMedia.Examples.DemolitionStudios.DemolitionMedia;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace InitializationFramework
{
    public class FadeScreen : MonoBehaviour
    {
        public RenderToIMGUI renderToIMGUI;
        public float fadeDuration = 2f;

        private bool _isFading = false;

        private void Awake()
        {
            if (renderToIMGUI != null)
            {
                renderToIMGUI.color = Color.black;
            }
        }

        private void Start()
        {
            if (renderToIMGUI.color == Color.black)
            {
                StartCoroutine(FadeAndLoadScene(Color.white));
            }
            else
            {
                StartCoroutine(FadeAndLoadScene(Color.black));
            }
        }

        public IEnumerator FadeAndLoadScene(Color targetColor)
        {
            if (renderToIMGUI == null)
            {
                Debug.LogError("RenderToIMGUI не найден в FadeScreen");
                yield break;
            }

            if (_isFading)
            {
                yield break;
            }

            _isFading = true;

            float elapsedTime = 0f;
            Color startColor = renderToIMGUI.color;

            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / fadeDuration;
                renderToIMGUI.color = Color.Lerp(startColor, targetColor, t);
                yield return null;
            }

            renderToIMGUI.color = targetColor;

            _isFading = false;
        }

        public bool IsFullyDarkened()
        {
            return renderToIMGUI != null && renderToIMGUI.color == Color.black;
        }

        public bool IsFullyBrightened()
        {
            return renderToIMGUI != null && renderToIMGUI.color == Color.white;
        }

        private void Reset()
        {
            gameObject.SetActive(false);
        }
    }
}