using System.Collections;
using UnityEngine;

namespace WhackAMole
{
    public class BirdFlightController : MonoBehaviour
    {
        public static int ActiveBirdCount = 0;

        [Header("Flight Settings")]
        [Tooltip("How fast the bird flies across the table (meters per second).")]
        [SerializeField] private float flightSpeed = 1.5f;
        
        [Header("Flight Limits (0 = Auto Calculate from Profile)")]
        [SerializeField] private float minFlightHeight = 0f;
        [SerializeField] private float maxFlightHeight = 0f;
        [SerializeField] private float minFlightDepth = 0f;
        [SerializeField] private float maxFlightDepth = 0f;

        [Header("Animation Links")]
        [SerializeField] private string flyAnimTrigger = "Fly";
        [SerializeField] private string celebrateAnimTrigger = "Celebrate";

        [Header("Coconut Placement")]
        [Tooltip("Local offset adjustment to place the coconut perfectly under the bird's feet.")]
        [SerializeField] private Vector3 coconutLocalOffset = Vector3.zero;

        [Header("Debug View (Read Only)")]
        [SerializeField] private float debugAutoMinHeight;
        [SerializeField] private float debugAutoMaxHeight;
        [SerializeField] private float debugAutoMinDepth;
        [SerializeField] private float debugAutoMaxDepth;

        private Animator anim;
        private Coroutine flightCoroutine;
        private Collider[] allColliders;

        private void Awake()
        {
            anim = GetComponentInChildren<Animator>();
            allColliders = GetComponentsInChildren<Collider>(true);
        }

        private void OnEnable()
        {
            if (WorkspaceAutoPositioner.Instance == null)
            {
                Debug.LogWarning("[BirdFlightController] WorkspaceAutoPositioner is missing! Disabling bird.");
                gameObject.SetActive(false);
                return;
            }

            ActiveBirdCount++;

            allColliders = GetComponentsInChildren<Collider>(true);
            SetAllColliders(false);

            flightCoroutine = StartCoroutine(FlightRoutine());
        }

        private void OnDisable()
        {
            ActiveBirdCount = Mathf.Max(0, ActiveBirdCount - 1);
            if (flightCoroutine != null)
            {
                StopCoroutine(flightCoroutine);
                flightCoroutine = null;
            }
        }

