using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FieldOfView : MonoBehaviour
{
    [Header("FOV Settings")]
    public float fovAngle = 90f;
    public float viewDistance = 5f;
    public int rayCount = 5;
    [SerializeField] private LayerMask obstacleMask; // Set to "Collision" in Inspector

    [Header("FOV Visual")]
    public bool showFOV = true;
    public Color fovColor = new Color(1f, 1f, 0f, 0.3f);

    private MeshRenderer meshRenderer;
    private Material mat;
    private Mesh mesh;
    private Transform playerTransform;

    void Start()
    {
        // Reset local transform relative to enemy parent
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
        meshRenderer.enabled = showFOV;
        if (showFOV)
        {
            DrawFOV();
        }
    }

    // 2 Raycasts: 1 for left edge, 1 for right edge
    void DrawTriangleFOV()
    {
        Vector3[] vertices = new Vector3[3];
        Vector2[] uv = new Vector2[3];
        int[] triangles = new int[3];

        vertices[0] = Vector3.zero; // Local origin
        uv[0] = new Vector2(0.5f, 0f);

        float halfAngle = fovAngle * 0.5f;

        // 1. Left boundary raycast
        Vector3 leftLocalDir = GetVectorFromAngle(halfAngle);
        Vector3 leftWorldDir = transform.TransformDirection(leftLocalDir).normalized;
        RaycastHit2D hitLeft = Physics2D.Raycast(transform.position, leftWorldDir, viewDistance, obstacleMask);
        vertices[1] = hitLeft.collider != null ? transform.InverseTransformPoint(hitLeft.point) : leftLocalDir * viewDistance;
        uv[1] = new Vector2(0f, 1f);

        // 2. Right boundary raycast
        Vector3 rightLocalDir = GetVectorFromAngle(-halfAngle);
        Vector3 rightWorldDir = transform.TransformDirection(rightLocalDir).normalized;
        RaycastHit2D hitRight = Physics2D.Raycast(transform.position, rightWorldDir, viewDistance, obstacleMask);
        vertices[2] = hitRight.collider != null ? transform.InverseTransformPoint(hitRight.point) : rightLocalDir * viewDistance;
        uv[2] = new Vector2(1f, 1f);

        // Connect triangle
        triangles[0] = 0;
        triangles[1] = 1;
        triangles[2] = 2;

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    // 1 Raycast: Math checks run first, raycast only fires if player is inside the cone
    public bool CanSeePlayer()
    {
        if (playerTransform == null) return false;

        Vector2 origin = transform.position;
        Vector2 target = playerTransform.position;
        Vector2 toPlayer = target - origin;

        // Check 1: Distance (sqrMagnitude avoids expensive sqrt)
        if (toPlayer.sqrMagnitude > viewDistance * viewDistance)
            return false;

        // Check 2: Angle within cone
        // GetVectorFromAngle uses Vector2.up as 0 deg, so transform.up represents enemy forward
        float angleToPlayer = Vector2.Angle(transform.up, toPlayer);
        if (angleToPlayer > fovAngle * 0.5f)
            return false;

        // Check 3: Single Linecast to verify no walls/obstacles block line-of-sight
        RaycastHit2D hit = Physics2D.Linecast(origin, target, obstacleMask);
        return hit.collider == null;
    }

    void DrawFOV()
    {
        //Debug.Log($"FOV parent: {transform.parent?.name}, FOV world pos: {transform.position}, FOV local pos: {transform.localPosition}");
        float angleIncrease = fovAngle / rayCount;
        float currentAngle = fovAngle / 2f;     // start at left edge of cone

        Vector3 worldOrigin = transform.position; // world space for raycast
        Vector3[] vertices = new Vector3[rayCount + 2];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[rayCount * 3];

        vertices[0] = Vector3.zero; // local origin

        for (int i = 0; i <= rayCount; i++)
        {
            Vector3 localDir = GetVectorFromAngle(currentAngle);

            // world space direction — accounts for parent rotation from AimController
            Vector3 worldDir = transform.TransformDirection(localDir).normalized;

            RaycastHit2D hit = Physics2D.Raycast(
                worldOrigin, worldDir, viewDistance,
                LayerMask.GetMask("Collision")
            );

            // hit → shorten ray to wall; no hit → full distance
            vertices[i + 1] = hit.collider != null
                ? transform.InverseTransformPoint(hit.point)  // world → local
                : localDir * viewDistance;                     // already local

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

    // Helper functions
    // Converts angle to local direction where 0 deg points up
    static Vector3 GetVectorFromAngle(float angle)
    {
        float rad = angle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), Mathf.Cos(rad), 0f);
    }
}


// Akira is gonna make this thing not lag our PC's until it blows up
/*
using UnityEngine;

public class FieldOfView : MonoBehaviour
{
    [Header("FOV Settings")]
    public float fovAngle = 90f;
    public float viewDistance = 5f;
    public int rayCount = 20;

    [Header("FOV Visual")]
    public bool showFOV;
    public Color fovColor = new Color(1f, 1f, 0f, 0.3f);

    MeshRenderer meshRenderer;
    Material mat;
    Mesh mesh;

    void Start()
    {
        // Reset local position
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        mesh = new Mesh { name = "FOV Mesh" };
        GetComponent<MeshFilter>().mesh = mesh;
        meshRenderer = GetComponent<MeshRenderer>();

        mat = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default"));
        mat.SetFloat("_Surface", 1f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
        mat.color = fovColor;
        meshRenderer.material = mat;

        // sorting layer from parent SpriteRenderer
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
        meshRenderer.enabled = showFOV;
        if (showFOV) DrawFOV();
    }

    void DrawFOV()
    {
        //Debug.Log($"FOV parent: {transform.parent?.name}, FOV world pos: {transform.position}, FOV local pos: {transform.localPosition}");
        float angleIncrease = fovAngle / rayCount;
        float currentAngle = fovAngle / 2f;     // start at left edge of cone

        Vector3 worldOrigin = transform.position; // world space for raycast
        Vector3[] vertices = new Vector3[rayCount + 2];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[rayCount * 3];

        vertices[0] = Vector3.zero; // local origin

        for (int i = 0; i <= rayCount; i++)
        {
            Vector3 localDir = GetVectorFromAngle(currentAngle);

            // world space direction — accounts for parent rotation from AimController
            Vector3 worldDir = transform.TransformDirection(localDir).normalized;

            RaycastHit2D hit = Physics2D.Raycast(
                worldOrigin, worldDir, viewDistance,
                LayerMask.GetMask("Collision")
            );

            // hit → shorten ray to wall; no hit → full distance
            vertices[i + 1] = hit.collider != null
                ? transform.InverseTransformPoint(hit.point)  // world → local
                : localDir * viewDistance;                     // already local

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

    // local-space direction from angle — Sin/Cos gives Vector2.up as 0 degrees
    static Vector3 GetVectorFromAngle(float angle)
    {
        float rad = angle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), Mathf.Cos(rad));
    }
}
*/