namespace ColliderVisualizer
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Collider visualizer.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Collider))]
    public class ColliderVisualizer : MonoBehaviour
    {
        private static readonly int ColorPropertyID = Shader.PropertyToID("_Color");

        private static readonly int[,] BoxEdges =
        {
            { 0, 1 }, { 1, 3 }, { 3, 2 }, { 2, 0 },
            { 4, 5 }, { 5, 7 }, { 7, 6 }, { 6, 4 },
            { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 }
        };

        /// <summary>
        /// The need to draw the collider fill has changed.
        /// </summary>
        public event Action onNeedDrawSolidChanged = delegate { };
        
        /// <summary>
        /// The need to draw collider boundaries has changed.
        /// </summary>
        public event Action onNeedDrawWireChanged = delegate { };
        
        /// <summary>
        /// The fill color has changed.
        /// </summary>
        public event Action onSolidColorChanged = delegate { };
        
        /// <summary>
        /// The border color has changed.
        /// </summary>
        public event Action onWireColorChanged = delegate { };
                
        /// <summary>
        /// Need to draw a fill.
        /// </summary>
        public bool NeedDrawSolid
        {
            get => drawSolid;

            set
            {
                if (drawSolid != value)
                {
                    drawSolid = value;
                    onNeedDrawSolidChanged();
                }
            }
        }
        
        /// <summary>
        /// You need to draw boundaries.
        /// </summary>
        public bool NeedDrawWire
        {
            get => drawWire;

            set
            {
                if (drawWire != value)
                {
                    drawWire = value;
                    onNeedDrawWireChanged();
                }
            }
        }
        
        /// <summary>
        /// Fill color.
        /// </summary>
        public Color SolidColor
        {
            get => solidColor;

            set
            {
                if (solidColor != value)
                {
                    solidColor = value;
                    onSolidColorChanged();
                }
            }
        }
                
        /// <summary>
        /// Border color.
        /// </summary>
        public Color WireColor
        {
            get => wireColor;

            set
            {
                if (wireColor != value)
                {
                    wireColor = value;
                    onWireColorChanged();
                }
            }
        }
        
        [Header("Rendering")] [SerializeField] private bool drawSolid = true;
        [SerializeField] private bool drawWire = true;

        [SerializeField] private Color solidColor = new Color(0f, 1f, 0f, 0.15f);
        [SerializeField] private Color wireColor = Color.green;

        [Header("Wire Quality")] 
        [SerializeField, Range(3, 64)] private int wireSegments = 24;

        [SerializeField, HideInInspector, Range(0, 100000)] private int maxMeshTriangles = 0; // 0 = without limit
        [SerializeField, Range(2, 32)] private int capsuleArcSegments = 8;

        [SerializeField, HideInInspector] private Mesh meshBox;
        [SerializeField, HideInInspector] private Mesh meshSphere;

        [SerializeField, HideInInspector] private Mesh meshCylinder;
        [SerializeField, HideInInspector] private Mesh upperHalfSphere;
        [SerializeField, HideInInspector] private Mesh lowerHalfSphere;

        [SerializeField, HideInInspector] private Shader coloredShader;
        
        private Material solidMat;
        private Material wireMat;
        
        private Collider col;

        private Transform _transform;

        private void Start() => Initialize();
        
        /// <summary>
        /// Initializes the component.
        /// </summary>
        public void Initialize()
        {
            col = GetComponent<Collider>();
            if (col == null) return;

            _transform = transform;

            RemoveMaterials();
            
            solidMat = new Material(coloredShader);
            wireMat = new Material(coloredShader);
        }

        private void OnRenderObject()
        {
            if (!enabled || col == null) return;

            if (drawSolid)
            {
                solidMat.SetColor(ColorPropertyID, solidColor);
                solidMat.SetPass(0);
                DrawSolid(col);
            }

            if (drawWire)
            {
                wireMat.SetColor(ColorPropertyID, wireColor);
                wireMat.SetPass(0);

                if (col is SphereCollider)
                {
                    GL.Begin(GL.LINES);
                    GL.Color(wireColor);
                    DrawWire(col);
                    GL.End();
                }
                else
                {
                    GL.PushMatrix();
                    GL.MultMatrix(transform.localToWorldMatrix);
                    GL.Begin(GL.LINES);
                    GL.Color(wireColor);
                    DrawWire(col);
                    GL.End();
                    GL.PopMatrix();
                }
            }
        }
        
        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                RemoveMaterials();
            }
        }

        private void RemoveMaterials()
        {
#if UNITY_EDITOR
            if (solidMat != null)
            {
                DestroyImmediate(solidMat);
            }

            if (wireMat != null)
            {
                DestroyImmediate(wireMat);
            }
#else
            if (solidMat != null)
            {
                Destroy(solidMat);
            }

            if (wireMat != null)
            {
                Destroy(wireMat);
            }
#endif
        }

        private void DrawSolid(Collider collider)
        {
            var tf = transform;
            var rotation = tf.rotation;
            var scale = tf.lossyScale;
            var position = tf.position;

            switch (collider)
            {
                case BoxCollider box:
                    Graphics.DrawMeshNow(meshBox,
                        Matrix4x4.TRS(position + rotation * box.center, rotation, Vector3.Scale(box.size, scale)));
                    break;

                case SphereCollider sphere:
                    float scaledRadius = sphere.radius * MaxAbsAxis(scale);
                    Graphics.DrawMeshNow(meshSphere,
                        Matrix4x4.TRS(position + rotation * sphere.center, rotation, Vector3.one * scaledRadius * 2f));
                    break;

                case CapsuleCollider capsule:
                    DrawCapsuleSolid(capsule);
                    break;

                case MeshCollider meshCol:
                    if (meshCol.sharedMesh != null)
                        Graphics.DrawMeshNow(meshCol.sharedMesh, tf.localToWorldMatrix);
                    break;
            }
        }

        private void DrawWire(Collider collider)
        {
            switch (collider)
            {
                case BoxCollider box:
                    DrawWireCube(box.center, box.size);
                    break;

                case SphereCollider sphere:
                    DrawWireSphere(sphere.center, sphere.radius);
                    break;

                case CapsuleCollider capsule:
                    DrawWireCapsule(capsule);
                    break;

                case MeshCollider meshCol:
                    DrawWireMesh(meshCol);
                    break;
            }
        }

        private float MaxAbsAxis(Vector3 v) =>
            Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        #region Box

        private void DrawWireCube(Vector3 center, Vector3 size)
        {
            Vector3 half = size * 0.5f;
            Vector3[] pts = new Vector3[8];

            for (int i = 0; i < 8; i++)
            {
                pts[i] = new Vector3(
                    ((i & 1) == 0 ? -1 : 1) * half.x,
                    ((i & 2) == 0 ? -1 : 1) * half.y,
                    ((i & 4) == 0 ? -1 : 1) * half.z
                ) + center;
            }

            for (int i = 0; i < BoxEdges.GetLength(0); i++)
            {
                GL.Vertex(pts[BoxEdges[i, 0]]);
                GL.Vertex(pts[BoxEdges[i, 1]]);
            }
        }

        #endregion

        #region Sphere

        private void DrawWireSphere(Vector3 center, float radius)
        {
            float maxScale = MaxAbsAxis(_transform.lossyScale);
            float scaledRadius = radius * maxScale;
            float angleStep = 360f / wireSegments;

            for (int i = 0; i < wireSegments; i++)
            {
                float a0 = Mathf.Deg2Rad * i * angleStep;
                float a1 = Mathf.Deg2Rad * (i + 1) * angleStep;

                Vector3 c = _transform.TransformPoint(center);

                // XY
                Vector3 p0 = c + _transform.rotation * new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * scaledRadius;
                Vector3 p1 = c + _transform.rotation * new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * scaledRadius;
                GL.Vertex(p0);
                GL.Vertex(p1);

                // XZ
                p0 = c + _transform.rotation * new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * scaledRadius;
                p1 = c + _transform.rotation * new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * scaledRadius;
                GL.Vertex(p0);
                GL.Vertex(p1);

                // YZ
                p0 = c + _transform.rotation * new Vector3(0, Mathf.Cos(a0), Mathf.Sin(a0)) * scaledRadius;
                p1 = c + _transform.rotation * new Vector3(0, Mathf.Cos(a1), Mathf.Sin(a1)) * scaledRadius;
                GL.Vertex(p0);
                GL.Vertex(p1);
            }
        }

        #endregion

        #region Capsule

        private void GetCapsuleAxisAndScale(CapsuleCollider capsule, out Vector3 up, out Quaternion rotation,
            out float radiusScale, out float heightScale)
        {
            up = Vector3.up;
            rotation = transform.rotation;
            radiusScale = 1f;
            heightScale = 1f;

            switch (capsule.direction)
            {
                case 0:
                    up = Vector3.right;
                    rotation *= Quaternion.Euler(0, 0, -90);
                    radiusScale = Mathf.Max(transform.lossyScale.y, transform.lossyScale.z);
                    heightScale = transform.lossyScale.x;
                    break;
                case 1:
                    up = Vector3.up;
                    radiusScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
                    heightScale = transform.lossyScale.y;
                    break;
                case 2:
                    up = Vector3.forward;
                    rotation *= Quaternion.Euler(90, 0, 0);
                    radiusScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
                    heightScale = transform.lossyScale.z;
                    break;
            }
        }

        private void DrawCapsuleSolid(CapsuleCollider capsule)
        {
            GetCapsuleAxisAndScale(capsule, out var up, out var rotation, out var radiusScale, out var heightScale);

            float radius = capsule.radius;
            float height = capsule.height;
            Vector3 center = capsule.center;

            float worldRadius = radius * radiusScale;
            float worldHeight = height * heightScale;
            float cylinderHeight = Mathf.Max(0f, worldHeight - 2f * worldRadius);

            Vector3 worldCenter = _transform.position + _transform.rotation * center;
            Vector3 top = worldCenter + up * (cylinderHeight * 0.5f);
            Vector3 bottom = worldCenter - up * (cylinderHeight * 0.5f);

            Vector3 cylinderScale = new Vector3(worldRadius, cylinderHeight, worldRadius);
            Vector3 sphereScale = Vector3.one * worldRadius;

            Graphics.DrawMeshNow(meshCylinder, Matrix4x4.TRS(worldCenter, rotation, cylinderScale));
            Graphics.DrawMeshNow(upperHalfSphere, Matrix4x4.TRS(top, rotation, sphereScale));
            Graphics.DrawMeshNow(lowerHalfSphere, Matrix4x4.TRS(bottom, rotation, sphereScale));
        }


        private void DrawWireCapsule(CapsuleCollider capsule)
        {
            Vector3 center = capsule.center;
            float radius = capsule.radius;
            float height = capsule.height;

            Vector3 up = Vector3.up;
            switch (capsule.direction)
            {
                case 0: up = Vector3.right; break;
                case 1: up = Vector3.up; break;
                case 2: up = Vector3.forward; break;
            }

            float cylinderHeight = Mathf.Max(0, height - 2f * radius);
            Vector3 top = center + up * (cylinderHeight * 0.5f);
            Vector3 bottom = center - up * (cylinderHeight * 0.5f);

            Quaternion ringRot = Quaternion.FromToRotation(Vector3.up, up);
            float angleStep = 360f / wireSegments;

            for (int i = 0; i < wireSegments; i++)
            {
                float a0 = Mathf.Deg2Rad * i * angleStep;
                float a1 = Mathf.Deg2Rad * (i + 1) * angleStep;

                Vector3 dirA = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                Vector3 dirB = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));

                Vector3 offsetA = ringRot * dirA * radius;
                Vector3 offsetB = ringRot * dirB * radius;

                GL.Vertex(top + offsetA);
                GL.Vertex(bottom + offsetA);
                GL.Vertex(top + offsetA);
                GL.Vertex(top + offsetB);
                GL.Vertex(bottom + offsetA);
                GL.Vertex(bottom + offsetB);

                DrawArc(top, offsetA.normalized, up, radius, capsuleArcSegments);
                DrawArc(bottom, offsetA.normalized, -up, radius, capsuleArcSegments);
            }
        }

        private void DrawArc(Vector3 center, Vector3 tangent, Vector3 normal, float radius, int segments)
        {
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
            tangent = Vector3.Cross(bitangent, normal).normalized;

            float angleStep = Mathf.PI / segments;

            for (int i = 0; i < segments; i++)
            {
                float a0 = i * angleStep;
                float a1 = (i + 1) * angleStep;

                Vector3 p0 = center + Mathf.Cos(a0) * tangent * radius + Mathf.Sin(a0) * normal * radius;
                Vector3 p1 = center + Mathf.Cos(a1) * tangent * radius + Mathf.Sin(a1) * normal * radius;

                GL.Vertex(p0);
                GL.Vertex(p1);
            }
        }

        private Mesh GenerateHalfSphereMesh(bool upper = true, int longitude = 24, int latitude = 12)
        {
            Mesh mesh = new Mesh();
            mesh.name = upper ? "UpperHalfSphere" : "LowerHalfSphere";

            List<Vector3> vertices = new();
            List<int> triangles = new();

            int latStart = upper ? 0 : latitude / 2;
            int latEnd = upper ? latitude / 2 : latitude;

            for (int lat = latStart; lat <= latEnd; lat++)
            {
                float a1 = Mathf.PI * lat / latitude;
                float sin1 = Mathf.Sin(a1);
                float cos1 = Mathf.Cos(a1);

                for (int lon = 0; lon <= longitude; lon++)
                {
                    float a2 = 2f * Mathf.PI * lon / longitude;
                    float sin2 = Mathf.Sin(a2);
                    float cos2 = Mathf.Cos(a2);

                    Vector3 vertex = new Vector3(sin1 * cos2, cos1, sin1 * sin2);
                    vertices.Add(vertex);
                }
            }

            int vertsPerRow = longitude + 1;
            for (int lat = 0; lat < (latEnd - latStart); lat++)
            {
                for (int lon = 0; lon < longitude; lon++)
                {
                    int current = lat * vertsPerRow + lon;
                    int next = current + vertsPerRow;

                    triangles.Add(current);
                    triangles.Add(next);
                    triangles.Add(current + 1);

                    triangles.Add(current + 1);
                    triangles.Add(next);
                    triangles.Add(next + 1);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Mesh GenerateOpenCylinder(int segments = 24)
        {
            Mesh mesh = new Mesh();
            mesh.name = "OpenCylinder";

            List<Vector3> vertices = new();
            List<int> triangles = new();

            for (int i = 0; i <= segments; i++)
            {
                float angle = 2f * Mathf.PI * i / segments;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);

                vertices.Add(new Vector3(x, -0.5f, z));
                vertices.Add(new Vector3(x, 0.5f, z));
            }

            for (int i = 0; i < segments; i++)
            {
                int baseIndex = i * 2;

                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 3);
                triangles.Add(baseIndex + 1);

                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 3);
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        #endregion

        #region Mesh

        private void DrawWireMesh(MeshCollider meshCollider)
        {
            var mesh = meshCollider.sharedMesh;
            if (mesh == null) return;

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            int triangleCount = triangles.Length / 3;

            if (maxMeshTriangles > 0 && triangleCount > maxMeshTriangles)
                triangleCount = maxMeshTriangles;

            for (int i = 0; i < triangleCount * 3; i += 3)
            {
                int i0 = triangles[i];
                int i1 = triangles[i + 1];
                int i2 = triangles[i + 2];

                GL.Vertex(vertices[i0]);
                GL.Vertex(vertices[i1]);

                GL.Vertex(vertices[i1]);
                GL.Vertex(vertices[i2]);

                GL.Vertex(vertices[i2]);
                GL.Vertex(vertices[i0]);
            }
        }

        #endregion
    }
}