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
        Debug.Log($"FOV parent: {transform.parent?.name}, FOV world pos: {transform.position}, FOV local pos: {transform.localPosition}");
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