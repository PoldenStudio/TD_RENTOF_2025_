using UnityEngine;


namespace DemolitionStudios.DemolitionMedia.Examples
{
    [RequireComponent(typeof(Transform))]
    public class RandomRotator : MonoBehaviour
    {
        void Update()
        {
            this.transform.Rotate(speed * dir * Time.deltaTime);

            time -= Time.deltaTime;
            if (time <= 0f)
                MakeNextRotation();
        }

        void Awake()
        {
            MakeNextRotation();
        }

        private void MakeNextRotation()
        {
            time = Random.Range(1f, 2f);
            speed = Random.Range(50f, 100f);
            dir = Random.onUnitSphere;
        }

        // Time remaining for the current rotation direction
        private float time;
        // Rotation speed
        private float speed;
        // Rotation direction
        private Vector3 dir;
    }
}