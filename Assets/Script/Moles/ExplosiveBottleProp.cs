using UnityEngine;
using System.Collections;
using ExpObj;

namespace WhackAMole
{
    public class ExplosiveBottleProp : MonoBehaviour, IHittable
    {
        [Header("Mesh Setup")]
        public GameObject solidMesh;
        public GameObject brokenMeshParent;

        [Header("Explosion Settings")]
        public float explosionForce = 300f;
        public float explosionRadius = 1.5f;
        public float upwardModifier = 1.0f;
        public float minHitVelocity = 1.5f;

        [Header("Performance Cleanup")]
        public float timeBeforeCleanup = 0.5f;
        public float shrinkDuration = 0.5f;

        [Header("VFX & SFX")]
        public GameObject hitParticlesPrefab;
        public AudioClip shatterSound;

        [Header("Ball Logic")]
        public GameObject ballObject;
        public bool debugForceShowBallInHands = false;

        [Header("Tutorial UI")]
        public GameObject tutorialArrow;

        [Header("ExplosiveObject Compatibility")]
        public GameObject brokenBottlePrefab;
        public GameObject soundFX;
        public AudioClip[] explosionClips;
        public float pitchVariationRange = 0.1f;
        public float destroySoundEffectAfter = 5f;
        public GameObject explosionVFX;
        public float destroyVisualEffectAfter = 7f;

        public event System.Action OnTargetDestroyed;

        public bool IsBroken => isBroken;

        private bool isBroken = false;
        private bool isThrown = false;
        private Rigidbody[] brokenPiecesRigidbodies;
        private TransformData[] initialPieceTransforms;
        private Vector3 initialParentScale;

        private Transform initialHandParent;
        private Vector3 initialLocalPos;
        private Quaternion initialLocalRot;
        private Vector3 initialBottleScale;

        private Transform initialballParent;
        private Vector3 initialballLocalPos;
        private Vector3 initialballScale;
        private bool ballPickedUp = false;
        private Transform leftHandBoneRef;
        private Transform rightHandBoneRef;

        private struct TransformData
        {
            public Vector3 localPosition;
            public Quaternion localRotation;
        }

