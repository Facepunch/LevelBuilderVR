using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace LevelBuilderVR
{
    public struct MeshData : IDisposable
    {
        public NativeArray<float3> Vertices;
        public NativeArray<float3> Normals;
        public NativeArray<float2> Uvs;
        public NativeArray<int> Indices;

        public int VertexOffset;
        public int IndexOffset;

        public MeshData(int maxVertices, int maxIndices)
        {
            Vertices = new NativeArray<float3>(maxVertices, Allocator.TempJob);
            Normals = new NativeArray<float3>(maxVertices, Allocator.TempJob);
            Uvs = new NativeArray<float2>(maxVertices, Allocator.TempJob);
            Indices = new NativeArray<int>(maxIndices, Allocator.TempJob);

            VertexOffset = 0;
            IndexOffset = 0;
        }

        public int AddVertex(float3 position, float3 normal, float2 uv)
        {
            var index = VertexOffset++;

            Vertices[index] = position;
            Normals[index] = normal;
            Uvs[index] = uv;

            return index;
        }

        public void AddTriangle(int a, int b, int c)
        {
            Indices[IndexOffset++] = a;
            Indices[IndexOffset++] = b;
            Indices[IndexOffset++] = c;
        }

        public void Dispose()
        {
            Vertices.Dispose();
            Normals.Dispose();
            Uvs.Dispose();
            Indices.Dispose();
        }

        private static readonly int[] _sEmptyIndices = new int[0];

        public void CopyToMesh(Mesh mesh)
        {
            mesh.SetIndices(_sEmptyIndices, MeshTopology.Triangles, 0);
            mesh.SetVertices(Vertices, 0, VertexOffset);
            mesh.SetNormals(Normals, 0, VertexOffset);
            mesh.SetUVs(0, Uvs, 0, VertexOffset);
            mesh.SetIndices(Indices, 0, IndexOffset, MeshTopology.Triangles, 0);
        }
    }
}
