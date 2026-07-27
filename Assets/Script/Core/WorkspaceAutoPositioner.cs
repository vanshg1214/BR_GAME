using System.Collections;
using System.Reflection;
using UnityEngine;

namespace WhackAMole
{
    public class WorkspaceAutoPositioner : MonoBehaviour
    {
        public static WorkspaceAutoPositioner Instance { get; private set; }

        [Header("Positioning")]
        [SerializeField] private float defaultDistanceFromPlayer = 0.3f;
        [SerializeField] private float playerShoulderOffset = 0.35f;
        [SerializeField] private float heightMultiplier = 0.80f;

        [Header("Chair Calibration")]
        [SerializeField] private GameObject chairPrefab;
        [SerializeField] private float chairYOffset = -0.7f;
        [SerializeField] private float chairZOffset = 0.0f;

        private GameObject chairInstance;
        private CharacterController cachedCC;
        private Rigidbody cachedRB;
        private MonoBehaviour cachedOvrPlayer;

        private bool hasPositionedProperly = false;
        private float trackingStabilizeTimer = 1.5f;
        private float currentXShift = 0f;

        private Vector3 lastLocalHeadPos;
        private Quaternion lastLocalHeadRot;
        private Vector3 lastWorldHeadPos;
        private Quaternion lastWorldHeadRot;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null);
        }

        private IEnumerator Start()
        {
            transform.position = new Vector3(0, -5f, 0);
            FixPlayerPhysics();
            CollisionIsolator.IsolateObject(gameObject);
            yield return null;
        }

        private void FixPlayerPhysics()
        {
            if (Camera.main != null)
            {
                ConfigureTrackingOriginToFloor();

                cachedCC = Camera.main.GetComponentInParent<CharacterController>();
                if (cachedCC != null) cachedCC.enabled = false;

                cachedOvrPlayer = null;
                System.Type ovrPlayerType = null;
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    ovrPlayerType = assembly.GetType("OVRPlayerController");
                    if (ovrPlayerType != null) break;
                }
                
                if (ovrPlayerType != null)
                {
                    cachedOvrPlayer = Camera.main.GetComponentInParent(ovrPlayerType) as MonoBehaviour;
                }
                if (cachedOvrPlayer != null) cachedOvrPlayer.enabled = false;

                cachedRB = Camera.main.GetComponentInParent<Rigidbody>();
                if (cachedRB != null) cachedRB.isKinematic = true;

                Transform playerRoot = cachedCC != null ? cachedCC.transform : (cachedRB != null ? cachedRB.transform : Camera.main.transform.root);
                CollisionIsolator.RegisterPlayer(playerRoot);
            }
        }

        private void ConfigureTrackingOriginToFloor()
        {
            System.Type ovrManagerType = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                ovrManagerType = assembly.GetType("OVRManager");
                if (ovrManagerType != null) break;
            }

            if (ovrManagerType != null)
            {
                PropertyInfo instanceProp = ovrManagerType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp != null)
                {
                    object managerInstance = instanceProp.GetValue(null);
                    if (managerInstance != null)
                    {
                        PropertyInfo trackingProp = ovrManagerType.GetProperty("trackingOriginType", BindingFlags.Public | BindingFlags.Instance);
                        if (trackingProp != null)
                        {
                            trackingProp.SetValue(managerInstance, System.Enum.ToObject(trackingProp.PropertyType, 1));
                        }
                    }
                }
            }

            System.Type xrOriginType = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                xrOriginType = assembly.GetType("Unity.XR.CoreUtils.XROrigin");
                if (xrOriginType != null) break;
            }

            if (xrOriginType != null && Camera.main != null)
            {
                Component xrOrigin = Camera.main.GetComponentInParent(xrOriginType);
                if (xrOrigin != null)
                {
                    PropertyInfo modeProp = xrOriginType.GetProperty("RequestedTrackingOriginMode", BindingFlags.Public | BindingFlags.Instance);
                    if (modeProp != null)
                    {
                        modeProp.SetValue(xrOrigin, System.Enum.ToObject(modeProp.PropertyType, 2));
                    }
                }
            }
        }

        private void Update()
        {
            if (Camera.main != null)
            {
                if (!hasPositionedProperly)
                {
                    if (trackingStabilizeTimer > 0f)
                    {
                        trackingStabilizeTimer -= Time.deltaTime;
                        lastLocalHeadPos = Camera.main.transform.localPosition;
                        lastLocalHeadRot = Camera.main.transform.localRotation;
                        lastWorldHeadPos = Camera.main.transform.position;
                        lastWorldHeadRot = Camera.main.transform.rotation;
                        return;
                    }

                    if (Camera.main.transform.position.sqrMagnitude > 0.01f)
                    {
                        FixPlayerPhysics();
                        RepositionBoard();
                        hasPositionedProperly = true;
                    }
                    lastLocalHeadPos = Camera.main.transform.localPosition;
                    lastLocalHeadRot = Camera.main.transform.localRotation;
                    lastWorldHeadPos = Camera.main.transform.position;
                    lastWorldHeadRot = Camera.main.transform.rotation;
                }
                else
                {
                    if (cachedCC != null && cachedCC.enabled) cachedCC.enabled = false;
                    if (cachedOvrPlayer != null && cachedOvrPlayer.enabled) cachedOvrPlayer.enabled = false;
                    if (cachedRB != null && !cachedRB.isKinematic) cachedRB.isKinematic = true;

                    Vector3 currentLocalPos = Camera.main.transform.localPosition;
                    Quaternion currentLocalRot = Camera.main.transform.localRotation;
                    Vector3 currentWorldPos = Camera.main.transform.position;
                    Quaternion currentWorldRot = Camera.main.transform.rotation;
                    
                    float posJump = Vector3.Distance(new Vector3(currentLocalPos.x, 0, currentLocalPos.z), new Vector3(lastLocalHeadPos.x, 0, lastLocalHeadPos.z));
                    float rotJump = Mathf.Abs(Mathf.DeltaAngle(currentLocalRot.eulerAngles.y, lastLocalHeadRot.eulerAngles.y));

                    float worldPosJump = Vector3.Distance(new Vector3(currentWorldPos.x, 0, currentWorldPos.z), new Vector3(lastWorldHeadPos.x, 0, lastWorldHeadPos.z));
                    float worldRotJump = Mathf.Abs(Mathf.DeltaAngle(currentWorldRot.eulerAngles.y, lastWorldHeadRot.eulerAngles.y));

                    if (posJump > 0.15f || rotJump > 15f || worldPosJump > 0.15f || worldRotJump > 15f) RepositionBoard();
                    
                    lastLocalHeadPos = currentLocalPos;
                    lastLocalHeadRot = currentLocalRot;
                    lastWorldHeadPos = currentWorldPos;
                    lastWorldHeadRot = currentWorldRot;
                }
            }
        }

        public void UpdateHandShiftOnly()
        {
            float newXShift = 0f;
            if (WorkspaceMapper.Instance != null)
            {
                WorkspaceMapper.Instance.GetWorkspaceDimensions(out float w, out float d, out newXShift);
            }

            Vector3 rightDir = transform.right;
            transform.position = transform.position - (rightDir * currentXShift) + (rightDir * newXShift);
            currentXShift = newXShift;
        }

        public void RepositionBoard()
        {
            if (Camera.main == null) return;
            FixPlayerPhysics();

            float distance = defaultDistanceFromPlayer;
            float xShift = 0f;

            if (GameManager.Instance?.RehabProfile != null)
            {
                RehabProfileSO profile = GameManager.Instance.RehabProfile;
                float maxReach = profile.armLength * Mathf.Clamp01(profile.maxFlexion / 90f);
                float minReach = 0.2f;

                HoleLayoutGenerator layout = GetComponent<HoleLayoutGenerator>();
                if (layout != null) minReach = layout.GetMinRadius(maxReach);

                distance = Mathf.Max(playerShoulderOffset - minReach, 0.10f);
            }

            if (WorkspaceMapper.Instance != null)
            {
                WorkspaceMapper.Instance.GetWorkspaceDimensions(out float w, out float d, out xShift);
            }

            Transform head = Camera.main.transform;
            Vector3 flatForward = head.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
            flatForward.Normalize();

            Vector3 position = head.position + flatForward * distance;
            position.y = Mathf.Max(head.position.y * heightMultiplier, 0.1f);

            Vector3 rightDir = Vector3.Cross(Vector3.up, flatForward).normalized;
            position += rightDir * xShift;
            currentXShift = xShift;

            transform.SetPositionAndRotation(position, Quaternion.LookRotation(flatForward));

            if (chairPrefab != null)
            {
                if (chairInstance == null)
                {
                    chairInstance = GameObject.Find("DynamicChair") ?? Instantiate(chairPrefab);
                    chairInstance.name = "DynamicChair";
                }

                if (chairInstance != null)
                {
                    Vector3 chairPos = head.position + flatForward * chairZOffset;
                    chairPos.y = head.position.y + chairYOffset;
                    chairInstance.transform.SetPositionAndRotation(chairPos, Quaternion.LookRotation(flatForward));
                    CollisionIsolator.IsolateObject(chairInstance);
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && chairInstance != null && Camera.main != null)
            {
                Transform head = Camera.main.transform;
                Vector3 flatForward = head.forward;
                flatForward.y = 0f;
                if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
                flatForward.Normalize();

                Vector3 chairPos = head.position + flatForward * chairZOffset;
                chairPos.y = head.position.y + chairYOffset;
                chairInstance.transform.position = chairPos;
            }
        }
#endif
    }
}
