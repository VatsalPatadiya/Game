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
        public Material backgroundMaterial;
        public int segments = 32;

        private static Texture2D _cachedTexture;
        private static Material _cachedMaterial;

        private Mesh _mesh;
        private Camera _lastCamera;
        private float _lastDistance;
        private float _lastAspect;
        private float _lastOrthoSize;
        private Color _lastInner;
        private Color _lastOuter;

        private void Awake()
        {
            if (_cachedTexture == null)
            {
                _cachedTexture = Resources.Load<Texture2D>("PremiumBackground");
            }
        }

        private void Start()
        {
            if (targetCamera == null) targetCamera = Camera.main;

            if (_cachedTexture == null)
            {
                _cachedTexture = Resources.Load<Texture2D>("PremiumBackground");
            }

            var renderer = GetComponent<MeshRenderer>();
            if (backgroundMaterial != null)
            {
                renderer.sharedMaterial = backgroundMaterial;
            }
            else if (_cachedTexture != null && (gameObject.name == "FeltBackground" || distance >= 10f))
            {
                if (_cachedMaterial == null)
                {
                    _cachedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    _cachedMaterial.SetTexture("_BaseMap", _cachedTexture);
                    _cachedMaterial.SetColor("_BaseColor", Color.white);
                    _cachedMaterial.SetFloat("_Cull", 0f);
                }
                renderer.sharedMaterial = _cachedMaterial;
            }
            else if (renderer.sharedMaterial == null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                mat.SetColor("_BaseColor", Color.white);
                renderer.sharedMaterial = mat;
            }

            Debug.Log($"[BackgroundRadial3D] Start on {gameObject.name}: tex={(_cachedTexture != null ? _cachedTexture.name : "null")}, mat={(renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "null")}");
            RebuildMesh();
        }

        private void Update()
        {
            if (targetCamera == null) return;
            if (_mesh == null || 
                targetCamera != _lastCamera || 
                distance != _lastDistance || 
                targetCamera.aspect != _lastAspect ||
                (targetCamera.orthographic && targetCamera.orthographicSize != _lastOrthoSize) ||
                innerColor != _lastInner || 
                outerColor != _lastOuter)
            {
                RebuildMesh();
            }
        }

        private void LateUpdate()
        {
            if (targetCamera == null) return;
            float actualDistance = targetCamera.orthographic ? Mathf.Max(distance, 75f) : distance;
            transform.position = targetCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, actualDistance));
            transform.rotation = targetCamera.transform.rotation;
        }

        private void RebuildMesh()
        {
            if (targetCamera == null) return;

            _lastCamera = targetCamera;
            _lastDistance = distance;
            _lastAspect = targetCamera.aspect;
            _lastOrthoSize = targetCamera.orthographicSize;
            _lastInner = innerColor;
            _lastOuter = outerColor;

            float h, w;
            if (targetCamera.orthographic)
            {
                h = 2f * targetCamera.orthographicSize;
                w = h * targetCamera.aspect;
            }
            else
            {
                h = 2f * distance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                w = h * targetCamera.aspect;
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "BackgroundRadial" };
                GetComponent<MeshFilter>().sharedMesh = _mesh;
            }

            if (_cachedTexture == null)
            {
                _cachedTexture = Resources.Load<Texture2D>("PremiumBackground");
            }

            var renderer = GetComponent<MeshRenderer>();
            bool isTextured = (backgroundMaterial != null)
                || (_cachedTexture != null && (gameObject.name == "FeltBackground" || distance >= 10f))
                || (renderer.sharedMaterial != null && (renderer.sharedMaterial.mainTexture != null || renderer.sharedMaterial.GetTexture("_BaseMap") != null));

            if (isTextured && (renderer.sharedMaterial == null || renderer.sharedMaterial.shader.name.Contains("Particles")))
            {
                if (backgroundMaterial != null)
                {
                    renderer.sharedMaterial = backgroundMaterial;
                }
                else if (_cachedTexture != null)
                {
                    if (_cachedMaterial == null)
                    {
                        _cachedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                        _cachedMaterial.SetTexture("_BaseMap", _cachedTexture);
                        _cachedMaterial.SetColor("_BaseColor", Color.white);
                        _cachedMaterial.SetFloat("_Cull", 0f);
                    }
                    renderer.sharedMaterial = _cachedMaterial;
                }
            }

            if (isTextured)
            {
                // Screen-filling quad with full UV mapping (double-sided triangles so it can never be culled)
                float halfW = w * 0.5f;
                float halfH = h * 0.5f;
                var quadVertices = new Vector3[4]
                {
                    new Vector3(-halfW, -halfH, 0f),
                    new Vector3( halfW, -halfH, 0f),
                    new Vector3( halfW,  halfH, 0f),
                    new Vector3(-halfW,  halfH, 0f)
                };
                var uvs = new Vector2[4]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                };
                // Double-sided winding order: 0,2,1 faces the camera (-Z), 0,1,2 faces away (+Z)
                var quadTriangles = new int[12]
                {
                    0, 2, 1, 0, 3, 2,
                    0, 1, 2, 0, 2, 3
                };

                _mesh.Clear();
                _mesh.vertices = quadVertices;
                _mesh.uv = uvs;
                _mesh.triangles = quadTriangles;
                _mesh.colors = null;
            }
            else
            {
                float radius = Mathf.Sqrt((w * w) / 4f + (h * h) / 4f) * 1.05f; // slightly larger to guarantee coverage
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
            }
            
            // Align to camera
            float actualDist = targetCamera.orthographic ? Mathf.Max(distance, 75f) : distance;
            transform.position = targetCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, actualDist));
            transform.rotation = targetCamera.transform.rotation;
        }
    }
}
