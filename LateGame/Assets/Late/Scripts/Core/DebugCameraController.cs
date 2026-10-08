using UnityEngine;

namespace Late.Core
{
    /// <summary>
    /// Cámara libre para depurar: se mueve con WASD / flechas o sigue a un objetivo.
    /// Se usa en las escenas de prueba, no en el juego.
    /// </summary>
    public class DebugCameraController : MonoBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] private float moveSpeed = 10f;

        [Header("Seguimiento")]
        [SerializeField] private bool useFollow = false;
        [SerializeField] private Transform followTarget;
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 10f, -10f);
        [SerializeField] private float followSmooth = 10f;
        [SerializeField] private bool lookAtTarget = true;

        private void Update()
        {
            if (useFollow && followTarget != null)
            {
                HandleFollow();
            }
            else
            {
                HandleMovement();
            }
        }

        private void HandleMovement()
        {
            Vector3 moveDirection = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
            transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);
        }

        private void HandleFollow()
        {
            Vector3 desiredPos = followTarget.position + followOffset;
            transform.position = Vector3.Lerp(transform.position, desiredPos, 1f - Mathf.Exp(-followSmooth * Time.deltaTime));

            if (lookAtTarget)
            {
                transform.LookAt(followTarget.position);
            }
        }
    }
}
