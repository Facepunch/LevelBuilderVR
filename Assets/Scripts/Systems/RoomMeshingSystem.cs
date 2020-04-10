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
                .WithAll<RenderMesh>()
                .WithAnyReadOnly<FlatFloor, FlatCeiling, SlopedFloor, SlopedCeiling>()
                .ToEntityQuery();

            _halfEdgesQuery = Entities
                .WithAllReadOnly<HalfEdge>()
                .ToEntityQuery();
        }

        private static bool GetFacePlane<TFlat, TSloped>(Entity roomEntity, ComponentDataFromEntity<Vertex> getVertex, ComponentDataFromEntity<TFlat> getFlat, ComponentDataFromEntity<TSloped> getSloped, out Plane plane)
            where TFlat : struct, IComponentData, IFlatFace
            where TSloped : struct, IComponentData, ISlopedFace
        {
            if (getFlat.HasComponent(roomEntity))
            {
                var flatFloor = getFlat[roomEntity];

                plane = new Plane
                {
                    Normal = new float3(0f, 1f, 0f),
                    Point = new float3(0f, flatFloor.Y, 0f)
                };

                return true;
            }

            if (getSloped.HasComponent(roomEntity))
            {
                var slopedFloor = getSloped[roomEntity];

                var vertex0 = getVertex[slopedFloor.Anchor0.Vertex];
                var vertex1 = getVertex[slopedFloor.Anchor1.Vertex];
                var vertex2 = getVertex[slopedFloor.Anchor2.Vertex];

                var a = new float3(vertex0.X, slopedFloor.Anchor0.Y, vertex0.Z);
                var b = new float3(vertex1.X, slopedFloor.Anchor1.Y, vertex1.Z);
                var c = new float3(vertex2.X, slopedFloor.Anchor2.Y, vertex2.Z);

                var n = math.normalize(math.cross(b - a, c - a));

                plane = new Plane
                {
                    Normal = n.y < 0f ? -n : n,
                    Point = (a + b + c) / 3f
                };

                return true;
            }

            plane = new Plane
            {
                Normal = new float3(0f, 1f, 0f),
                Point = float3.zero
            };

            return false;
        }

        private static float3 VertexToFloat3(in Plane plane, Vertex vertex)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (plane.Normal.y == 1f)
            {
                return new float3(vertex.X, plane.Point.y, vertex.Z);
            }

            return math.dot(plane.Normal, plane.Point - new float3(vertex.X, 0f, vertex.Z)) / plane.Normal.y;
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
                var uvs = new NativeArray<float2>(MaxVertices, Allocator.TempJob);
                var indices = new NativeArray<int>(MaxIndices, Allocator.TempJob);

                foreach (var roomEntity in changedRooms)
                {
                    PostUpdateCommands.RemoveComponent<DirtyMesh>(roomEntity);

                    var hasFloor = GetFacePlane(roomEntity, getVertex, getFlatFloor, getSlopedFloor,
                        out var floor);
                    var hasCeiling = GetFacePlane(roomEntity, getVertex, getFlatCeiling, getSlopedCeiling,
                        out var ceiling);

                    var vertexOffset = 0;
                    var indexOffset = 0;

                    var firstHalfEdgeEntity = Entity.Null;
                    var halfEdgeCount = 0;

                    foreach (var halfEdgeEntity in halfEdges)
                    {
                        var halfEdge = getHalfEdge[halfEdgeEntity];
                        if (halfEdge.Room != roomEntity)
                        {
                            continue;
                        }

                        ++halfEdgeCount;

                        if (firstHalfEdgeEntity == Entity.Null)
                        {
                            firstHalfEdgeEntity = halfEdgeEntity;
                        }

                        var vertex0 = getVertex[halfEdge.Vertex];
                        var vertex1 = getVertex[getHalfEdge[halfEdge.Next].Vertex];

                        var diff = new float2(vertex1.X, vertex1.Z) - new float2(vertex0.X, vertex0.Z);
                        var normal = math.normalize(new float3(diff.y, 0f, -diff.x));
                        var tangent = math.cross(normal, new float3(0f, 1f, 0f));

                        var u0 = math.dot(tangent, new float3(vertex0.X, 0f, vertex0.Z));
                        var u1 = math.dot(tangent, new float3(vertex1.X, 0f, vertex1.Z));

                        if (!hasFloor || !hasCeiling) continue;

                        int wall0, wall1, wall2, wall3;

                        vertices[wall0 = vertexOffset++] = VertexToFloat3(in floor, vertex0);
                        vertices[wall1 = vertexOffset++] = VertexToFloat3(in floor, vertex1);

                        normals[wall0] = normal;
                        normals[wall1] = normal;

                        uvs[wall0] = new float2(u0, vertices[wall0].y);
                        uvs[wall1] = new float2(u1, vertices[wall1].y);

                        vertices[wall2 = vertexOffset++] = VertexToFloat3(in ceiling, vertex0);
                        vertices[wall3 = vertexOffset++] = VertexToFloat3(in ceiling, vertex1);

                        normals[wall2] = normal;
                        normals[wall3] = normal;

                        uvs[wall2] = new float2(u0, vertices[wall2].y);
                        uvs[wall3] = new float2(u1, vertices[wall3].y);

                        indices[indexOffset++] = wall0;
                        indices[indexOffset++] = wall2;
                        indices[indexOffset++] = wall1;

                        indices[indexOffset++] = wall2;
                        indices[indexOffset++] = wall3;
                        indices[indexOffset++] = wall1;
                    }

                    var firstFloorIndex = -1;
                    var firstCeilIndex = -1;

                    var curHalfEdgeEntity = firstHalfEdgeEntity;
                    do
                    {
                        var halfEdge = getHalfEdge[curHalfEdgeEntity];

                        var vertex0 = getVertex[halfEdge.Vertex];
                        var vertex1 = getVertex[getHalfEdge[halfEdge.Next].Vertex];

                        if (hasFloor)
                        {
                            if (firstFloorIndex == -1)
                            {
                                firstFloorIndex = vertexOffset;
                            }

                            int floor0, floor1;

                            vertices[floor0 = vertexOffset++] = VertexToFloat3(in floor, vertex0);
                            vertices[floor1 = vertexOffset++] = VertexToFloat3(in floor, vertex1);

                            normals[floor0] = floor.Normal;
                            normals[floor1] = floor.Normal;

                            uvs[floor0] = new float2(vertex0.X, vertex0.Z);
                            uvs[floor1] = new float2(vertex1.X, vertex1.Z);

                            if (curHalfEdgeEntity != firstHalfEdgeEntity)
                            {
                                indices[indexOffset++] = firstFloorIndex;
                                indices[indexOffset++] = floor0;
                                indices[indexOffset++] = floor1;
                            }
                        }

                        if (hasCeiling)
                        {
                            if (firstCeilIndex == -1)
                            {
                                firstCeilIndex = vertexOffset;
                            }

                            int ceiling0, ceiling1;

                            vertices[ceiling0 = vertexOffset++] = VertexToFloat3(in ceiling, vertex0);
                            vertices[ceiling1 = vertexOffset++] = VertexToFloat3(in ceiling, vertex1);

                            normals[ceiling0] = -ceiling.Normal;
                            normals[ceiling1] = -ceiling.Normal;

                            uvs[ceiling0] = new float2(vertex0.X, vertex0.Z);
                            uvs[ceiling1] = new float2(vertex1.X, vertex1.Z);

                            if (curHalfEdgeEntity != firstHalfEdgeEntity)
                            {
                                indices[indexOffset++] = firstCeilIndex;
                                indices[indexOffset++] = ceiling1;
                                indices[indexOffset++] = ceiling0;
                            }
                        }

                        curHalfEdgeEntity = halfEdge.Next;
                    } while (curHalfEdgeEntity != firstHalfEdgeEntity && --halfEdgeCount > 0);

                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(roomEntity);

                    renderMesh.mesh.SetVertices(vertices, 0, vertexOffset);
                    renderMesh.mesh.SetNormals(normals, 0, vertexOffset);
                    renderMesh.mesh.SetUVs(0, uvs, 0, vertexOffset);
                    renderMesh.mesh.SetIndices(indices, 0, indexOffset, MeshTopology.Triangles, 0);
                }

                vertices.Dispose();
                normals.Dispose();
                uvs.Dispose();
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
