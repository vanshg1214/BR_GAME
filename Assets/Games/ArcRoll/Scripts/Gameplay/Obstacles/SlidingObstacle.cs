using UnityEngine;

namespace ArcRoll.Gameplay.Obstacles
{
    public class SlidingObstacle : MonoBehaviour
    {
        [Header("Sliding Settings")]
        [Tooltip("How far the obstacle slides left and right from its center.")]
        [SerializeField] private float slideDistance = 1.0f;
        
        [Tooltip("How fast the obstacle moves.")]
        [SerializeField] private float slideSpeed = 2.0f;
        
        private Vector3 startPos;
        private float internalTime;

        private void Start()
        {
            startPos = transform.localPosition;
            internalTime = 0f;
        }

        private void Update()
        {
            internalTime += Time.deltaTime;
            // Use internal timer so Sin(0) = 0, meaning it ALWAYS starts perfectly in the center!
            float offset = Mathf.Sin(internalTime * slideSpeed) * slideDistance;
            
            // Move along local X axis so it slides properly regardless of lane rotation
            transform.localPosition = startPos + new Vector3(offset, 0, 0);
        }
    }
}