        private IEnumerator FlightRoutine()
        {
            Transform table = WorkspaceAutoPositioner.Instance.transform;
            bool flyLeftToRight = Random.value > 0.5f;

            float currentFlightHeight = 0.7f;
            float calcMinHeight = 0.75f;
            float calcMaxHeight = 1.0f;

            if (GameManager.Instance != null && GameManager.Instance.RehabProfile != null)
            {
                RehabProfileSO profile = GameManager.Instance.RehabProfile;
                calcMaxHeight = Mathf.Max(calcMinHeight + 0.1f, profile.armLength * Mathf.Clamp01(profile.maxFlexion / 90f));
            }

            debugAutoMinHeight = calcMinHeight;
            debugAutoMaxHeight = calcMaxHeight;

            if (minFlightHeight > 0f && maxFlightHeight > 0f)
                currentFlightHeight = Random.Range(minFlightHeight, maxFlightHeight);
            else
                currentFlightHeight = Random.Range(calcMinHeight, calcMaxHeight);

            float hoverZ = 0.4f;
            float calcMinZ = 0.15f;
            float calcMaxZ = 0.6f;

            if (GameManager.Instance != null && GameManager.Instance.RehabProfile != null)
                calcMaxZ = Mathf.Max(calcMinZ + 0.1f, GameManager.Instance.RehabProfile.armLength * 0.85f);

            debugAutoMinDepth = calcMinZ;
            debugAutoMaxDepth = calcMaxZ;

            if (minFlightDepth > 0f && maxFlightDepth > 0f)
                hoverZ = Random.Range(minFlightDepth, maxFlightDepth);
            else
                hoverZ = Random.Range(calcMinZ, calcMaxZ);

            float currentFlightDist = 4.0f;
            if (GameManager.Instance != null && GameManager.Instance.RehabProfile != null)
            {
                RehabProfileSO profile = GameManager.Instance.RehabProfile;
                float abductionPercent = Mathf.Clamp01((profile.maxAbduction - 45f) / 135f);
                currentFlightDist = Mathf.Lerp(2.5f, 5.0f, abductionPercent);
            }

            Vector3 centerPos = table.position + (Vector3.up * currentFlightHeight) + (table.forward * hoverZ);
            Vector3 leftPos = centerPos - (table.right * currentFlightDist);
            Vector3 rightPos = centerPos + (table.right * currentFlightDist);

            Vector3 startPos = flyLeftToRight ? leftPos : rightPos;
            Vector3 endPos = flyLeftToRight ? rightPos : leftPos;

            float hoverX = Random.Range(-currentFlightDist * 0.15f, currentFlightDist * 0.15f);
            Vector3 hoverPos = table.position + (Vector3.up * currentFlightHeight)
                                              + (table.right * hoverX)
                                              + (table.forward * hoverZ);

            Vector3 flightDir = (endPos - startPos).normalized;
            Vector3 oppFlightDir = -flightDir;

            Vector3 playerPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            Vector3 toPlayerDir = playerPos - hoverPos;
            toPlayerDir.y = 0f;
            toPlayerDir = toPlayerDir.sqrMagnitude > 0.001f ? toPlayerDir.normalized : -table.forward;
            Vector3 backDir = -toPlayerDir;

            float distToHoverTotal = Vector3.Distance(startPos, hoverPos);
            float entryArcLength = Mathf.Min(0.8f, distToHoverTotal * 0.3f);
            
            float distToEndTotal = Vector3.Distance(hoverPos, endPos);
            float exitArcLength = Mathf.Min(2.8f, distToEndTotal * 0.6f);

            Vector3 hoverPosEntry = hoverPos - flightDir * entryArcLength;

            transform.position = startPos;
            
            Vector3 toHoverEntryDir = (hoverPosEntry - startPos).normalized;
            if (toHoverEntryDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(toHoverEntryDir);
            }

            CoconutPayload coconut = GetComponentInChildren<CoconutPayload>();
            if (coconut != null)
            {
                coconut.SetFacing(!flyLeftToRight);
                coconut.transform.localPosition += coconutLocalOffset;
            }

            SetAllColliders(false);

            float distToHoverEntry = Vector3.Distance(startPos, hoverPosEntry);
            float fastFlightSpeed = flightSpeed * 8.0f; 
            float durationToHoverEntry = Mathf.Max(distToHoverEntry / fastFlightSpeed, 0.15f);
            float elapsed = 0f;

            if (anim != null) anim.CrossFade("Fly_Normal", 0.1f);

            while (elapsed < durationToHoverEntry)
            {
                SetAllColliders(false);

                elapsed += Time.deltaTime;
                float easeT = elapsed / durationToHoverEntry;

                transform.position = Vector3.Lerp(startPos, hoverPosEntry, easeT);
                float bobOffset = Mathf.Sin(easeT * Mathf.PI * 2f) * 0.05f;
                transform.position += Vector3.up * bobOffset;

                yield return null;
            }
            transform.position = hoverPosEntry;

            if (anim != null) anim.CrossFade("Fly_Hover", 0.25f);

            Vector3 p0_entry = hoverPosEntry;
            Vector3 p3_entry = hoverPos;
            Vector3 p1_entry = p0_entry + flightDir * (entryArcLength * 0.5f);
            Vector3 p2_entry = p3_entry - toPlayerDir * (entryArcLength * 0.5f);

            float entryTurnDuration = 0.8f;
            float entryTurnElapsed = 0f;

            while (entryTurnElapsed < entryTurnDuration)
            {
                SetAllColliders(false);

                entryTurnElapsed += Time.deltaTime;
                float t = entryTurnElapsed / entryTurnDuration;

                Vector3 pos = EvaluateCubicBezier(p0_entry, p1_entry, p2_entry, p3_entry, t);
                transform.position = pos;

                Vector3 tangent = EvaluateCubicBezierDerivative(p0_entry, p1_entry, p2_entry, p3_entry, t).normalized;
                if (tangent != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(tangent);
                }

                yield return null;
            }
            transform.position = hoverPos;
            if (toPlayerDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(toPlayerDir);
            }

            if (coconut != null && !coconut.IsHit)
            {
                SetAllColliders(true);
            }

            float waitDuration = 5f;
            float waitElapsed = 0f;

            while (waitElapsed < waitDuration)
            {
                if (coconut != null && coconut.IsHit)
                {
                    yield return new WaitForSeconds(1.0f);
                    break;
                }

                waitElapsed += Time.deltaTime;
                float hoverBob = Mathf.Sin(waitElapsed * Mathf.PI * 1.5f) * 0.03f;
                transform.position = hoverPos + Vector3.up * hoverBob;

                if (Camera.main != null)
                {
                    Vector3 livePlayerPos = Camera.main.transform.position;
                    Vector3 liveToPlayerDir = (livePlayerPos - transform.position);
                    liveToPlayerDir.y = 0;
                    if (liveToPlayerDir != Vector3.zero)
                    {
                        transform.rotation = Quaternion.LookRotation(liveToPlayerDir.normalized);
                    }
                }
                yield return null;
            }

            SetAllColliders(false);
            bool wasHit = coconut != null && coconut.IsHit;

            float rejoinDist = Mathf.Max(exitArcLength, currentFlightDist * 0.45f);
            Vector3 hoverPosExit2 = hoverPos + flightDir * rejoinDist;

            Vector3 p0_exit = transform.position;
            Vector3 p3_exit = hoverPosExit2;

            Vector3 p1_exit = p0_exit + backDir * 0.9f + flightDir * 0.05f;
            Vector3 p2_exit = p3_exit - flightDir * 0.5f + backDir * 0.35f;

            float exitTurnDuration = 2.2f; 
            float exitTurnElapsed = 0f;

            Quaternion startExitRot = transform.rotation;
            float smoothBankDuration = 0.4f;

            while (exitTurnElapsed < exitTurnDuration)
            {
                SetAllColliders(false);
                exitTurnElapsed += Time.deltaTime;
                float t = exitTurnElapsed / exitTurnDuration;

                Vector3 pos = EvaluateCubicBezier(p0_exit, p1_exit, p2_exit, p3_exit, t);
                transform.position = pos;

                Vector3 tangent = EvaluateCubicBezierDerivative(p0_exit, p1_exit, p2_exit, p3_exit, t).normalized;
                if (tangent != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(tangent);
                    if (exitTurnElapsed < smoothBankDuration)
                    {
                        float bankT = exitTurnElapsed / smoothBankDuration;
                        transform.rotation = Quaternion.Slerp(startExitRot, targetRot, bankT);
                    }
                    else
                    {
                        transform.rotation = targetRot;
                    }
                }

                yield return null;
            }
            transform.position = hoverPosExit2;
            Vector3 hoverPosExit = hoverPosExit2;

            float distToEnd = Vector3.Distance(hoverPosExit, endPos);
            float durationToEnd = Mathf.Max(distToEnd / flightSpeed, 0.5f);
            elapsed = 0f;

            if (wasHit && anim != null)
            {
                anim.CrossFade("Fly_Normal", 0.2f);
            }

            Quaternion phase4StartRot = transform.rotation;
            Quaternion phase4TargetRot = flightDir != Vector3.zero ? Quaternion.LookRotation(flightDir) : transform.rotation;

            while (elapsed < durationToEnd)
            {
                SetAllColliders(false);
                elapsed += Time.deltaTime;
                float t = elapsed / durationToEnd;
                float bobOffset = Mathf.Sin(t * Mathf.PI * 2f) * 0.05f;
                transform.position = Vector3.Lerp(hoverPosExit, endPos, t) + Vector3.up * bobOffset;
                
                if (elapsed < 0.3f)
                {
                    transform.rotation = Quaternion.Slerp(phase4StartRot, phase4TargetRot, elapsed / 0.3f);
                }
                else
                {
                    transform.rotation = phase4TargetRot;
                }
                
                yield return null;
            }
            transform.position = endPos;

            if (!wasHit && ScoreManager.Instance != null)
            {
                ScoreManager.Instance.RegisterMiss();
            }
            gameObject.SetActive(false);
        }

        private void SetAllColliders(bool enabled)
        {
            if (allColliders == null) return;
            foreach (Collider col in allColliders)
                if (col != null) col.enabled = enabled;
        }

        private Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector3 p = uuu * p0;
            p += 3f * uu * t * p1;
            p += 3f * u * tt * p2;
            p += ttt * p3;

            return p;
        }

        private Vector3 EvaluateCubicBezierDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            float uu = u * u;
            float tt = t * t;

            Vector3 d = 3f * uu * (p1 - p0);
            d += 6f * u * t * (p2 - p1);
            d += 3f * tt * (p3 - p2);

            return d;
        }
    }
}
