using UnityEngine;

namespace JbelIghoud.DayNight
{
    /// <summary>
    /// Cave Entrance Blend Trigger.
    /// Smoothly calculates player depth from the cave entrance into the interior tunnel
    /// and feeds the depth factor into the CaveLightingController.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CaveEntranceTrigger : MonoBehaviour
    {
        [Header("--- Transition Reference Points ---")]
        [Tooltip("Outside entrance boundary point in world coordinates.")]
        public Vector3 entrancePoint = new Vector3(126.88f, 18.0f, -140.0f);

        [Tooltip("Inside deep boundary point in world coordinates.")]
        public Vector3 interiorPoint = new Vector3(126.88f, 18.0f, -165.0f);

        [Header("--- Settings ---")]
        [Tooltip("Tag identifying the player object.")]
        public string playerTag = "Player";

        private Transform playerTransform;
        private CaveLightingController controller;

        private void Start()
        {
            controller = CaveLightingController.Instance;
            if (controller == null)
            {
                controller = FindAnyObjectByType<CaveLightingController>();
            }

            FindPlayer();
        }

        private void FindPlayer()
        {
            if (playerTransform != null) return;

            var playerGO = GameObject.FindWithTag(playerTag);
            if (playerGO != null)
            {
                playerTransform = playerGO.transform;
            }
            else
            {
                var controller = FindAnyObjectByType<TopDownPlayerController>();
                if (controller != null) playerTransform = controller.transform;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other))
            {
                playerTransform = other.transform;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!IsPlayer(other)) return;

            Vector3 playerPos = other.transform.position;
            float depth = CalculateDepth(playerPos);

            if (controller == null) controller = CaveLightingController.Instance;
            if (controller != null)
            {
                controller.SetPlayerCaveDepth(depth);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;

            // Determine if player exited towards outside or deeper into the cave
            Vector3 playerPos = other.transform.position;
            float distToExterior = Vector3.Distance(playerPos, entrancePoint);
            float distToInterior = Vector3.Distance(playerPos, interiorPoint);

            float exitDepth = distToInterior < distToExterior ? 1.0f : 0.0f;

            if (controller == null) controller = CaveLightingController.Instance;
            if (controller != null)
            {
                controller.SetPlayerCaveDepth(exitDepth);
            }
        }

        public float CalculateDepth(Vector3 currentPos)
        {
            Vector3 line = interiorPoint - entrancePoint;
            float lineLength = line.magnitude;
            if (lineLength <= 0.001f) return 0f;

            Vector3 dir = line / lineLength;
            Vector3 toPlayer = currentPos - entrancePoint;

            float projection = Vector3.Dot(toPlayer, dir);
            return Mathf.Clamp01(projection / lineLength);
        }

        private bool IsPlayer(Collider other)
        {
            if (other.CompareTag(playerTag)) return true;
            if (playerTransform != null && other.transform.IsChildOf(playerTransform)) return true;
            if (other.GetComponent<TopDownPlayerController>() != null || other.GetComponentInParent<TopDownPlayerController>() != null) return true;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(entrancePoint, 0.5f);
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(interiorPoint, 0.5f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(entrancePoint, interiorPoint);
        }
    }
}
