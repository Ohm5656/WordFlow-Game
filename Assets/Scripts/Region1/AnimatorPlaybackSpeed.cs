using UnityEngine;

/// <summary>
/// Forces this object's Animator to play at a fixed speed multiplier. Used to slow down the
/// "!" marker bob. Re-applies on enable so it sticks when the object is revealed (SetActive).
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class AnimatorPlaybackSpeed : MonoBehaviour
{
    [Tooltip("Animator speed multiplier (1 = normal, 0.5 = half speed).")]
    [SerializeField] private float speed = 2.0f;

    private void OnEnable()
    {
        var a = GetComponent<Animator>();
        if (a != null) a.speed = speed;
    }
}
