using System.Collections.Generic;
using UnityEngine;


namespace DemolitionStudios.DemolitionMedia.Examples
{
    public class HMD360Controls : MonoBehaviour
    {
        public virtual void Start()
        {
            var xrDisplaySubsystems = new List<UnityEngine.XR.XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems<UnityEngine.XR.XRDisplaySubsystem>(xrDisplaySubsystems);
            foreach (var xrDisplay in xrDisplaySubsystems)
            {
                if (xrDisplay.running)
                {
                    this.enabled = false;
                    return;
                }
            }

            if (SystemInfo.supportsGyroscope)
            {
                Input.gyro.enabled = true;
                transform.parent.Rotate(new Vector3(90f, 0f, 0f));
            }
        }

        public void OnDestroy()
        {
            if (SystemInfo.supportsGyroscope)
            {
                Input.gyro.enabled = false;
            }
        }

        private float _spinX;
        private float _spinY;

        public virtual void Update()
        {
            if (SystemInfo.supportsGyroscope && Input.gyro.enabled)
            {
                // Invert the z and w of the gyro attitude
                transform.localRotation = new Quaternion(Input.gyro.attitude.x, Input.gyro.attitude.y, -Input.gyro.attitude.z, -Input.gyro.attitude.w);
            }
            // Also rotate from mouse / touch input
            else if (Input.GetMouseButton(0))
            {
                float h = 40.0f * -Input.GetAxis("Mouse X") * Time.deltaTime;
                float v = 40.0f * Input.GetAxis("Mouse Y") * Time.deltaTime;
                h = Mathf.Clamp(h, -0.5f, 0.5f);
                v = Mathf.Clamp(v, -0.5f, 0.5f);
                _spinX += h;
                _spinY += v;
            }
            if (!Mathf.Approximately(_spinX, 0f) || !Mathf.Approximately(_spinY, 0f))
            {
                transform.Rotate(Vector3.up, _spinX);
                transform.Rotate(Vector3.right, _spinY);

                _spinX = Mathf.MoveTowards(_spinX, 0f, 5f * Time.deltaTime);
                _spinY = Mathf.MoveTowards(_spinY, 0f, 5f * Time.deltaTime);
            }
        }
    }
}