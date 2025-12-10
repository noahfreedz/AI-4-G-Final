using UnityEngine;

public class BallSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private int numberOfBalls = 10;

    [Header("Spawn Around Player")]
    [SerializeField] private Transform player;
    [SerializeField] private float spawnRadius = 20f;
    [SerializeField] private bool spawnInFullCircle = true; 
    [SerializeField] private float frontArcAngle = 120f; 

    [Header("Height Range")]
    [SerializeField] private float minHeight = 10f;
    [SerializeField] private float maxHeight = 50f;
    [SerializeField] private bool heightRelativeToPlayer = true; 

    [Header("Options")]
    [SerializeField] private bool spawnOnStart = false;

    [Header("Spawn Key")]
    [SerializeField] private KeyCode spawnKey = KeyCode.Space;

    private Transform ballContainer;

    void Start()
    {
        ballContainer = new GameObject("Spawned Balls").transform;

        if (player == null)
        {
            player = Camera.main.transform;
            if (player != null)
            {
                Debug.Log("Auto-assigned Main Camera as player");
            }
            else
            {
                Debug.LogWarning("No player/camera assigned and couldn't find Main Camera!");
            }
        }

        if (spawnOnStart)
        {
            SpawnBalls();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(spawnKey))
        {
            SpawnBalls();
        }
    }

    [ContextMenu("Spawn Balls")]
    public void SpawnBalls()
    {
        if (ballPrefab == null)
        {
            Debug.LogError("Ball prefab is not assigned!");
            return;
        }

        if (player == null)
        {
            Debug.LogError("Player/Camera is not assigned!");
            return;
        }

        for (int i = 0; i < numberOfBalls; i++)
        {
            Vector3 randomPosition = GetRandomSpawnPositionAroundPlayer();
            GameObject ball = Instantiate(ballPrefab, randomPosition, Quaternion.identity, ballContainer);
            ball.name = $"Ball_{i + 1}";
        }

        Debug.Log($"Spawned {numberOfBalls} balls around player at {player.position}");
    }

    [ContextMenu("Clear All Balls")]
    public void ClearAllBalls()
    {
        if (ballContainer != null)
        {
            foreach (Transform child in ballContainer)
            {
                Destroy(child.gameObject);
            }
            Debug.Log("Cleared all spawned balls");
        }
    }

    private Vector3 GetRandomSpawnPositionAroundPlayer()
    {
        float randomDistance = Random.Range(0f, spawnRadius);

        float randomAngle;
        if (spawnInFullCircle)
        {
            randomAngle = Random.Range(0f, 360f);
        }
        else
        {
            float playerYaw = player.eulerAngles.y;
            float halfArc = frontArcAngle / 2f;
            randomAngle = playerYaw + Random.Range(-halfArc, halfArc);
        }
        float angleRad = randomAngle * Mathf.Deg2Rad;

        float offsetX = Mathf.Sin(angleRad) * randomDistance;
        float offsetZ = Mathf.Cos(angleRad) * randomDistance;

        float spawnHeight;
        if (heightRelativeToPlayer)
        {
            spawnHeight = player.position.y + Random.Range(minHeight, maxHeight);
        }
        else
        {
            spawnHeight = Random.Range(minHeight, maxHeight);
        }

        Vector3 spawnPosition = new Vector3(
            player.position.x + offsetX,
            spawnHeight,
            player.position.z + offsetZ
        );

        return spawnPosition;
    }

    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        Gizmos.color = Color.cyan;
        DrawCircle(player.position, spawnRadius, 64);

        if (!spawnInFullCircle)
        {
            Gizmos.color = Color.yellow;
            DrawArc(player.position, player.forward, spawnRadius, frontArcAngle, 32);
        }

        float baseHeight = heightRelativeToPlayer ? player.position.y : 0f;

        Gizmos.color = Color.green;
        DrawCircle(player.position + Vector3.up * (baseHeight + minHeight), spawnRadius, 32);

        Gizmos.color = Color.red;
        DrawCircle(player.position + Vector3.up * (baseHeight + maxHeight), spawnRadius, 32);

        Gizmos.color = Color.white;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            float x = Mathf.Sin(angle) * spawnRadius;
            float z = Mathf.Cos(angle) * spawnRadius;
            Vector3 basePos = player.position + new Vector3(x, 0, z);
            Gizmos.DrawLine(
                basePos + Vector3.up * (baseHeight + minHeight),
                basePos + Vector3.up * (baseHeight + maxHeight)
            );
        }
    }

    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(0, 0, radius);

        for (int i = 1; i <= segments; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Sin(angle) * radius, 0, Mathf.Cos(angle) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }

    private void DrawArc(Vector3 center, Vector3 forward, float radius, float arcAngle, int segments)
    {
        float baseAngle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        float startAngle = baseAngle - arcAngle / 2f;
        float angleStep = arcAngle / segments;

        Vector3 prevPoint = center + new Vector3(
            Mathf.Sin(startAngle * Mathf.Deg2Rad) * radius,
            0,
            Mathf.Cos(startAngle * Mathf.Deg2Rad) * radius
        );

        for (int i = 1; i <= segments; i++)
        {
            float angle = (startAngle + angleStep * i) * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Sin(angle) * radius, 0, Mathf.Cos(angle) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }

        Gizmos.DrawLine(center, center + new Vector3(
            Mathf.Sin(startAngle * Mathf.Deg2Rad) * radius,
            0,
            Mathf.Cos(startAngle * Mathf.Deg2Rad) * radius
        ));
        Gizmos.DrawLine(center, center + new Vector3(
            Mathf.Sin((startAngle + arcAngle) * Mathf.Deg2Rad) * radius,
            0,
            Mathf.Cos((startAngle + arcAngle) * Mathf.Deg2Rad) * radius
        ));
    }
}