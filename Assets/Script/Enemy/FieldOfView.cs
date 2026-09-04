using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FieldOfView : MonoBehaviour
{
    private EnemyVision enemyVision;

    [Header("FOV Visual")]
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] public bool showFOV = true;
    [SerializeField] public Color fovColor = new Color(1f, 1f, 0f, 0.3f);

    [Header("Debug")]
    [SerializeField] private int rayCount = 5;

    private MeshRenderer meshRenderer;
    private Material mat;
    private Mesh mesh;
    private Transform playerTransform;

    void Start()
    {
        // Get EnemyVision from parent (root Enemy object)
        enemyVision = GetComponentInParent<EnemyVision>();
        if (enemyVision == null)
            Debug.LogWarning($"[FieldOfView] No EnemyVision found on parent of {gameObject.name}");

        // Reset local transform
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        // Auto-fill obstacle mask if left default
        if (obstacleMask == 0)
        {
            obstacleMask = LayerMask.GetMask("Collision");
        }

        // Cache player reference
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        // Setup visual mesh
        mesh = new Mesh { name = "FOV Mesh" };
        GetComponent<MeshFilter>().mesh = mesh;
        meshRenderer = GetComponent<MeshRenderer>();

        // Material setup
        mat = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default"));
        mat.SetFloat("_Surface", 1f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
        mat.color = fovColor;
        meshRenderer.material = mat;

        // Sync sorting layer with parent sprite
        SpriteRenderer parentSprite = transform.parent?.GetComponent<SpriteRenderer>();
        if (parentSprite != null)
        {
            meshRenderer.sortingLayerName = parentSprite.sortingLayerName;
            meshRenderer.sortingOrder = parentSprite.sortingOrder + 1;
        }
        else
        {
            meshRenderer.sortingLayerName = "Default";
            meshRenderer.sortingOrder = 1;
        }
    }

    void Update()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        meshRenderer.enabled = showFOV;
        if (showFOV)
        {
            DrawFOV();
        }
    }

    void DrawFOV()
    {
        if (enemyVision == null) return;

        // READ values from EnemyVision instead of own fields
        float fovAngle = enemyVision.fovAngle;
        float viewDistance = enemyVision.viewDistance;

        float angleIncrease = fovAngle / rayCount;
        float currentAngle = fovAngle / 2f;

        Vector3 worldOrigin = transform.position;
        Vector3[] vertices = new Vector3[rayCount + 2];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[rayCount * 3];

        vertices[0] = Vector3.zero;

        for (int i = 0; i <= rayCount; i++)
        {
            Vector3 localDir = GetVectorFromAngle(currentAngle);
            Vector3 worldDir = transform.TransformDirection(localDir).normalized;

            RaycastHit2D hit = Physics2D.Raycast(
                worldOrigin, worldDir, viewDistance,
                obstacleMask
            );

            vertices[i + 1] = hit.collider != null
                ? transform.InverseTransformPoint(hit.point)
                : localDir * viewDistance;

            if (i < rayCount)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            currentAngle -= angleIncrease;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    public bool CanSeePlayer()
    {
        if (playerTransform == null || enemyVision == null) return false;

        Vector2 origin = transform.position;
        Vector2 target = playerTransform.position;
        Vector2 toPlayer = target - origin;

        if (toPlayer.sqrMagnitude > enemyVision.viewDistance * enemyVision.viewDistance)
            return false;

        float angleToPlayer = Vector2.Angle(transform.up, toPlayer);
        if (angleToPlayer > enemyVision.fovAngle * 0.5f)
            return false;

        RaycastHit2D hit = Physics2D.Linecast(origin, target, obstacleMask);
        return hit.collider == null;
    }

    static Vector3 GetVectorFromAngle(float angle)
    {
        float rad = angle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), Mathf.Cos(rad), 0f);
    }
}