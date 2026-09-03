using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FieldOfView : MonoBehaviour
{
    public float fovAngle = 90f;
    int rayCount = 20; // more = smoother arc
    float viewDistance = 5f;

    // moved to Start — can't reference instance fields at declaration
    float angleIncrease;
    float angle;

    [Header("FOV Visual")]
    public bool showFOV;
    public Color fovColor = new Color(1f, 1f, 0f, 0.3f);

    MeshRenderer meshRenderer;
    Material mat;
    Mesh mesh;

    void Start()
    {
        // calculate here instead of at field declaration
        angleIncrease = fovAngle / rayCount;
        angle = fovAngle / 2f; // start at left edge of cone so it's centered

        mesh = new Mesh
        {
            name = "FOV Mesh"
        };
        GetComponent<MeshFilter>().mesh = mesh;
        meshRenderer = GetComponent<MeshRenderer>();

        // Material in code
        mat = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default"));
        mat.SetFloat("_Surface", 1f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        // show or hide FOV based on bool
        mat.color = showFOV ? fovColor : new Color(0, 0, 0, 0f);
        meshRenderer.material = mat;

        // sorting and order layer based on parent
        SpriteRenderer parentSprite = transform.parent?.GetComponent<SpriteRenderer>();
        if (parentSprite != null)
        {
            meshRenderer.sortingLayerName = parentSprite.sortingLayerName;
            meshRenderer.sortingOrder = parentSprite.sortingOrder + 1;
        }
        else
        {
            // fallback if parent has no SpriteRenderer
            meshRenderer.sortingLayerName = "Default";
            meshRenderer.sortingOrder = 1;
        }

        // +1 for the origin, +1 for the last vertex to close the mesh
        Vector3 origin = Vector3.zero;
        Vector3[] vertices = new Vector3[rayCount + 1 + 1];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[rayCount * 3];

        vertices[0] = origin;

        for (int i = 0; i <= rayCount; i++)
        {
            // convert angle to direction vector scaled by view distance
            Vector3 vertex = origin + GetVectorFromAngle(angle) * viewDistance;

            vertices[i + 1] = vertex;

            if (i < rayCount)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            // step angle across the FOV range
            angle -= angleIncrease;
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    void Update()
    {
        // toggle visibility based on showFOV
        meshRenderer.enabled = showFOV;
    }

    public static Vector3 GetVectorFromAngle(float angle)
    {
        float angleRad = angle * (Mathf.PI / 180f);
        return new Vector3(Mathf.Sin(angleRad), Mathf.Cos(angleRad));
    }
}