using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using DemolitionStudios.DemolitionMedia.Examples.DemolitionStudios.DemolitionMedia;
using DemolitionStudios.DemolitionMedia;

public class LoadScene : MonoBehaviour
{
    public int sceneIndexToLoad = 1;
    public RenderToIMGUIWithControls renderToIMGUI;
    public float fadeDuration = 1f;

    private bool isLoading = false;

    void Update()
    {
        if (!isLoading && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            isLoading = true;
            StartCoroutine(FadeAndLoadScene());
        }
    }

    public IEnumerator FadeAndLoadScene()
    {
        if (renderToIMGUI != null)
        {
            float elapsedTime = 0f;
            Color startColor = renderToIMGUI.color;
            Color targetColor = Color.black;

            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / fadeDuration;
                renderToIMGUI.color = Color.Lerp(startColor, targetColor, t);
                yield return null;
            }

            renderToIMGUI.color = targetColor;
        }

        if (sceneIndexToLoad >= 0 && sceneIndexToLoad < SceneManager.sceneCountInBuildSettings && renderToIMGUI.color == Color.black)
        {
            SceneManager.LoadScene(sceneIndexToLoad);
        }
    }

    public void SetSceneIndexToLoad(int index)
    {
        if (index >= 0)
        {
            sceneIndexToLoad = index;
        }
        else
        {
            Debug.LogError("Индекс сцены не может быть отрицательным.");
        }
    }
}