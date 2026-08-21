using UnityEngine;

namespace ArcRoll.Gameplay.Obstacles
{
    public class PopupObstacle : MonoBehaviour
    {
        private enum PopupState
        {
            Rising,
            StayingUp,
            Lowering,
            StayingDown
        }

        [Header("Height Settings")]
        [Tooltip("How high the obstacle rises relative to its start position.")]
        [SerializeField] private float popupHeight = 0.5f;

        [Tooltip("Speed of the rising and lowering transitions.")]
        [SerializeField] private float transitionSpeed = 4f;

        [Header("Timing Settings")]
        [Tooltip("Seconds the obstacle remains fully up.")]
        [SerializeField] private float upDuration = 1.5f;

        [Tooltip("Seconds the obstacle remains fully down.")]
        [SerializeField] private float downDuration = 1.5f;

        private Vector3 startPos;
        private Vector3 targetUpPos;
        private PopupState currentState = PopupState.StayingDown;
        private float timer = 0f;

        private void Start()
        {
            startPos = transform.localPosition;
            targetUpPos = startPos + new Vector3(0, popupHeight, 0);
            
            // Start fully down, waiting to pop up
            transform.localPosition = startPos;
            timer = downDuration;
            currentState = PopupState.StayingDown;
        }

        private void Update()
        {
            switch (currentState)
            {
                case PopupState.StayingDown:
                    timer -= Time.deltaTime;
                    if (timer <= 0f)
                    {
                        currentState = PopupState.Rising;
                    }
                    break;

                case PopupState.Rising:
                    transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetUpPos, transitionSpeed * Time.deltaTime);
                    if (Vector3.Distance(transform.localPosition, targetUpPos) < 0.001f)
                    {
                        transform.localPosition = targetUpPos;
                        currentState = PopupState.StayingUp;
                        timer = upDuration;
                    }
                    break;

                case PopupState.StayingUp:
                    timer -= Time.deltaTime;
                    if (timer <= 0f)
                    {
                        currentState = PopupState.Lowering;
                    }
                    break;

                case PopupState.Lowering:
                    transform.localPosition = Vector3.MoveTowards(transform.localPosition, startPos, transitionSpeed * Time.deltaTime);
                    if (Vector3.Distance(transform.localPosition, startPos) < 0.001f)
                    {
                        transform.localPosition = startPos;
                        currentState = PopupState.StayingDown;
                        timer = downDuration;
                    }
                    break;
            }
        }
    }
}
