using UnityEngine;

// Keeps a flat shadow directly beneath a target on the road surface — X/Z
// track the target, Y stays pinned to the road regardless of how high the
// target currently is (jumping, ducking, bouncing), so its lane position and
// jump timing are easy to judge at a glance. Used for the players, whose
// height changes at runtime — static obstacles just bake a child shadow in
// at build time instead, since their height never changes.
public class GroundShadow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float groundY = 0.02f;

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 pos = target.position;
        pos.y = groundY;
        transform.position = pos;
    }
}
