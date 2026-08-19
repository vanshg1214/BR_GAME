using UnityEngine;
using PopstrikeVR.Gameplay;

namespace PopstrikeVR.Interaction
{
    /// <summary>
    /// Bridges the Meta XR OVRHand system with PopstrikeVR's physics and gesture logic.
    /// Attach this to your Left and Right OVRHandPrefab in the scene.
    /// </summary>
    [RequireComponent(typeof(OVRHand))]
    [RequireComponent(typeof(OVRSkeleton))]
    [RequireComponent(typeof(Rigidbody))]
    public class MetaHandIntegrator : MonoBehaviour, IHandVelocityProvider
    {
        private OVRHand ovrHand;
        private OVRSkeleton ovrSkeleton;
        private Rigidbody rb;
        
        private Vector3 previousPosition;
        private Vector3 currentVelocity;

        [Tooltip("Is this the left or right hand?")]
        public bool isLeftHand;

        private bool collidersInitialized = false;

        private void Awake()
        {
            ovrHand = GetComponent<OVRHand>();
            ovrSkeleton = GetComponent<OVRSkeleton>();
            
            // 1. The Rigidbody is REQUIRED on the root hand. 
            // In Unity, OnTriggerEnter only fires if at least ONE of the interacting objects has a Rigidbody.
            // Since our balloons don't have rigidbodies (to save performance), the hand MUST have a Kinematic one.
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        private void Start()
        {
            previousPosition = transform.position;

            // Register this hand directly into the GestureDetector so it can use 
            // the highly accurate 3D wrist-distance algorithm instead of pinch strength.
            if (GestureDetector.Instance != null)
            {
                if (isLeftHand)
                {
                    GestureDetector.Instance.leftHand = ovrHand;
                    GestureDetector.Instance.leftSkeleton = ovrSkeleton;
                }
                else
                {
                    GestureDetector.Instance.rightHand = ovrHand;
                    GestureDetector.Instance.rightSkeleton = ovrSkeleton;
                }
            }
        }

        private void Update()
        {
            // We must wait for the OVRSkeleton to initialize its bones before we can attach our colliders
            if (!collidersInitialized && ovrSkeleton.IsInitialized)
            {
                SetupBoneColliders();
                collidersInitialized = true;
            }
        }

        private void SetupBoneColliders()
        {
            // THE SOLUTION: Instead of a laggy Mesh Collider, we spawn 3 tiny, ultra-performant Sphere Colliders 
            // and attach them directly to specific bones. They will move dynamically as the fingers bend!

            // 1. Index Fingertip (For Trace / TMT pointing)
            // Note: Meta XR does not consistently store 'Tip' bones in the main Bones array. We use Index3 instead.
            AttachColliderToBone(OVRSkeleton.BoneId.Hand_Index3, 0.04f);

            // 2. Palm / Middle Knuckle (For Blaze Fists)
            AttachColliderToBone(OVRSkeleton.BoneId.Hand_Middle1, 0.06f);

            // 3. Pinky Base / Hand Edge (For Blade slashing)
            AttachColliderToBone(OVRSkeleton.BoneId.Hand_Pinky1, 0.05f);
        }

        private void AttachColliderToBone(OVRSkeleton.BoneId boneId, float radius)
        {
            foreach (var bone in ovrSkeleton.Bones)
            {
                if (bone.Id == boneId)
                {
                    // Create a child object on the bone
                    GameObject colObj = new GameObject($"Hitbox_{boneId}");
                    colObj.transform.SetParent(bone.Transform, false);
                    colObj.transform.localPosition = Vector3.zero;

                    // Add a cheap trigger sphere
                    SphereCollider sphere = colObj.AddComponent<SphereCollider>();
                    sphere.isTrigger = true;
                    sphere.radius = radius;

                    // Forward collisions back to this main script using a helper
                    var forwarder = colObj.AddComponent<HandColliderForwarder>();
                    forwarder.VelocityProvider = this;

                    return;
                }
            }
        }

        private void FixedUpdate()
        {
            // Calculate real-world hand velocity manually for perfect accuracy
            Vector3 currentPos = transform.position;
            currentVelocity = (currentPos - previousPosition) / Time.fixedDeltaTime;
            previousPosition = currentPos;
        }

        public Vector3 GetVelocity()
        {
            return currentVelocity;
        }
    }
}
