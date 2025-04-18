/*using UnityEngine;
using System.Collections;
using System;
using DemolitionStudios.DemolitionMedia;

public class CometController : MonoBehaviour
{
    [SerializeField] private RectTransform cometRect;
    [SerializeField] private float travelDuration = 1f;
    [SerializeField] private Vector2 startOffset = new(-0.5f, 0f);
    [SerializeField] private Vector2 endOffset = new(0.5f, 0f);
    private Coroutine _travelCoroutine;
    private Action _onCometFinishedCallback;

    [Header("Demolition Media")]
    [SerializeField] private Media _demolitionMedia;
    private IMediaPlayer _mediaPlayer;

    private void Awake()
    {
        if (cometRect == null)
        {
            Debug.LogError("[CometController] Comet RectTransform not assigned!");
        }
        else
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 100;
            }
        }
        //gameObject.SetActive(false);
    }

    private void Start()
    {
        if (_demolitionMedia == null)
        {
            Debug.LogError("[CometController] Demolition Media is not assigned!");
            return;
        }

        _mediaPlayer = new DemolitionMediaPlayer(_demolitionMedia);
        _mediaPlayer.Pause();
    }



    public Coroutine StartCometTravel(Action onFinished = null)
    {
        if (_travelCoroutine != null)
        {
            StopCoroutine(_travelCoroutine);
            _travelCoroutine = null;
        }

        _onCometFinishedCallback = onFinished;
        _travelCoroutine = StartCoroutine(TravelAnimation());

        Debug.Log("[CometController] Starting comet travel animation");
        return _travelCoroutine;
    }

    private IEnumerator TravelAnimation()
    {
        //gameObject.SetActive(true);

        RectTransform parentRect = (RectTransform)cometRect.parent;
        float screenWidth = parentRect.rect.width;
        float screenHeight = parentRect.rect.height;

        Vector2 startPos = new Vector2(startOffset.x * screenWidth, startOffset.y * screenHeight);
        Vector2 endPos = new Vector2(screenWidth + endOffset.x * screenWidth, endOffset.y * screenHeight);

        cometRect.anchoredPosition = startPos;

        Debug.Log($"[CometController] Comet animation from {startPos} to {endPos}, duration: {travelDuration}s");

        float timer = 0f;
        while (timer < travelDuration)
        {
            timer += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(timer / travelDuration);
            cometRect.anchoredPosition = Vector2.Lerp(startPos, endPos, normalizedTime);
            yield return null;
        }

        cometRect.anchoredPosition = endPos;

        Debug.Log("[CometController] Comet animation completed");

        _onCometFinishedCallback?.Invoke();
        _onCometFinishedCallback = null;

        //gameObject.SetActive(false);
    }

    public void ResetComet()
    {
        if (_travelCoroutine != null)
        {
            StopCoroutine(_travelCoroutine);
            _travelCoroutine = null;
        }

        RectTransform parentRect = (RectTransform)cometRect.parent;
        float screenWidth = parentRect.rect.width;
        float screenHeight = parentRect.rect.height;

        Vector2 startPos = new Vector2(startOffset.x * screenWidth, startOffset.y * screenHeight);
        cometRect.anchoredPosition = startPos;
        //gameObject.SetActive(false);

        Debug.Log("[CometController] Comet reset to starting position.");
    }

    public void SetOnCometFinishedCallback(Action callback)
    {
        _onCometFinishedCallback = callback;
    }
}*/