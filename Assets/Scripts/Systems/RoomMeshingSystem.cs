using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace LevelBuilderVR.Systems
{
    public class RoomMeshingSystem : ComponentSystem
    {
        private const int MaxVertices = 1024;
        private const int MaxIndices = MaxVertices * 4;

        private EntityQuery _changedRoomsQuery;
        private EntityQuery _halfEdgesQuery;

        protected override void OnCreate()
        {
            _changedRoomsQuery = Entities
                .WithAllReadOnly<Room, DirtyMesh>()
                .WithAnyReadOnly<FlatFloor, FlatCeiling, SlopedFloor, SlopedCeiling, RenderMesh>()
                .ToEntityQuery();

            _halfEdgesQuery = Entities
                .WithAllReadOnly<HalfEdge>()
                .ToEntityQuery();
        }

        private static bool GetFaceDescription<TFlat, TSloped>(Entity roomEntity, ComponentDataFromEntity<Vertex> getVertex, ComponentDataFromEntity<TFlat> getFlat, ComponentDataFromEntity<TSloped> getSloped, out float3 a, out float3 b, out float3 c)
            where TFlat : struct, IComponentData, IFlatFace
            where TSloped : struct, IComponentData, ISlopedFace
        {
            if (getFlat.HasComponent(roomEntity))
            {
                var flatFloor = getFlat[roomEntity];

                a = new float3(0f, flatFloor.Y, 0f);
                b = new float3(1f, flatFloor.Y, 0f);
                c = new float3(0f, flatFloor.Y, 1f);

                return true;
            }

            if (getSloped.HasComponent(roomEntity))
            {
                var slopedFloor = getSloped[roomEntity];

                var vertex0 = getVertex[slopedFloor.Anchor0.Vertex];
                var vertex1 = getVertex[slopedFloor.Anchor1.Vertex];
                var vertex2 = getVertex[slopedFloor.Anchor2.Vertex];

                a = new float3(vertex0.X, slopedFloor.Anchor0.Y, vertex0.Z);
                b = new float3(vertex1.X, slopedFloor.Anchor1.Y, vertex1.Z);
                c = new float3(vertex2.X, slopedFloor.Anchor2.Y, vertex2.Z);

                return true;
            }

            a = default(float3);
            b = default(float3);
            c = default(float3);

            return false;
        }

        private static float GetFaceY(ref float3 a, ref float3 b, ref float3 c, float x, float z)
        {
            // TODO
            return a.y;
        }

        private static float3 VertexToFloat3(ref float3 a, ref float3 b, ref float3 c, Vertex vertex)
        {
            return new float3(vertex.X, GetFaceY(ref a, ref b, ref c, vertex.X, vertex.Z), vertex.Z);
        }

        protected override void OnUpdate()
        {
            var getFlatFloor = GetComponentDataFromEntity<FlatFloor>(true);
            var getSlopedFloor = GetComponentDataFromEntity<SlopedFloor>(true);
            var getFlatCeiling = GetComponentDataFromEntity<FlatCeiling>(true);
            var getSlopedCeiling = GetComponentDataFromEntity<SlopedCeiling>(true);

            var getVertex = GetComponentDataFromEntity<Vertex>(true);
            var getHalfEdge = GetComponentDataFromEntity<HalfEdge>(true);

            using (var changedRooms = _changedRoomsQuery.ToEntityArray(Allocator.TempJob))
            using (var halfEdges = _halfEdgesQuery.ToEntityArray(Allocator.TempJob))
            {
                var vertices = new NativeArray<float3>(MaxVertices, Allocator.TempJob);
                var normals = new NativeArray<float3>(MaxVertices, Allocator.TempJob);
                var indices = new NativeArray<int>(MaxIndices, Allocator.TempJob);

                foreach (var roomEntity in changedRooms)
                {
                    PostUpdateCommands.RemoveComponent<DirtyMesh>(roomEntity);

                    var hasFloor = GetFaceDescription(roomEntity, getVertex, getFlatFloor, getSlopedFloor,
                        out var floor0, out var floor1, out var floor2);
                    var hasCeiling = GetFaceDescription(roomEntity, getVertex, getFlatCeiling, getSlopedCeiling,
                        out var ceiling0, out var ceiling1, out var ceiling2);

                    var vertexOffset = 0;
                    var indexOffset = 0;

                    foreach (var halfEdgeEntity in halfEdges)
                    {
                        var halfEdge = getHalfEdge[halfEdgeEntity];
                        if (!halfEdge.Room.Equals(roomEntity))
                        {
                            continue;
                        }

                        var vertex0 = getVertex[halfEdge.Vertex0];
                        var vertex1 = getVertex[halfEdge.Vertex1];

                        var diff = new float2(vertex1.X, vertex1.Z) - new float2(vertex0.X, vertex0.Z);
                        var normal = new float3(-diff.y, 0f, diff.x);

                        if (hasFloor && hasCeiling)
                        {
                            int wall0, wall1, wall2, wall3;

                            vertices[wall0 = vertexOffset++] = VertexToFloat3(ref floor0, ref floor1, ref floor2, vertex0);
                            vertices[wall1 = vertexOffset++] = VertexToFloat3(ref floor0, ref floor1, ref floor2, vertex1);

                            normals[wall0] = normal;
                            normals[wall1] = normal;

                            vertices[wall2 = vertexOffset++] = VertexToFloat3(ref ceiling0, ref ceiling1, ref ceiling2, vertex0);
                            vertices[wall3 = vertexOffset++] = VertexToFloat3(ref ceiling0, ref ceiling1, ref ceiling2, vertex1);

                            normals[wall2] = normal;
                            normals[wall3] = normal;

                            indices[indexOffset++] = wall0;
                            indices[indexOffset++] = wall1;
                            indices[indexOffset++] = wall2;

                            indices[indexOffset++] = wall2;
                            indices[indexOffset++] = wall1;
                            indices[indexOffset++] = wall3;
                        }
                    }

                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(roomEntity);

                    renderMesh.mesh.SetVertices(vertices, 0, vertexOffset);
                    renderMesh.mesh.SetNormals(normals, 0, vertexOffset);
                    renderMesh.mesh.SetIndices(indices, 0, indexOffset, MeshTopology.Triangles, 0);
                }

                vertices.Dispose();
                normals.Dispose();
                indices.Dispose();
            }

            var getLocalToWorld = GetComponentDataFromEntity<LocalToWorld>();

            Entities
                .WithAllReadOnly<Room, RenderMesh, WithinLevel>()
                .WithAll<LocalToWorld, RenderBounds>()
                .ForEach((Entity entity, ref LocalToWorld localToWorld, ref RenderBounds renderBounds) =>
                {
                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(entity);
                    var withinLevel = EntityManager.GetSharedComponentData<WithinLevel>(entity);

                    localToWorld = getLocalToWorld[withinLevel.Level];

                    renderBounds.Value = new AABB
                    {
                        Center = renderMesh.mesh.bounds.center,
                        Extents = renderMesh.mesh.bounds.extents
                    };
                });
        }
    }
}
