using UnityEngine;

namespace ArcRoll.Gameplay
{
    /// <summary>
    /// Dynamically creates and manages a glowing TrailRenderer on the ball.
    /// It intelligently turns off while the ball is being held or spawned, 
    /// and only paints a line in the air while it is actively thrown.
    /// </summary>
    [RequireComponent(typeof(Ball))]
    [RequireComponent(typeof(TrailRenderer))]
    public class ArcRollBallTrail : MonoBehaviour
    {
        private TrailRenderer trail;
        private Ball ball;

        private void Awake()
        {
            ball = GetComponent<Ball>();
            trail = GetComponent<TrailRenderer>();

            // Start with the trail turned OFF so it doesn't draw lines while the cannon is aiming!
            trail.emitting = false;
            
            // Listen to state changes
            ball.OnStateChanged += OnBallStateChanged;
        }

        private void OnDestroy()
        {
            if (ball != null)
            {
                ball.OnStateChanged -= OnBallStateChanged;
            }
        }

        private void OnBallStateChanged(Ball b, Ball.BallState state)
        {
            if (trail == null) return;

            // Only turn the trail on when it is flying through the air!
            if (state == Ball.BallState.Thrown)
            {
                trail.Clear(); // Erase any old garbage lines
                trail.emitting = true;
            }
            // Turn it off when grabbed, dead (hit the floor), or just sitting in the rack.
            else
            {
                trail.emitting = false;
            }
        }


    }
}
