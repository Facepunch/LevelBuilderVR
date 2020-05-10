using System;
using LevelBuilderVR.Entities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace LevelBuilderVR.Systems
{
    [UpdateAfter(typeof(DirtyVertexSystem)), UpdateAfter(typeof(WidgetVisibleSystem)), UpdateBefore(typeof(WidgetTransformSystem))]
    public class RoomMeshingSystem : ComponentSystem
    {
        private const int MaxVertices = 1024;
        private const int MaxIndices = MaxVertices * 4;

        private static readonly int[] _sEmptyIndices = new int[0];

        private EntityQuery _changedRoomsQuery;
        private EntityQuery _halfEdgesQuery;
        private EntityQuery _roomsQuery;

        protected override void OnCreate()
        {
            _changedRoomsQuery = Entities
                .WithAllReadOnly<Room, DirtyMesh>()
                .WithAll<RenderMesh>()
                .WithAnyReadOnly<FlatFloor, FlatCeiling, SlopedFloor, SlopedCeiling>()
                .ToEntityQuery();

            _halfEdgesQuery = Entities
                .WithAll<HalfEdge>()
                .ToEntityQuery();

            _roomsQuery = Entities
                .WithAllReadOnly<Room, RenderMesh, WithinLevel>()
                .WithAll<LocalToWorld, RenderBounds>()
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

            return plane.ProjectOnto(new float3(vertex.X, 0f, vertex.Z), new float3(0f, 1f, 0f));
        }

        private struct MeshData : IDisposable
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

            public void Dispose()
            {
                Vertices.Dispose();
                Normals.Dispose();
                Uvs.Dispose();
                Indices.Dispose();
            }
        }

        private static void MeshWall(ref MeshData meshData,
            in float3 floor0, in float3 floor1,
            in float3 ceiling0, in float3 ceiling1,
            in float3 normal, float u0, float u1)
        {
            if (floor0.y >= ceiling0.y && floor1.y >= ceiling1.y)
            {
                return;
            }

            // TODO: (floor0.y >= ceiling0.y) != (floor1.y >= ceiling1.y)

            int wall0, wall1, wall2, wall3;

            meshData.Vertices[wall0 = meshData.VertexOffset++] = floor0;
            meshData.Vertices[wall1 = meshData.VertexOffset++] = floor1;

            meshData.Normals[wall0] = normal;
            meshData.Normals[wall1] = normal;

            meshData.Uvs[wall0] = new float2(u0, floor0.y);
            meshData.Uvs[wall1] = new float2(u1, floor1.y);

            meshData.Vertices[wall2 = meshData.VertexOffset++] = ceiling0;
            meshData.Vertices[wall3 = meshData.VertexOffset++] = ceiling1;

            meshData.Normals[wall2] = normal;
            meshData.Normals[wall3] = normal;

            meshData.Uvs[wall2] = new float2(u0, ceiling0.y);
            meshData.Uvs[wall3] = new float2(u1, ceiling1.y);

            meshData.Indices[meshData.IndexOffset++] = wall0;
            meshData.Indices[meshData.IndexOffset++] = wall2;
            meshData.Indices[meshData.IndexOffset++] = wall1;

            meshData.Indices[meshData.IndexOffset++] = wall2;
            meshData.Indices[meshData.IndexOffset++] = wall3;
            meshData.Indices[meshData.IndexOffset++] = wall1;
        }

        protected override void OnUpdate()
        {
            var getFlatFloor = GetComponentDataFromEntity<FlatFloor>(true);
            var getSlopedFloor = GetComponentDataFromEntity<SlopedFloor>(true);
            var getFlatCeiling = GetComponentDataFromEntity<FlatCeiling>(true);
            var getSlopedCeiling = GetComponentDataFromEntity<SlopedCeiling>(true);

            var getVertex = GetComponentDataFromEntity<Vertex>(true);
            var getVertexWritable = GetComponentDataFromEntity<Vertex>(false);
            var getHalfEdge = GetComponentDataFromEntity<HalfEdge>(true);
            var getHalfEdgeWritable = GetComponentDataFromEntity<HalfEdge>(false);

            var anyChangedRooms = false;

            using (var changedRooms = _changedRoomsQuery.ToEntityArray(Allocator.TempJob))
            using (var halfEdges = _halfEdgesQuery.ToEntityArray(Allocator.TempJob))
            {
                if (changedRooms.Length > 0)
                {
                    anyChangedRooms = true;
                }

                var meshData = new MeshData(MaxVertices, MaxIndices);

                foreach (var roomEntity in changedRooms)
                {
                    PostUpdateCommands.RemoveComponent<DirtyMesh>(roomEntity);

                    var hasFloor = GetFacePlane(roomEntity, getVertex, getFlatFloor, getSlopedFloor,
                        out var floor);
                    var hasCeiling = GetFacePlane(roomEntity, getVertex, getFlatCeiling, getSlopedCeiling,
                        out var ceiling);

                    meshData.VertexOffset = 0;
                    meshData.IndexOffset = 0;

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

                        if (hasFloor && hasCeiling)
                        {
                            var floor0 = VertexToFloat3(in floor, vertex0);
                            var floor1 = VertexToFloat3(in floor, vertex1);

                            var ceiling0 = VertexToFloat3(in ceiling, vertex0);
                            var ceiling1 = VertexToFloat3(in ceiling, vertex1);

                            halfEdge.MinY = math.min(floor0.y, ceiling0.y);
                            halfEdge.MaxY = math.max(floor0.y, ceiling0.y);

                            if (halfEdge.BackFace != Entity.Null)
                            {
                                var backFace = getHalfEdge[halfEdge.BackFace];

                                if (GetFacePlane(backFace.Room, getVertex, getFlatFloor, getSlopedFloor, out var backFloor))
                                {
                                    var backFloor0 = VertexToFloat3(in backFloor, vertex0);
                                    var backFloor1 = VertexToFloat3(in backFloor, vertex1);

                                    MeshWall(ref meshData, in floor0, in floor1, in backFloor0, in backFloor1, in normal, u0, u1);
                                }

                                if (GetFacePlane(backFace.Room, getVertex, getFlatCeiling, getSlopedCeiling, out var backCeiling))
                                {
                                    var backCeiling0 = VertexToFloat3(in backCeiling, vertex0);
                                    var backCeiling1 = VertexToFloat3(in backCeiling, vertex1);

                                    MeshWall(ref meshData, in backCeiling0, in backCeiling1, in ceiling0, in ceiling1, in normal, u0, u1);
                                }
                            }
                            else
                            {
                                MeshWall(ref meshData, in floor0, in floor1, in ceiling0, in ceiling1, in normal, u0, u1);
                            }
                        }
                        else if (hasFloor)
                        {
                            halfEdge.MinY = halfEdge.MaxY = VertexToFloat3(in floor, vertex0).y;
                        }
                        else if (hasCeiling)
                        {
                            halfEdge.MinY = halfEdge.MaxY = VertexToFloat3(in ceiling, vertex0).y;
                        }

                        getHalfEdgeWritable[halfEdgeEntity] = halfEdge;
                    }

                    if (hasFloor || hasCeiling)
                    {
                        var roomVertices = new NativeArray<float2>(halfEdgeCount, Allocator.TempJob);
                        var roomVertexIndex = 0;

                        var firstFloorOffset = meshData.VertexOffset;
                        var firstCeilingOffset = meshData.VertexOffset + (hasFloor ? 1 : 0);
                        var floorCeilingStride = (hasFloor ? 1 : 0) + (hasCeiling ? 1 : 0);

                        var curHalfEdgeEntity = firstHalfEdgeEntity;
                        do
                        {
                            var halfEdge = getHalfEdge[curHalfEdgeEntity];
                            var vertex = getVertex[halfEdge.Vertex];

                            if (hasFloor)
                            {
                                int floorIndex;
                                meshData.Vertices[floorIndex = meshData.VertexOffset++] = VertexToFloat3(in floor, vertex);
                                meshData.Normals[floorIndex] = floor.Normal;
                                meshData.Uvs[floorIndex] = new float2(vertex.X, vertex.Z);
                            }

                            if (hasCeiling)
                            {
                                int ceilingIndex;
                                meshData.Vertices[ceilingIndex = meshData.VertexOffset++] = VertexToFloat3(in ceiling, vertex);
                                meshData.Normals[ceilingIndex] = -ceiling.Normal;
                                meshData.Uvs[ceilingIndex] = new float2(vertex.X, vertex.Z);
                            }

                            roomVertices[roomVertexIndex++] = new float2(vertex.X, vertex.Z);

                            curHalfEdgeEntity = halfEdge.Next;
                        } while (curHalfEdgeEntity != firstHalfEdgeEntity && roomVertexIndex < halfEdgeCount);

                        var triangulationIndices = new NativeArray<int>(Helpers.GetIndexCount(halfEdgeCount), Allocator.TempJob);

                        Helpers.Triangulate(roomVertices, triangulationIndices, out var triangulationIndexCount);

                        if (hasFloor)
                        {
                            for (var i = 0; i < triangulationIndexCount; ++i)
                            {
                                meshData.Indices[meshData.IndexOffset++] = firstFloorOffset + triangulationIndices[i] * floorCeilingStride;
                            }
                        }

                        if (hasCeiling)
                        {
                            for (var i = triangulationIndexCount - 1; i >= 0; --i)
                            {
                                meshData.Indices[meshData.IndexOffset++] = firstCeilingOffset + triangulationIndices[i] * floorCeilingStride;
                            }
                        }

                        triangulationIndices.Dispose();
                        roomVertices.Dispose();
                    }

                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(roomEntity);

                    renderMesh.mesh.SetIndices(_sEmptyIndices, MeshTopology.Triangles, 0);
                    renderMesh.mesh.SetVertices(meshData.Vertices, 0, meshData.VertexOffset);
                    renderMesh.mesh.SetNormals(meshData.Normals, 0, meshData.VertexOffset);
                    renderMesh.mesh.SetUVs(0, meshData.Uvs, 0, meshData.VertexOffset);
                    renderMesh.mesh.SetIndices(meshData.Indices, 0, meshData.IndexOffset,
                        MeshTopology.Triangles, 0);
                }

                meshData.Dispose();
            }

            if (anyChangedRooms)
            {
                // Update HalfEdge and Vertex Min/MaxY

                Entities.WithAll<Vertex>()
                    .ForEach((ref Vertex vertex) =>
                    {
                        vertex.MinY = float.PositiveInfinity;
                        vertex.MaxY = float.NegativeInfinity;
                    });

                Entities.WithAllReadOnly<HalfEdge>()
                    .ForEach((ref HalfEdge halfEdge) =>
                    {
                        var vertex = getVertex[halfEdge.Vertex];

                        vertex.MinY = math.min(vertex.MinY, halfEdge.MinY);
                        vertex.MaxY = math.max(vertex.MaxY, halfEdge.MaxY);

                        getVertexWritable[halfEdge.Vertex] = vertex;
                    });
            }

            // Update Room and Vertex LocalToWorld / RenderBounds

            var getLocalToWorld = GetComponentDataFromEntity<LocalToWorld>();

            Entities
                .WithAllReadOnly<Level>()
                .ForEach(level =>
                {
                    var levelLocalToWorld = getLocalToWorld[level];

                    _roomsQuery.SetSharedComponentFilter(EntityManager.GetWithinLevel(level));

                    var rooms = _roomsQuery.ToEntityArray(Allocator.TempJob);
                    var localToWorlds = _roomsQuery.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);
                    var renderBoundses = _roomsQuery.ToComponentDataArray<RenderBounds>(Allocator.TempJob);

                    for (var i = 0; i < rooms.Length; ++i)
                    {
                        var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(rooms[i]);

                        localToWorlds[i] = levelLocalToWorld;
                        renderBoundses[i] = new RenderBounds
                        {
                            Value = new AABB
                            {
                                Center = renderMesh.mesh.bounds.center,
                                Extents = renderMesh.mesh.bounds.extents
                            }
                        };
                    }

                    _roomsQuery.CopyFromComponentDataArray(localToWorlds);
                    _roomsQuery.CopyFromComponentDataArray(renderBoundses);

                    rooms.Dispose();
                    localToWorlds.Dispose();
                    renderBoundses.Dispose();
                });

        }
    }
}
