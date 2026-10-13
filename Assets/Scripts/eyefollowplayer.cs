using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EyeFollowPlayerNavMesh : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;

    [Tooltip("Optional. Put the visible eye mesh here if it is a child object.")]
    [SerializeField] private Transform eyeVisual;

    [Header("Follow Settings")]
    [SerializeField] private float followDistance = 2.5f;
    [SerializeField] private float repathRate = 0.15f;
    [SerializeField] private float minDestinationChange = 0.25f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float angularSpeed = 360f;

    [Header("Facing")]
    [SerializeField] private bool facePlayer = true;
    [SerializeField] private float faceSpeed = 10f;
    [SerializeField] private bool onlyRotateOnY = true;

    [Header("Hover Look")]
    [Tooltip("Only affects the visual mesh, not the NavMeshAgent.")]
    [SerializeField] private bool keepVisualHeight = true;

    [SerializeField] private float visualHeightOffset = 1.4f;

    private NavMeshAgent agent;
    private float nextRepathTime;
    private Vector3 lastDestination;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        agent.speed = moveSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = followDistance;

        agent.updateRotation = false;
    }

    private void Update()
    {
        if (player == null)
            return;

        UpdateMovement();
        UpdateFacing();
        UpdateVisualHeight();
    }

    private void UpdateMovement()
    {
        if (Time.time < nextRepathTime)
            return;

        nextRepathTime = Time.time + repathRate;

        Vector3 targetPosition = player.position;

        if (!NavMesh.SamplePosition(targetPosition, out NavMeshHit navHit, 2f, agent.areaMask))
            return;

        Vector3 navTarget = navHit.position;

        if (Vector3.Distance(lastDestination, navTarget) < minDestinationChange)
            return;

        lastDestination = navTarget;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= followDistance)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        else
        {
            agent.isStopped = false;
            agent.SetDestination(navTarget);
        }
    }

    private void UpdateFacing()
    {
        if (!facePlayer)
            return;

        Vector3 direction = player.position - transform.position;

        if (onlyRotateOnY)
            direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            1f - Mathf.Exp(-faceSpeed * Time.deltaTime)
        );
    }

    private void UpdateVisualHeight()
    {
        if (!keepVisualHeight || eyeVisual == null)
            return;

        Vector3 localPos = eyeVisual.localPosition;
        localPos.y = visualHeightOffset;
        eyeVisual.localPosition = localPos;
    }

    public void SetPlayer(Transform newPlayer)
    {
        player = newPlayer;
    }
}