        private void Awake()
        {
            timeBeforeCleanup = 0.5f;
            shrinkDuration = 0.5f;

            initialHandParent = transform.parent;
            initialLocalPos = transform.localPosition;
            initialLocalRot = transform.localRotation;
            initialBottleScale = transform.localScale;

            if (ballObject == null)
            {
                Transform[] children = GetComponentsInChildren<Transform>(true);
                foreach (Transform t in children)
                {
                    if (t.name.ToLower().Contains("ball") || t.name.ToLower().Contains("sphere"))
                    {
                        ballObject = t.gameObject;
                        break;
                    }
                }
            }

            if (brokenMeshParent != null && solidMesh != null && brokenMeshParent.transform.IsChildOf(solidMesh.transform))
            {
                brokenMeshParent.transform.SetParent(solidMesh.transform.parent, true);
            }
            if (ballObject != null && solidMesh != null && ballObject.transform.IsChildOf(solidMesh.transform))
            {
                ballObject.transform.SetParent(solidMesh.transform.parent, true);
            }

            Rigidbody rootRb = GetComponent<Rigidbody>();
            if (rootRb != null)
            {
                rootRb.isKinematic = true;
                rootRb.useGravity = false;
            }

            if (ballObject != null) 
            {
                initialballParent = ballObject.transform.parent;
                initialballLocalPos = ballObject.transform.localPosition;
                initialballScale = ballObject.transform.localScale;
                
                ballObject.SetActive(false);
            }

            if (brokenMeshParent != null)
            {
                if (brokenMeshParent.isStatic)
                {
                    brokenMeshParent.isStatic = false;
                }

                initialParentScale = brokenMeshParent.transform.localScale;
                
                MeshFilter[] filters = brokenMeshParent.GetComponentsInChildren<MeshFilter>(true);
                
                System.Collections.Generic.List<Rigidbody> rbs = new System.Collections.Generic.List<Rigidbody>();
                
                foreach (MeshFilter f in filters)
                {
                    if (f.gameObject.isStatic)
                    {
                        f.gameObject.isStatic = false; 
                    }

                    Rigidbody rb = f.GetComponent<Rigidbody>();
                    if (rb == null) rb = f.gameObject.AddComponent<Rigidbody>();
                    
                    Collider col = f.GetComponent<Collider>();
                    
                    if (col != null && col is MeshCollider) 
                    { 
                        DestroyImmediate(col); 
                        col = null; 
                    }
                    
                    if (col == null && f.GetComponent<BoxCollider>() == null)
                    {
                        col = f.gameObject.AddComponent<BoxCollider>();
                    }
                    
                    rb.isKinematic = true;
                    f.gameObject.AddComponent<DeactivateIfBelowTable>();
                    rbs.Add(rb);
                }
                
                brokenPiecesRigidbodies = rbs.ToArray();
                
                initialPieceTransforms = new TransformData[brokenPiecesRigidbodies.Length];
                for (int i = 0; i < brokenPiecesRigidbodies.Length; i++)
                {
                    Rigidbody rb = brokenPiecesRigidbodies[i];
                    initialPieceTransforms[i] = new TransformData
                    {
                        localPosition = rb.transform.localPosition,
                        localRotation = rb.transform.localRotation
                    };
                }

                brokenMeshParent.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (ballObject != null && !ballPickedUp)
            {
                ballObject.transform.SetParent(initialballParent);
                ballObject.transform.localPosition = initialballLocalPos;
            }
        }

        public void OnHit(Vector3 velocity, Vector3 hitPosition)
        {
            if (isBroken) return;

            Vector3 horizontalVelocity = new Vector3(velocity.x, 0, velocity.z);
            float horizontalSpeed = horizontalVelocity.magnitude;
            float verticalSpeed = Mathf.Abs(velocity.y);

            if (verticalSpeed >= 1.5f) return;

            float requiredSpeed = Mathf.Max(0.15f, minHitVelocity);
            if (horizontalSpeed < requiredSpeed) return;

            float requiredHorizontalRatio = 1.5f; 
            if (horizontalSpeed < verticalSpeed * requiredHorizontalRatio) return;

            isBroken = true;

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(20, velocity.magnitude); 
            }

            Shatter(hitPosition);
        }

        private void Shatter(Vector3 hitPosition)
        {
            if (tutorialArrow != null) tutorialArrow.SetActive(false);

            if (solidMesh != null) 
            {
                if (solidMesh == gameObject)
                {
                    Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer r in renderers)
                    {
                        if (ballObject != null && r.transform.IsChildOf(ballObject.transform)) continue;
                        if (brokenMeshParent != null && r.transform.IsChildOf(brokenMeshParent.transform)) continue;
                        r.enabled = false;
                    }
                    Collider[] colliders = GetComponentsInChildren<Collider>(true);
                    foreach (Collider c in colliders)
                    {
                        if (ballObject != null && c.transform.IsChildOf(ballObject.transform)) continue;
                        if (brokenMeshParent != null && c.transform.IsChildOf(brokenMeshParent.transform)) continue;
                        c.enabled = false;
                    }
                }
                else
                {
                    solidMesh.SetActive(false);
                }
            }

            AudioClip clipToPlay = null;
            if (explosionClips != null && explosionClips.Length > 0)
            {
                clipToPlay = explosionClips[Random.Range(0, explosionClips.Length)];
            }
            else if (shatterSound != null)
            {
                clipToPlay = shatterSound;
            }

            if (clipToPlay != null)
            {
                AudioSource.PlayClipAtPoint(clipToPlay, hitPosition);
            }

            if (soundFX != null)
            {
                GameObject newSFX = ObjectPooler.Instance.SpawnOrAddPool(soundFX.name, soundFX, 5, transform.position, Quaternion.identity);
                AudioSource audioS = newSFX.GetComponent<AudioSource>();
                if (audioS != null)
                {
                    if (clipToPlay != null) audioS.clip = clipToPlay;
                    audioS.pitch = 1f + Random.Range(-pitchVariationRange, pitchVariationRange);
                    audioS.Play();
                }
                ObjectPooler.Instance.ReturnToPool(newSFX, destroySoundEffectAfter);
            }

