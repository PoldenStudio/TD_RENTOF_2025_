using UnityEngine;


namespace DemolitionStudios.DemolitionMedia
{
	[RequireComponent(typeof(AudioSource))]
	[AddComponentMenu("Demolition Media/Media Audio Source")]
	public class MediaAudioSource : MonoBehaviour
	{
		// Target media
		public Media media;
		// Audio source component
		private AudioSource _audioSource;

		void Awake()
		{
            //Utilities.LogWarning("[DemolitionMedia] MediaAudioSource::Awake");
            _audioSource = GetComponent<AudioSource>();

            if (media == null)
            {
                media = GetComponent<Media>();
            }
        }

		void Start()
		{
			SetActiveMedia(media);
		}

		void OnDestroy()
		{
            //Utilities.LogWarning("[DemolitionMedia] MediaAudioSource::OnDestroy");
            SetActiveMedia(null);
            _audioSource = null;
        }


        // Called by the unity audio thread to feed the samples data to the DSP buffer
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (media != null && media.IsPlaying)
            {
                // Note: since data.Length is number of float samples for all the channels,
                // to get this value for a single channel, we should divide it by the channels count
                NativeDll.FillAudioBuffer(media.MediaId, data, 0, data.Length / channels, channels);
            }
        }

        public void SetActiveMedia(Media newPlayer)
		{
			// When changing the media player, handle event subscriptions
			if (media != null)
			{
				media.Events.RemoveListener(OnMediaPlayerEvent);
				media = null;
			}

			media = newPlayer;
			if (media != null)
			{
				media.Events.AddListener(OnMediaPlayerEvent);
			}
		}

		// Callback function to handle media events
		private void OnMediaPlayerEvent(Media media, MediaEvent.Type et, MediaError errorCode)
		{
			switch (et)
			{
			case MediaEvent.Type.PlaybackStarted:
			case MediaEvent.Type.PlaybackResumed:
				_audioSource.Play();
				break;
            case MediaEvent.Type.Closed:
                //Utilities.LogWarning("AudioSource::Stop");
                _audioSource.Stop();
                break;
            }
		}

	}
}