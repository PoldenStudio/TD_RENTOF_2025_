using UnityEngine;

public class ShutterController : MonoBehaviour
{
    public RectTransform shutterRectTransform;

    private float minX;
    private float maxX;

    private void Start()
    {
        minX = shutterRectTransform.anchoredPosition.x;
        maxX = minX + shutterRectTransform.rect.width;
    }

    public void SetValue(float value)
    {
        float x = Mathf.Lerp(minX, maxX, value);
        shutterRectTransform.anchoredPosition = new Vector2(x, shutterRectTransform.anchoredPosition.y);
    }

    public float GetCurrentValue()
    {
        return (shutterRectTransform.anchoredPosition.x - minX) / (maxX - minX);
    }
}