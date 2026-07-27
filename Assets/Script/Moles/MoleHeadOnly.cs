using UnityEngine;

namespace WhackAMole
{
    /// <summary>
    /// A clever trick to hide the body of a single-mesh rigged character.
    /// It shrinks the root bone to microscopic size, but inflates the Neck bone back to 1.0.
    /// Doing this in LateUpdate ensures animations don't override the squash!
    /// </summary>
    public class MoleHeadOnly : MonoBehaviour
    {
        [Tooltip("Drag the Root bone (e.g. Hips or Pelvis) here. This is the bone that controls the whole body.")]
        public Transform rootBone;

        [Tooltip("Drag the Neck bone here. The Head and Neck will stay full size!")]
        public Transform neckBone;

        [Tooltip("Because squashing the body pulls the head down to the floor, use this to lift the mesh back up to the hole.")]
        public float headYOffset = 0.5f;

        private void LateUpdate()
        {
            if (rootBone == null || neckBone == null) return;

            // 1. Squash the entire body into a microscopic dot
            rootBone.localScale = new Vector3(0.001f, 0.001f, 0.001f);

            // 2. Inflate the neck back to normal size (0.001 * 1000 = 1.0)
            neckBone.localScale = new Vector3(1000f, 1000f, 1000f);

            // 3. Lift the root up so the head sits exactly where the body's center used to be
            rootBone.localPosition = new Vector3(rootBone.localPosition.x, headYOffset, rootBone.localPosition.z);
        }
    }
}
