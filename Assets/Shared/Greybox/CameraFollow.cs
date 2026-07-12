using UnityEngine;

namespace AIRGAP.Shared.Greybox
{
    /// <summary>Grey-box camera: smooth-follows the target in XY, keeps its own Z.</summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float smoothTime = 0.18f;

        private Vector3 _velocity;

        public void SetTarget(Transform followTarget) => target = followTarget;

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 goal = new Vector3(target.position.x, target.position.y, transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref _velocity, smoothTime);
        }
    }
}
