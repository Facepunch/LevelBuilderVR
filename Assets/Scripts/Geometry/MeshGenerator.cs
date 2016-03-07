using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace LevelBuilder.Geometry
{
    public class MeshGenerator : IDisposable
    {
        private struct Vertex : IEquatable<Vertex>
        {
            public readonly Vector3 Position;
            public readonly Vector2 TexCoord;
            public readonly Vector3 Normal;
            public readonly int HashCode;

            public Vertex(Vector3 position, Vector2 texCoord, Vector3 normal)
            {
                Position = position;
                TexCoord = texCoord;
                Normal = normal;
                HashCode = Position.GetHashCode();
            }

            public bool Equals(Vertex other)
            {
                return Position.Equals(other.Position)
                       && TexCoord.Equals(other.TexCoord)
                       && Normal.Equals(other.Normal);
            }

            public override bool Equals(object obj)
            {
                return obj is Vertex && Equals((Vertex) obj);
            }

            public override int GetHashCode()
            {
                return HashCode;
            }
        }

        public static MeshGenerator Create()
        {
            return new MeshGenerator();
        }

        private static void Release(MeshGenerator meshGen)
        {
            // TODO
        }

        private readonly Dictionary<Vertex, int> _vertices = new Dictionary<Vertex, int>(); 

        private readonly Dictionary<int, DynamicArray<int>> _indices = new Dictionary<int, DynamicArray<int>>();
        private readonly DynamicArray<Vector3> _positions = new DynamicArray<Vector3>();
        private readonly DynamicArray<Vector2> _texCoords = new DynamicArray<Vector2>();
        private readonly DynamicArray<Vector3> _normals = new DynamicArray<Vector3>();

        private readonly Stack<Vector3> _offsets = new Stack<Vector3>();
        private Vector3 _currentOffset = Vector3.zero; 

        private readonly Stack<int> _subMeshIndices = new Stack<int>();
        private int _currentSubMeshIndex = 0;
        private DynamicArray<int> _currentSubMesh = null;

        private MeshGenerator()
        {
            SetSubMeshIndex(0);
        }

        private void SetSubMeshIndex(int index)
        {
            _currentSubMeshIndex = index;
            if (_indices.TryGetValue(index, out _currentSubMesh)) return;

            _currentSubMesh = new DynamicArray<int>();
            _indices.Add(index, _currentSubMesh);
        }

        public void PushOffset(Vector3 offset)
        {
            _offsets.Push(_currentOffset);
            _currentOffset += offset;
        }

        public void PopOffset()
        {
            if (_offsets.Count == 0) return;
            _currentOffset = _offsets.Pop();
        }

        public void PushSubmesh(int index)
        {
            _subMeshIndices.Push(_currentSubMeshIndex);
            SetSubMeshIndex(index);
        }

        public void PopSubmesh()
        {
            if (_subMeshIndices.Count == 0) return;
            SetSubMeshIndex(_subMeshIndices.Pop());
        }

        public void AddFloor(Vector3 normal, float yPos, IList<Vector2> verts)
        {
            if (verts.Count < 3) return;

            Helper.Triangulate(verts, (a, b, c) =>
            {
                AddTriangle(
                    new Vector3(a.x, yPos, a.y),
                    new Vector3(b.x, yPos, b.y),
                    new Vector3(c.x, yPos, c.y),
                    new Vector2(a.x, a.y) * 0.25f,
                    new Vector2(b.x, b.y) * 0.25f,
                    new Vector2(c.x, c.y) * 0.25f,
                    normal);
            });
        }

        public void AddFloor(Vector3 normal, float yPos, params Vector2[] verts)
        {
            AddFloor(normal, yPos, (IList<Vector2>) verts);
        }

        public void AddWall(Vector3 bottomLeft, Vector3 topRight)
        {
            var normal = -Vector3.Cross(Vector3.up, bottomLeft - topRight).normalized;

            var a = bottomLeft;
            var b = new Vector3(bottomLeft.x, topRight.y, bottomLeft.z);
            var c = topRight;
            var d = new Vector3(topRight.x, bottomLeft.y, topRight.z);

            var uvEnd = Mathf.Round(Helper.SwizzleXz(d - a).magnitude) * 0.5f;

            AddQuad(a, b, c, d,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(uvEnd, 1f),
                new Vector2(uvEnd, 0f),
                normal);
        }

        public void AddTriangle(Vector3 aPos, Vector3 bPos, Vector3 cPos,
            Vector2 aUv, Vector2 bUv, Vector2 cUv, Vector3 normal)
        {
            AddVertex(aPos, aUv, normal);
            AddVertex(bPos, bUv, normal);
            AddVertex(cPos, cUv, normal);
        }

        public void AddQuad(Vector3 aPos, Vector3 bPos, Vector3 cPos, Vector3 dPos,
            Vector2 aUv, Vector2 bUv, Vector2 cUv, Vector2 dUv, Vector3 normal)
        {
            AddVertex(aPos, aUv, normal);
            AddVertex(bPos, bUv, normal);
            AddVertex(cPos, cUv, normal);
            AddVertex(aPos, aUv, normal);
            AddVertex(cPos, cUv, normal);
            AddVertex(dPos, dUv, normal);
        }

        private void AddIndex(int index)
        {
            _currentSubMesh.Add(index);
        }

        public void AddVertex(Vector3 position, Vector2 texCoord, Vector3 normal)
        {
            position += _currentOffset;

            var vertex = new Vertex(position, texCoord, normal);

            int index;
            if (_vertices.TryGetValue(vertex, out index))
            {
                AddIndex(index);
                return;
            }

            Debug.Assert(_positions.Count == _normals.Count);

            index = _positions.Count;
            _vertices.Add(vertex, index);
            
            _positions.Add(position);
            _texCoords.Add(texCoord);
            _normals.Add(normal);

            AddIndex(index);
        }

        public void Clear()
        {
            _vertices.Clear();

            _indices.Clear();
            _positions.Clear();
            _texCoords.Clear();
            _normals.Clear();

            SetSubMeshIndex(_currentSubMeshIndex);
        }

        public void CopyToMesh(Mesh mesh)
        {
            mesh.Clear(false);

            mesh.vertices = _positions.GetArray();
            mesh.uv = _texCoords.GetArray();
            mesh.normals = _normals.GetArray();

            mesh.subMeshCount = _indices.Max(x => x.Key) + 1;

            foreach (var pair in _indices)
            {
                var indices = pair.Value.GetArray();

                unsafe
                {
                    fixed (void* pIndices = indices)
                    {
                        var length = (UIntPtr*) pIndices - 1;
                        var originalLength = *length;

                        try
                        {
                            *length = (UIntPtr) pair.Value.Count;
                            mesh.SetIndices(indices, MeshTopology.Triangles, pair.Key);
                        }
                        finally
                        {
                            *length = originalLength;
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            Release(this);
        }
    }
}