using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;


namespace DemolitionStudios.DemolitionMedia
{
    [AddComponentMenu("Demolition Media/LFO Clock")]
    public class LFOClockLocal : MonoBehaviour, IPropertyPreview
    {
        private LFOClockBase impl = new LFOClockBase();

        /// Target media
        public Media TargetMedia;

        [SerializeField] float _position = 0.0f;
        [SerializeField, Range(-10, 10)] public float _speed = 1;
        [SerializeField] public bool _loop = true;

        [SerializeField] public bool _frameQueueDrained = false;
        [SerializeField] public bool _useCorrection = false;
        [SerializeField] public bool _waitOnDrainedQueue = false;

        /// GUI skin
        public GUISkin skin;

        int _mouseDownFrame = 0;
        float _mouseDownTime = 0.0f;

        #region IPropertyPreview implementation

        public void GatherProperties(PlayableDirector director, IPropertyCollector driver)
        {
            driver.AddFromName<LFOClockLocal>(gameObject, "_time");
        }

        #endregion


        #region MonoBehaviour implementation

        void Awake()
        {
            impl.Awake();

            if (TargetMedia == null)
            {
                TargetMedia = GetComponent<Media>();
            }
        }

        void Start()
        {
            impl.Start(TargetMedia);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                impl.Pause = !impl.Pause;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha1))
                _speed = 1.0f;
            else if (Input.GetKeyDown(KeyCode.Alpha2))
                _speed = 2.0f;
            else if (Input.GetKeyDown(KeyCode.Alpha3))
                _speed = 3.0f;
            else if (Input.GetKeyDown(KeyCode.Alpha4))
                _speed = 4.0f;
            else if (Input.GetKeyDown(KeyCode.Alpha5))
               _speed = 0.5f;
            else if (Input.GetKeyDown(KeyCode.Alpha6))
                _speed = 6.0f;
            else if (Input.GetKeyDown(KeyCode.Alpha7))
                _speed = 7.0f;
            else if (Input.GetKeyDown(KeyCode.Alpha8))
                _speed = 8.0f;
            else if (Input.GetKeyDown(KeyCode.Alpha9))
                _speed = 9.0f;
            else if (Input.GetKeyDown(KeyCode.Alpha0))
                _speed = 0.0f;
            else if (Input.GetKeyDown(KeyCode.Equals))
                _speed *= 2;
            else if (Input.GetKeyDown(KeyCode.Minus))
                _speed = -1 * _speed;

            impl.Speed = _speed;

            if (Input.GetMouseButton(0))
            {
                float mousePosRelX = Input.mousePosition.x / Screen.width;
                _position = TargetMedia.DurationSeconds * mousePosRelX;
                int frame = (int)(_position * TargetMedia.VideoFramerate);
                if (frame != _mouseDownFrame)
                {
                    _mouseDownFrame = frame;
                    _mouseDownTime = Time.realtimeSinceStartup;
                }
                impl.Position = _position;
            }
            if (Input.GetMouseButtonUp(0))
            {
                //Utilities.Log("Mouse up");
                if (!impl.Pause/* && Time.realtimeSinceStartup - _mouseDownTime < 5e-1*/)
                {
                    //Utilities.Log("Seek correction set");
                    impl.SetMakeSeekCorrection(true);
                }
            }
        }


        void LateUpdate()
        {
            impl.Update(true, true);
        }

        void OnGUI()
        {
            impl.OnGUI();
        }

        #endregion
    }
}