            if (brokenBottlePrefab != null)
            {
                GameObject brokenBottle = ObjectPooler.Instance.SpawnOrAddPool(brokenBottlePrefab.name, brokenBottlePrefab, 5, transform.position, transform.rotation);
                
                foreach (Transform child in brokenBottle.transform)
                {
                    child.gameObject.SetActive(true);
                    if (child.gameObject.GetComponent<DeactivateIfBelowTable>() == null && (child.gameObject.GetComponent<Collider>() != null || child.gameObject.GetComponent<Rigidbody>() != null))
                    {
                        child.gameObject.AddComponent<DeactivateIfBelowTable>();
                    }
                }

                brokenBottle.transform.localScale = transform.lossyScale;

                CollisionIsolator.IsolateObject(brokenBottle);

                BrokenObject brokenObj = brokenBottle.GetComponent<BrokenObject>();
                if (brokenObj != null)
                {
                    brokenObj.RandomVelocities();
                }
                else
                {
                    ObjectPooler.Instance.ReturnToPool(brokenBottle, timeBeforeCleanup + shrinkDuration);
                }
            }
            else if (brokenMeshParent != null) 
            {
                if (solidMesh != null)
                {
                    brokenMeshParent.transform.localPosition = solidMesh.transform.localPosition;
                    brokenMeshParent.transform.localRotation = solidMesh.transform.localRotation;
                }
                else
                {
                    brokenMeshParent.transform.localScale = initialParentScale;
                }
                
                brokenMeshParent.SetActive(true);
                
                CollisionIsolator.IsolateRigidbodies(brokenPiecesRigidbodies);

                if (brokenPiecesRigidbodies != null && brokenPiecesRigidbodies.Length > 0)
                {
                    foreach (Rigidbody rb in brokenPiecesRigidbodies)
                    {
                        if (rb != null)
                        {
                            rb.isKinematic = false;
                            rb.useGravity = true;
                            Vector3 outwardDir = (rb.transform.position - hitPosition).normalized;
                            if (outwardDir == Vector3.zero) outwardDir = Vector3.up;
                            rb.linearVelocity = outwardDir * 4f + Vector3.up * 2f;
                            rb.angularVelocity = new Vector3(Random.Range(-15f, 15f), Random.Range(-15f, 15f), Random.Range(-15f, 15f));
                        }
                    }
                }
                StartCoroutine(CleanupRoutine());
            }

            if (ballObject != null) 
            {
                ballObject.transform.SetParent(null, true);
                ballObject.transform.localScale = initialballScale; 
                ballObject.SetActive(true);
                Renderer[] rends = ballObject.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in rends)
                {
                    if (r != null) r.enabled = true;
                }
            }

