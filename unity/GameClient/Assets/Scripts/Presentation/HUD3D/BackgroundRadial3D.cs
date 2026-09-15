using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BackgroundRadial3D : MonoBehaviour
    {
        public Camera targetCamera;
        public float distance = 8f;
        public Color innerColor = new Color(0.690f, 0.769f, 0.871f, 1f); // Light Steel Blue (#B0C4DE)
        public Color outerColor = new Color(0.450f, 0.530f, 0.640f, 1f); // Deeper Steel Blue vignette
        public int segments = 32;

        private Mesh _mesh;
        private Camera _lastCamera;
        private float _lastDistance;
        private float _lastAspect;
        private Color _lastInner;
        private Color _lastOuter;

        private void Start()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            RebuildMesh();
            
            var renderer = GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            // Enable vertex color support in the Unlit shader if needed, but URP Unlit usually supports it.
            // Actually, standard URP Unlit might not multiply vertex color without a keyword or specific setup.
            // Let's use a standard Particles/Unlit or just URP/Unlit.
            mat.SetColor("_BaseColor", Color.white);
            renderer.sharedMaterial = mat;
        }

        private void Update()
        {
            if (targetCamera == null) return;
            if (_mesh == null || 
                targetCamera != _lastCamera || 
                distance != _lastDistance || 
                targetCamera.aspect != _lastAspect ||
                innerColor != _lastInner || 
                outerColor != _lastOuter)
            {
                RebuildMesh();
            }
        }

        private void RebuildMesh()
        {
            if (targetCamera == null) return;

            _lastCamera = targetCamera;
            _lastDistance = distance;
            _lastAspect = targetCamera.aspect;
            _lastInner = innerColor;
            _lastOuter = outerColor;

            float h = 2f * distance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float w = h * targetCamera.aspect;
            float radius = Mathf.Sqrt((w * w) / 4f + (h * h) / 4f) * 1.05f; // slightly larger to guarantee coverage

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "BackgroundRadial" };
                GetComponent<MeshFilter>().sharedMesh = _mesh;
            }

            int numVertices = segments + 2;
            var vertices = new Vector3[numVertices];
            var colors = new Color[numVertices];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            colors[0] = innerColor;

            float angleStep = 360f / segments;
            for (int i = 0; i < segments + 1; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                colors[i + 1] = outerColor;
            }

            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.colors = colors;
            _mesh.triangles = triangles;
            
            // Align to camera
            transform.position = targetCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance));
            transform.rotation = targetCamera.transform.rotation;
        }
    }
}
