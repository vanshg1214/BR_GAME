using UnityEngine;

namespace Rehab.Volleyball.Mechanics
{
    /// <summary>
    /// Simple marker component to identify the volleyball net.
    /// Attach this to your net GameObject instead of using a tag.
    /// The VolleyballBall script detects it via GetComponent, which never throws errors.
    /// </summary>
    public class VolleyballNet : MonoBehaviour
    {
        // No logic needed - this is a pure marker/identifier component.
    }
}