            if (OnTargetDestroyed != null)
            {
                OnTargetDestroyed.Invoke();
            }
        }

        private IEnumerator CleanupRoutine()
        {
            yield return new WaitForSeconds(timeBeforeCleanup);

            if (brokenMeshParent == null) yield break;

            Vector3 initialScale = brokenMeshParent.transform.localScale;
            float elapsed = 0f;

            while (elapsed < shrinkDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shrinkDuration;
                brokenMeshParent.transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, t);
                yield return null;
            }

            brokenMeshParent.SetActive(false);
        }

        public void AttachToInitialHand()
        {
            if (initialHandParent != null)
            {
                transform.SetParent(initialHandParent);
                transform.localPosition = initialLocalPos + new Vector3(0f, 0.05f, 0f);
                transform.localRotation = initialLocalRot;
                transform.localScale = initialBottleScale * 0.8f;
                
                if (tutorialArrow != null) tutorialArrow.SetActive(false);
            }
        }

        public void ResetProp()
        {
            isBroken = false;
            isThrown = false;
            gameObject.SetActive(true);

            Collider mainCol = GetComponent<Collider>();
            if (mainCol != null) mainCol.enabled = true;

            if (initialHandParent != null)
            {
                transform.SetParent(initialHandParent);
                transform.localPosition = initialLocalPos;
                transform.localRotation = initialLocalRot;
                transform.localScale = initialBottleScale; 
                
                if (tutorialArrow != null) tutorialArrow.SetActive(false);
            }

            if (solidMesh != null) 
            {
                if (solidMesh == gameObject)
                {
                    Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer r in renderers)
                    {
                        if (ballObject != null && r.transform.IsChildOf(ballObject.transform)) continue;
                        if (brokenMeshParent != null && r.transform.IsChildOf(brokenMeshParent.transform)) continue;
                        r.enabled = true;
                    }
                    Collider[] colliders = GetComponentsInChildren<Collider>(true);
                    foreach (Collider c in colliders)
                    {
                        if (ballObject != null && c.transform.IsChildOf(ballObject.transform)) continue;
                        if (brokenMeshParent != null && c.transform.IsChildOf(brokenMeshParent.transform)) continue;
                        c.enabled = true;
                    }
                }
                else
                {
                    solidMesh.SetActive(true);
                    Collider[] childCols = solidMesh.GetComponentsInChildren<Collider>(true);
                    foreach (Collider c in childCols)
                    {
                        c.enabled = true;
                    }
                }
            }
            
            if (ballObject != null) 
            {
                ballObject.transform.SetParent(null, false);
                ballObject.SetActive(false);
                ballObject.transform.SetParent(initialballParent);
                ballObject.transform.localPosition = initialballLocalPos;
                ballPickedUp = false;
                leftHandBoneRef = null;
                rightHandBoneRef = null;
            }
            
            if (brokenMeshParent != null)
            {
                brokenMeshParent.SetActive(false);
                brokenMeshParent.transform.localScale = initialParentScale;

                if (brokenPiecesRigidbodies != null)
                {
                    for (int i = 0; i < brokenPiecesRigidbodies.Length; i++)
                    {
                        Rigidbody rb = brokenPiecesRigidbodies[i];
                        if (rb != null)
                        {
                            if (!rb.isKinematic)
                            {
                                rb.linearVelocity = Vector3.zero;
                                rb.angularVelocity = Vector3.zero;
                                rb.isKinematic = true; 
                            }
                            rb.transform.localPosition = initialPieceTransforms[i].localPosition;
                            rb.transform.localRotation = initialPieceTransforms[i].localRotation;
                            
                            rb.gameObject.SetActive(true);
                        }
                    }
                }
            }
        }

        public void ThrowForward()
        {
            if (isThrown) return;
            isThrown = true;
            StartCoroutine(ThrowRoutine());
        }

        private IEnumerator ThrowRoutine()
        {
            DogCharacter dog = GetComponentInParent<DogCharacter>();
            float tableWorldY = transform.position.y; 
            if (dog != null)
            {
                tableWorldY = dog.transform.position.y;
            }

            transform.SetParent(null);
            
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation; 
            
            Vector3 throwDirection = transform.forward;
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Vector3 toCam = mainCam.transform.position - startPos;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 0.001f)
                {
                    throwDirection = toCam.normalized;
                }
            }

            Quaternion targetRot = Quaternion.LookRotation(throwDirection);

            float throwDist = 0.1f;
            Vector3 targetPos = startPos + (throwDirection * throwDist);
            targetPos.y = tableWorldY; 

            if (mainCam != null)
            {
                Vector3 headPos = mainCam.transform.position;
                if (Vector3.Distance(targetPos, headPos) < 0.25f)
                {
                    throwDist = 0.05f;
                    targetPos = startPos + (throwDirection * throwDist);
                    targetPos.y = tableWorldY;

                    if (Vector3.Distance(targetPos, headPos) < 0.25f)
                    {
                        throwDist = 0f;
                        targetPos = startPos;
                        targetPos.y = tableWorldY;
                    }
                }
            }

            Vector3 startScale = transform.localScale;
            Vector3 targetScale = startScale * 1.1f; 

            float duration = 0.4f; 
            float elapsed = 0f;
            float arcHeight = 0.2f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
                currentPos.y += Mathf.Sin(t * Mathf.PI) * arcHeight; 

                transform.position = currentPos;
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                
                yield return null;
            }

            transform.position = targetPos;
            transform.localScale = targetScale;
            transform.rotation = targetRot;
            if (tutorialArrow != null)
            {
                tutorialArrow.SetActive(true);
            }
        }

        public void MoveBallToTarget(Vector3 targetPos, float duration, bool useArc = true)
        {
            if (tutorialArrow != null) tutorialArrow.SetActive(false);
            if (ballObject != null && ballObject.activeSelf)
            {
                StartCoroutine(MoveRoutine(ballObject.transform, targetPos, duration, useArc));
            }
        }

        public void MoveBottleToTarget(Vector3 targetPos, float duration, bool useArc = true)
        {
            if (tutorialArrow != null) tutorialArrow.SetActive(false);
            if (isThrown && !isBroken)
            {
                StartCoroutine(MoveRoutine(transform, targetPos, duration, useArc));
            }
        }

        private IEnumerator MoveRoutine(Transform objToMove, Vector3 targetPos, float duration, bool useArc)
        {
            objToMove.SetParent(null);
            Vector3 startPos = objToMove.position;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
                if (useArc) 
                {
                    currentPos.y += Mathf.Sin(t * Mathf.PI) * 0.1f;
                }
                
                objToMove.position = currentPos;
                yield return null;
            }
            objToMove.position = targetPos;
        }

        public void AnimateBallToHands(Transform leftHand, Transform rightHand)
        {
            if (tutorialArrow != null) tutorialArrow.SetActive(false);
            if (ballObject == null) return;

            ballPickedUp = true;
            leftHandBoneRef = leftHand;
            rightHandBoneRef = rightHand;
            ballObject.SetActive(true);

            ballObject.transform.SetParent(null, false);

            if (leftHand == null) return;

            ballObject.transform.localScale = initialballScale;

            if (rightHand != null)
            {
                ballObject.transform.position = (leftHand.position + rightHand.position) / 2f;
            }
            else
            {
                ballObject.transform.position = leftHand.position;
            }
        }

        private void LateUpdate()
        {
            if (ballPickedUp && ballObject != null)
            {
                if (leftHandBoneRef != null && rightHandBoneRef != null)
                {
                    Vector3 midpointWorld = (leftHandBoneRef.position + rightHandBoneRef.position) / 2f;

                    Transform dogRoot = leftHandBoneRef.GetComponentInParent<DogCharacter>()?.transform;
                    Vector3 forward = dogRoot != null ? dogRoot.forward : Vector3.forward;

                    midpointWorld += Vector3.down * 0.05f;      
                    midpointWorld += forward * 0.04f;            

                    ballObject.transform.position = midpointWorld;
                }
                else if (leftHandBoneRef != null)
                {
                    ballObject.transform.position = leftHandBoneRef.position;
                }
            }
        }

        public void AnimateBottleToHands(Transform leftHand, Transform rightHand)
        {
            if (tutorialArrow != null) tutorialArrow.SetActive(false);
            if (isThrown && !isBroken)
            {
                StartCoroutine(FollowHandsRoutine(transform, leftHand, rightHand, initialBottleScale));
            }
        }

        private IEnumerator FollowHandsRoutine(Transform objToMove, Transform leftHand, Transform rightHand, Vector3? targetScale = null)
        {
            Vector3 startWorldPos = objToMove.position;
            Quaternion targetWorldRot = objToMove.rotation;

            Transform dogRoot = leftHand.GetComponentInParent<BaseMole>()?.transform;
            if (dogRoot != null)
            {
                objToMove.SetParent(dogRoot, true);
            }
            else
            {
                objToMove.SetParent(leftHand, true); 
            }
            
            Vector3 startScale = objToMove.localScale;       
            Vector3 finalScale = targetScale ?? startScale;

            float scaleDuration = 0.2f;
            float elapsed = 0f;

            while (objToMove != null && objToMove.gameObject.activeInHierarchy)
            {
                Vector3 middleHandPos = (leftHand.position + rightHand.position) / 2f;

                if (elapsed < scaleDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / scaleDuration;
                    
                    objToMove.position = Vector3.Lerp(startWorldPos, middleHandPos, t);
                    if (targetScale.HasValue) objToMove.localScale = Vector3.Lerp(startScale, finalScale, t);
                }
                else
                {
                    objToMove.position = middleHandPos;
                    if (targetScale.HasValue) objToMove.localScale = finalScale;
                }

                objToMove.rotation = targetWorldRot;
                
                yield return null;
            }
        }

        public void DisableHittable()
        {
            Collider mainCol = GetComponent<Collider>();
            if (mainCol != null) mainCol.enabled = false;

            if (solidMesh != null)
            {
                Collider[] childCols = solidMesh.GetComponentsInChildren<Collider>(true);
                foreach (Collider c in childCols)
                {
                    c.enabled = false;
                }
            }
        }

        public void AnimateBottleBackNoPickup(Transform leftHand, Transform rightHand, float duration)
        {
            if (tutorialArrow != null) tutorialArrow.SetActive(false);
            if (isThrown && !isBroken)
            {
                StartCoroutine(ScaleBottleDownToHandsRoutine(leftHand, rightHand, duration));
            }
        }

        private IEnumerator ScaleBottleDownToHandsRoutine(Transform leftHand, Transform rightHand, float duration)
        {
            Vector3 startWorldPos = transform.position;
            Quaternion targetWorldRot = transform.rotation; 
            Vector3 startScale = transform.localScale;
            Vector3 targetScale = Vector3.zero;

            float elapsed = 0f;
            while (elapsed < duration && transform != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                Vector3 middleHandPos = (leftHand.position + rightHand.position) / 2f;
                transform.position = Vector3.Lerp(startWorldPos, middleHandPos, t);
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                transform.rotation = targetWorldRot;

                yield return null;
            }

            if (transform != null)
            {
                transform.localScale = targetScale;
                gameObject.SetActive(false);
            }
        }

        private Vector3 ClampToTableROM(Vector3 pos)
        {
            if (WorkspaceAutoPositioner.Instance == null) return pos;

            Transform tableTrans = WorkspaceAutoPositioner.Instance.transform;
            Vector3 localPos = tableTrans.InverseTransformPoint(pos);

            float maxReach = 0.6f;
            float minRadius = 0.1f;
            float sweepLeft = 60f;
            float sweepRight = 60f;

            if (GameManager.Instance != null && GameManager.Instance.RehabProfile != null)
            {
                RehabProfileSO profile = GameManager.Instance.RehabProfile;
                maxReach = profile.armLength * Mathf.Clamp01(profile.maxFlexion / 90f);
                minRadius = Mathf.Max(0.1f, maxReach * 0.35f);
                sweepLeft = profile.shoulderHorizontalAdductionMax;
                sweepRight = profile.shoulderHorizontalAdductionMax;
            }

            float x = localPos.x;
            float z = localPos.z;

            float radius = Mathf.Sqrt(x * x + z * z);
            float angleRad = Mathf.Atan2(z, x);
            float angleDeg = angleRad * Mathf.Rad2Deg;

            float relativeAngle = angleDeg - 90f;

            relativeAngle = Mathf.Clamp(relativeAngle, -sweepRight, sweepLeft);
            radius = Mathf.Clamp(radius, minRadius, maxReach);

            float finalAngleRad = (relativeAngle + 90f) * Mathf.Deg2Rad;
            localPos.x = Mathf.Cos(finalAngleRad) * radius;
            localPos.z = Mathf.Sin(finalAngleRad) * radius;

            return tableTrans.TransformPoint(localPos);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (gameObject.scene.name == null) return;
            if (ballObject == null) return;

            if (initialballParent == null)
            {
                initialballParent = ballObject.transform.parent;
                initialballLocalPos = ballObject.transform.localPosition;
            }

            if (debugForceShowBallInHands)
            {
                DogCharacter dogChar = GetComponentInParent<DogCharacter>();
                if (dogChar != null && dogChar.leftHandBone != null)
                {
                    ballObject.SetActive(true);
                    
                    Vector3 midpointWorld = dogChar.leftHandBone.position;
                    if (dogChar.rightHandBone != null)
                    {
                        midpointWorld = (dogChar.leftHandBone.position + dogChar.rightHandBone.position) / 2f;
                    }
                    ballObject.transform.position = midpointWorld;
                }
            }
            else
            {
                if (initialballParent != null)
                {
                    ballObject.transform.SetParent(initialballParent, true);
                    ballObject.transform.localPosition = initialballLocalPos;
                }
                ballObject.SetActive(false);
            }
        }
#endif
    }
}
