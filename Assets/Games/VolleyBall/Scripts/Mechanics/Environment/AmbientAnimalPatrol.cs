using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Rehab.Volleyball.Mechanics
{
    /// <summary>
    /// Advanced VR-friendly waypoint patrol script for ambient background animals.
    /// Walks in a ping-pong sequence and executes specific actions (Jump, Stun) at waypoints.
    /// </summary>
    public class AmbientAnimalPatrol : MonoBehaviour
    {
        [Header("Patrol Settings")]
        [Tooltip("The waypoints this animal will walk between. Must have the AnimalWaypoint script attached.")]
        public List<AnimalWaypoint> waypoints = new List<AnimalWaypoint>();
        
        [Tooltip("How fast the animal walks.")]
        public float walkSpeed = 1.0f;
        
        [Tooltip("How fast the animal rotates to face its next target.")]
        public float rotationSpeed = 3.0f;
        
        [Header("Organic Movement")]
        [Tooltip("How much the animal zig-zags left and right (in degrees) while walking. Set to 0 for a straight robotic line.")]
        public float wobbleIntensity = 15f;
        [Tooltip("How fast the animal weaves left and right.")]
        public float wobbleSpeed = 2.5f;

        [Header("Idle Behavior")]
        [Tooltip("Chance (0 to 1) to trigger the 'IdleAction' animation while waiting at a normal waypoint.")]
        public float idleActionChance = 0.5f;

        [Header("Animation Parameters (String names)")]
        [Tooltip("The boolean parameter in the Animator that controls walking.")]
        public string isWalkingBool = "IsWalking";
        [Tooltip("The trigger parameter for normal random actions (like eating).")]
        public string idleActionTrigger = "IdleAction";
        [Tooltip("The trigger parameter for jumping.")]
        public string jumpTrigger = "Jump";
        [Tooltip("The trigger parameter for getting stunned.")]
        public string stunTrigger = "Stun";

        private Animator anim;
        private Coroutine patrolCoroutine;
        
        // Ping-Pong tracking
        private int currentWaypointIndex = 0;
        private bool isMovingForward = true;

        private void Start()
        {
            // Search on this exact object first, then all children
            anim = GetComponent<Animator>();
            if (anim == null) anim = GetComponentInChildren<Animator>(true);
            
            if (anim == null)
                Debug.LogWarning($"[AmbientAnimalPatrol] No Animator found on '{gameObject.name}' or any of its children! Animations will not play.");
            else
                Debug.Log($"[AmbientAnimalPatrol] Found Animator on '{anim.gameObject.name}'. Walking param: '{isWalkingBool}'");
            
            // Remove any null waypoints that might have been accidentally left in the list
            waypoints.RemoveAll(w => w == null);
            
            if (waypoints.Count > 0)
            {
                // Snap to first waypoint
                transform.position = new Vector3(waypoints[0].transform.position.x, transform.position.y, waypoints[0].transform.position.z);
                patrolCoroutine = StartCoroutine(PatrolRoutine());
            }
        }

        private IEnumerator PatrolRoutine()
        {
            while (true)
            {
                // 1. Determine the next waypoint index (Ping-Pong logic)
                if (waypoints.Count > 1)
                {
                    if (isMovingForward)
                    {
                        currentWaypointIndex++;
                        if (currentWaypointIndex >= waypoints.Count)
                        {
                            currentWaypointIndex = waypoints.Count - 2; // Step back one
                            isMovingForward = false;
                        }
                    }
                    else
                    {
                        currentWaypointIndex--;
                        if (currentWaypointIndex < 0)
                        {
                            currentWaypointIndex = 1; // Step forward one
                            isMovingForward = true;
                        }
                    }
                }
                else
                {
                    currentWaypointIndex = 0;
                }

                AnimalWaypoint currentTarget = waypoints[currentWaypointIndex];
                Debug.Log($"[AmbientAnimalPatrol] -> Setting target to Point {currentWaypointIndex + 1}. Starting to walk.");

                // 2. Turn to face the waypoint
                yield return StartCoroutine(SmoothTurnTowards(currentTarget.transform.position));

                // 3. Walk towards the waypoint organically
                if (anim != null) 
                {
                    anim.SetBool(isWalkingBool, true);
                    Debug.Log($"[AmbientAnimalPatrol] SetBool('{isWalkingBool}', true) called!");
                }
                
                // Randomize the starting wobble offset so multiple animals don't weave in sync
                float timeOffset = Random.Range(0f, 100f);
                
                while (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                                        new Vector3(currentTarget.transform.position.x, 0, currentTarget.transform.position.z)) > 0.15f)
                {
                    Vector3 baseDir = (currentTarget.transform.position - transform.position).normalized;
                    baseDir.y = 0;
                    
                    if (baseDir.sqrMagnitude > 0.01f)
                    {
                        // Add organic wobble (zig-zag) by modifying the look direction
                        float wobbleOffset = Mathf.Sin((Time.time + timeOffset) * wobbleSpeed) * wobbleIntensity;
                        Vector3 wobblyDir = Quaternion.Euler(0, wobbleOffset, 0) * baseDir;
                        
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(wobblyDir), rotationSpeed * Time.deltaTime);
                    }
                    
                    // Move forward in the exact direction the animal is physically facing, creating a natural curved path!
                    transform.position += transform.forward * walkSpeed * Time.deltaTime;
                    
                    yield return null;
                }

                // 4. Stop Walking
                Debug.Log($"[AmbientAnimalPatrol] Reached Point {currentWaypointIndex + 1}. Stopping walk.");
                if (anim != null) anim.SetBool(isWalkingBool, false);

                // 5. Execute Action based on the Waypoint's configuration
                Debug.Log($"[AmbientAnimalPatrol] Executing Action: {currentTarget.action}");
                switch (currentTarget.action)
                {
                    case WaypointAction.Jump:
                        if (anim != null && !string.IsNullOrEmpty(jumpTrigger)) 
                        {
                            anim.SetTrigger(jumpTrigger);
                            Debug.Log($"[AmbientAnimalPatrol] Triggered '{jumpTrigger}'!");
                        }
                        yield return new WaitForSeconds(currentTarget.waitTime);
                        break;
                        
                    case WaypointAction.StunAtPlayer:
                        // Find the player (Main Camera)
                        Transform playerHead = Camera.main != null ? Camera.main.transform : null;
                        if (playerHead != null)
                        {
                            // Turn to face the player smoothly before stunning
                            yield return StartCoroutine(SmoothTurnTowards(playerHead.position));
                        }
                        
                        // Play Stun
                        if (anim != null && !string.IsNullOrEmpty(stunTrigger)) 
                        {
                            anim.SetTrigger(stunTrigger);
                            Debug.Log($"[AmbientAnimalPatrol] Triggered '{stunTrigger}'!");
                        }
                        
                        // Wait for stun to finish
                        yield return new WaitForSeconds(currentTarget.waitTime);
                        break;
                        
                    case WaypointAction.None:
                    default:
                        // Just regular waiting, maybe trigger random idle action
                        if (anim != null && !string.IsNullOrEmpty(idleActionTrigger) && Random.value <= idleActionChance)
                        {
                            anim.SetTrigger(idleActionTrigger);
                            Debug.Log($"[AmbientAnimalPatrol] Triggered random '{idleActionTrigger}'!");
                        }
                        yield return new WaitForSeconds(currentTarget.waitTime);
                        break;
                }
                
                Debug.Log($"[AmbientAnimalPatrol] Finished waiting at Point {currentWaypointIndex + 1}. Moving to next...");
            }
        }
        
        private IEnumerator SmoothTurnTowards(Vector3 targetPos)
        {
            Vector3 direction = (targetPos - transform.position).normalized;
            direction.y = 0; // Keep rotation flat on the ground
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                while (Quaternion.Angle(transform.rotation, targetRotation) > 5f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                    yield return null;
                }
            }
        }
        
        // Let the editor draw lines between waypoints so the user can see the paths easily!
        private void OnDrawGizmosSelected()
        {
            if (waypoints == null || waypoints.Count == 0) return;
            
            Gizmos.color = Color.green;
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] != null)
                {
                    // Draw a different color/shape based on the action so the user can visually identify them!
                    if (waypoints[i].action == WaypointAction.StunAtPlayer)
                        Gizmos.color = Color.red;
                    else if (waypoints[i].action == WaypointAction.Jump)
                        Gizmos.color = Color.blue;
                    else
                        Gizmos.color = Color.green;

                    Gizmos.DrawWireSphere(waypoints[i].transform.position, 0.25f);
                    
                    // Draw lines showing the ping-pong path
                    Gizmos.color = Color.green;
                    if (i < waypoints.Count - 1 && waypoints[i+1] != null)
                    {
                        Gizmos.DrawLine(waypoints[i].transform.position, waypoints[i+1].transform.position);
                    }
                }
            }
        }
    }
}
