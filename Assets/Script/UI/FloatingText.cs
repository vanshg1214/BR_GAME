using System.Collections;
using TMPro;
using UnityEngine;

namespace WhackAMole.UI
{
    [RequireComponent(typeof(TextMeshPro))]
    public class FloatingText : MonoBehaviour
    {
        [SerializeField] private float floatSpeed = 1.0f;
        [SerializeField] private float lifetime = 1.5f;
        [SerializeField] private float curveStrength = 0.05f;
        
        private TextMeshPro textMesh;
        private Color originalColor;

        private void Awake()
        {
            textMesh = GetComponent<TextMeshPro>();
            originalColor = textMesh.color;
        }

        public void Initialize(string text)
        {
            textMesh.text = text;
            textMesh.color = originalColor;
            
            // Re-center horizontally
            textMesh.alignment = TextAlignmentOptions.Center;
            
            // Randomize starting rotation slightly for juice
            transform.Rotate(0, 0, Random.Range(-10f, 10f));

            StartCoroutine(FloatAndFadeRoutine());
        }

        private IEnumerator FloatAndFadeRoutine()
        {
            float elapsed = 0f;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = elapsed / lifetime;

                // Move upwards
                transform.position += Vector3.up * (floatSpeed * Time.deltaTime);

                // Fade out (using a smooth curve, mostly fading at the end)
                float alpha = 1f - Mathf.Pow(normalizedTime, 3f);
                textMesh.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

                // Ensure it always faces the camera
                if (Camera.main != null)
                {
                    transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
                }

                // Apply curve by offsetting vertices
                textMesh.ForceMeshUpdate(false);
                TMP_TextInfo textInfo = textMesh.textInfo;
                int characterCount = textInfo.characterCount;
                
                if (characterCount > 1 && curveStrength != 0f)
                {
                    for (int i = 0; i < characterCount; i++)
                    {
                        TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                        if (!charInfo.isVisible) continue;

                        float xPos = (float)i / (characterCount - 1);
                        // Sine wave from 0 to PI gives a nice peak in the middle
                        float yOffset = Mathf.Sin(xPos * Mathf.PI) * curveStrength;

                        int materialIndex = charInfo.materialReferenceIndex;
                        int vertexIndex = charInfo.vertexIndex;

                        Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
                        vertices[vertexIndex + 0].y += yOffset;
                        vertices[vertexIndex + 1].y += yOffset;
                        vertices[vertexIndex + 2].y += yOffset;
                        vertices[vertexIndex + 3].y += yOffset;
                    }
                    textMesh.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
                }

                yield return null;
            }

            // PERFORMANCE FIX: Deactivate instead of destroying so it can be pooled!
            gameObject.SetActive(false);
        }
    }
}
