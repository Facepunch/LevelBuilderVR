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

        private EntityQuery _changedRoomsQuery;
        private EntityQuery _halfEdgesQuery;
        private EntityQuery _roomsQuery;
        private EntityQuery _floorCeilingsQuery;

        protected override void OnCreate()
        {
            _changedRoomsQuery = Entities
                .WithAllReadOnly<Room, DirtyMesh>()
                .WithAll<RenderMesh>()
                .ToEntityQuery();

            _halfEdgesQuery = Entities
                .WithAll<HalfEdge>()
                .ToEntityQuery();

            _roomsQuery = Entities
                .WithAllReadOnly<Room, RenderMesh, WithinLevel>()
                .WithAll<LocalToWorld, RenderBounds>()
                .ToEntityQuery();

            _floorCeilingsQuery = Entities
                .WithAllReadOnly<FloorCeiling, RenderMesh, WithinLevel>()
                .WithAll<LocalToWorld, RenderBounds>()
                .ToEntityQuery();
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

            var wall0 = meshData.AddVertex(floor0, normal, new float2(u0, floor0.y));
            var wall1 = meshData.AddVertex(floor1, normal, new float2(u1, floor1.y));
            var wall2 = meshData.AddVertex(ceiling0, normal, new float2(u0, ceiling0.y));
            var wall3 = meshData.AddVertex(ceiling1, normal, new float2(u1, ceiling1.y));

            meshData.AddTriangle(wall0, wall2, wall1);
            meshData.AddTriangle(wall2, wall3, wall1);
        }

        protected override void OnUpdate()
        {
            var getVertex = GetComponentDataFromEntity<Vertex>(true);
            var getVertexWritable = GetComponentDataFromEntity<Vertex>(false);
            var getHalfEdge = GetComponentDataFromEntity<HalfEdge>(true);
            var getHalfEdgeWritable = GetComponentDataFromEntity<HalfEdge>(false);
            var getRoom = GetComponentDataFromEntity<Room>(true);
            var getFloorCeiling = GetComponentDataFromEntity<FloorCeiling>(true);

            var anyChangedRooms = false;

            using (var changedRooms = _changedRoomsQuery.ToEntityArray(Allocator.TempJob))
            using (var halfEdges = _halfEdgesQuery.ToEntityArray(Allocator.TempJob))
            {
                if (changedRooms.Length > 0)
                {
                    anyChangedRooms = true;
                }

                var roomMeshData = new MeshData(MaxVertices, MaxIndices);
                var floorCeilingMeshData = new MeshData(MaxVertices, MaxIndices);

                foreach (var roomEntity in changedRooms)
                {
                    PostUpdateCommands.RemoveComponent<DirtyMesh>(roomEntity);

                    var room = getRoom[roomEntity];

                    FloorCeiling floor = default, ceiling = default;

                    var hasFloor = room.Floor != Entity.Null;
                    var hasCeiling = room.Ceiling != Entity.Null;

                    if (hasFloor)
                    {
                        floor = getFloorCeiling[room.Floor];
                    }

                    if (hasCeiling)
                    {
                        ceiling = getFloorCeiling[room.Ceiling];
                    }

                    roomMeshData.VertexOffset = 0;
                    roomMeshData.IndexOffset = 0;

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
                            var floor0 = VertexToFloat3(in floor.Plane, vertex0);
                            var floor1 = VertexToFloat3(in floor.Plane, vertex1);

                            var ceiling0 = VertexToFloat3(in ceiling.Plane, vertex0);
                            var ceiling1 = VertexToFloat3(in ceiling.Plane, vertex1);

                            halfEdge.MinY = math.min(floor0.y, ceiling0.y);
                            halfEdge.MaxY = math.max(floor0.y, ceiling0.y);

                            if (halfEdge.BackFace != Entity.Null)
                            {
                                var backFace = getHalfEdge[halfEdge.BackFace];
                                var backRoom = getRoom[backFace.Room];

                                if (backRoom.Floor != Entity.Null)
                                {
                                    var backFloor = getFloorCeiling[backRoom.Floor];

                                    var backFloor0 = VertexToFloat3(in backFloor.Plane, vertex0);
                                    var backFloor1 = VertexToFloat3(in backFloor.Plane, vertex1);

                                    MeshWall(ref roomMeshData, in floor0, in floor1, in backFloor0, in backFloor1, in normal, u0, u1);
                                }

                                if (backRoom.Ceiling != Entity.Null)
                                {
                                    var backCeiling = getFloorCeiling[backRoom.Ceiling];

                                    var backCeiling0 = VertexToFloat3(in backCeiling.Plane, vertex0);
                                    var backCeiling1 = VertexToFloat3(in backCeiling.Plane, vertex1);

                                    MeshWall(ref roomMeshData, in backCeiling0, in backCeiling1, in ceiling0, in ceiling1, in normal, u0, u1);
                                }
                            }
                            else
                            {
                                MeshWall(ref roomMeshData, in floor0, in floor1, in ceiling0, in ceiling1, in normal, u0, u1);
                            }
                        }
                        else if (hasFloor)
                        {
                            halfEdge.MinY = halfEdge.MaxY = VertexToFloat3(in floor.Plane, vertex0).y;
                        }
                        else if (hasCeiling)
                        {
                            halfEdge.MinY = halfEdge.MaxY = VertexToFloat3(in ceiling.Plane, vertex0).y;
                        }

                        getHalfEdgeWritable[halfEdgeEntity] = halfEdge;
                    }

                    if (hasFloor || hasCeiling)
                    {
                        var roomVertices = new NativeArray<float2>(halfEdgeCount, Allocator.TempJob);
                        var roomVertexIndex = 0;

                        var renderFloor = hasFloor && floor.Below == Entity.Null;
                        var renderCeiling = hasCeiling && ceiling.Above == Entity.Null;

                        var firstFloorOffset = roomMeshData.VertexOffset;
                        var firstCeilingOffset = roomMeshData.VertexOffset + (renderFloor ? 1 : 0);
                        var floorCeilingStride = (renderFloor ? 1 : 0) + (renderCeiling ? 1 : 0);

                        var curHalfEdgeEntity = firstHalfEdgeEntity;
                        do
                        {
                            var halfEdge = getHalfEdge[curHalfEdgeEntity];
                            var vertex = getVertex[halfEdge.Vertex];

                            if (renderFloor)
                            {
                                roomMeshData.AddVertex(
                                    VertexToFloat3(in floor.Plane, vertex),
                                    floor.Plane.Normal,
                                    new float2(vertex.X, vertex.Z));
                            }

                            if (renderCeiling)
                            {
                                roomMeshData.AddVertex(
                                    VertexToFloat3(in ceiling.Plane, vertex),
                                    -ceiling.Plane.Normal,
                                    new float2(vertex.X, vertex.Z));
                            }

                            roomVertices[roomVertexIndex++] = new float2(vertex.X, vertex.Z);

                            curHalfEdgeEntity = halfEdge.Next;
                        } while (curHalfEdgeEntity != firstHalfEdgeEntity && roomVertexIndex < halfEdgeCount);

                        var triangulationIndices = new NativeArray<int>(Helpers.GetIndexCount(halfEdgeCount), Allocator.TempJob);

                        Helpers.Triangulate(roomVertices, triangulationIndices, out var triangulationIndexCount);

                        if (renderFloor)
                        {
                            for (var i = 0; i < triangulationIndexCount; ++i)
                            {
                                roomMeshData.Indices[roomMeshData.IndexOffset++] = firstFloorOffset + triangulationIndices[i] * floorCeilingStride;
                            }
                        }

                        if (renderCeiling)
                        {
                            for (var i = triangulationIndexCount - 1; i >= 0; --i)
                            {
                                roomMeshData.Indices[roomMeshData.IndexOffset++] = firstCeilingOffset + triangulationIndices[i] * floorCeilingStride;
                            }
                        }

                        if (hasFloor && EntityManager.HasComponent<RenderMesh>(room.Floor))
                        {
                            floorCeilingMeshData.IndexOffset = 0;
                            floorCeilingMeshData.VertexOffset = 0;

                            for (var i = 0; i < halfEdgeCount; ++i)
                            {
                                var pos = roomVertices[i];

                                floorCeilingMeshData.AddVertex(
                                    VertexToFloat3(in floor.Plane, new Vertex { X = pos.x, Z = pos.y }),
                                    floor.Plane.Normal, pos);
                            }

                            for (var i = 0; i < triangulationIndexCount; ++i)
                            {
                                floorCeilingMeshData.Indices[floorCeilingMeshData.IndexOffset++] = triangulationIndices[i];
                            }

                            floorCeilingMeshData.CopyToMesh(EntityManager.GetSharedComponentData<RenderMesh>(room.Floor).mesh);
                        }

                        // Don't need to update the ceiling mesh if the room above would do it (as a floor)
                        if (hasCeiling && EntityManager.HasComponent<RenderMesh>(room.Ceiling) && ceiling.Above == Entity.Null)
                        {
                            floorCeilingMeshData.IndexOffset = 0;
                            floorCeilingMeshData.VertexOffset = 0;

                            for (var i = 0; i < halfEdgeCount; ++i)
                            {
                                var pos = roomVertices[i];

                                floorCeilingMeshData.AddVertex(
                                    VertexToFloat3(in ceiling.Plane, new Vertex { X = pos.x, Z = pos.y }),
                                    ceiling.Plane.Normal, pos);
                            }

                            for (var i = 0; i < triangulationIndexCount; ++i)
                            {
                                floorCeilingMeshData.Indices[floorCeilingMeshData.IndexOffset++] = triangulationIndices[i];
                            }

                            floorCeilingMeshData.CopyToMesh(EntityManager.GetSharedComponentData<RenderMesh>(room.Ceiling).mesh);
                        }

                        triangulationIndices.Dispose();
                        roomVertices.Dispose();
                    }

                    roomMeshData.CopyToMesh(EntityManager.GetSharedComponentData<RenderMesh>(roomEntity).mesh);
                }

                roomMeshData.Dispose();
                floorCeilingMeshData.Dispose();
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
                    var withinLevel = EntityManager.GetWithinLevel(level);

                    _roomsQuery.SetSharedComponentFilter(withinLevel);

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

                    _floorCeilingsQuery.SetSharedComponentFilter(withinLevel);

                    var floorCeilings = _floorCeilingsQuery.ToEntityArray(Allocator.TempJob);
                    localToWorlds = _floorCeilingsQuery.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);
                    renderBoundses = _floorCeilingsQuery.ToComponentDataArray<RenderBounds>(Allocator.TempJob);

                    for (var i = 0; i < floorCeilings.Length; ++i)
                    {
                        var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(floorCeilings[i]);

                        localToWorlds[i] = levelLocalToWorld;
                        renderBoundses[i] = new RenderBounds
                        {
                            Value = new AABB
                            {
                                Center = renderMesh.mesh?.bounds.center ?? float3.zero,
                                Extents = renderMesh.mesh?.bounds.extents ?? float3.zero
                            }
                        };
                    }

                    _floorCeilingsQuery.CopyFromComponentDataArray(localToWorlds);
                    _floorCeilingsQuery.CopyFromComponentDataArray(renderBoundses);

                    floorCeilings.Dispose();
                    localToWorlds.Dispose();
                    renderBoundses.Dispose();
                });

        }
    }
}
