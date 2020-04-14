using LevelBuilderVR.Behaviours;
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

        private static readonly int[] _sEmptyIndices = new int[0];

        private EntityQuery _changedRoomsQuery;
        private EntityQuery _halfEdgesQuery;

        private static HybridLevel _sHybridLevel;

        private static HybridLevel HybridLevel => _sHybridLevel ?? (_sHybridLevel = Object.FindObjectOfType<HybridLevel>());

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
            var getVertexWritable = GetComponentDataFromEntity<Vertex>(false);
            var getHalfEdge = GetComponentDataFromEntity<HalfEdge>(true);
            var getHalfEdgeWritable = GetComponentDataFromEntity<HalfEdge>(false);

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

                        if (hasFloor && hasCeiling)
                        {
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

                            var floorHeight = vertices[wall0].y;
                            var ceilingHeight = vertices[wall2].y;

                            halfEdge.MinY = math.min(floorHeight, ceilingHeight);
                            halfEdge.MaxY = math.max(floorHeight, ceilingHeight);
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

                        var firstFloorOffset = vertexOffset;
                        var firstCeilingOffset = vertexOffset + (hasFloor ? 1 : 0);
                        var floorCeilingStride = (hasFloor ? 1 : 0) + (hasCeiling ? 1 : 0);

                        var curHalfEdgeEntity = firstHalfEdgeEntity;
                        do
                        {
                            var halfEdge = getHalfEdge[curHalfEdgeEntity];
                            var vertex = getVertex[halfEdge.Vertex];

                            int floorIndex = -1, ceilingIndex = -1;

                            if (hasFloor)
                            {
                                vertices[floorIndex = vertexOffset++] = VertexToFloat3(in floor, vertex);
                                normals[floorIndex] = floor.Normal;
                                uvs[floorIndex] = new float2(vertex.X, vertex.Z);
                            }

                            if (hasCeiling)
                            {
                                vertices[ceilingIndex = vertexOffset++] = VertexToFloat3(in ceiling, vertex);
                                normals[ceilingIndex] = -ceiling.Normal;
                                uvs[ceilingIndex] = new float2(vertex.X, vertex.Z);
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
                                indices[indexOffset++] = firstFloorOffset + triangulationIndices[i] * floorCeilingStride;
                            }
                        }

                        if (hasCeiling)
                        {
                            for (var i = triangulationIndexCount - 1; i >= 0; --i)
                            {
                                indices[indexOffset++] = firstCeilingOffset + triangulationIndices[i] * floorCeilingStride;
                            }
                        }

                        triangulationIndices.Dispose();
                        roomVertices.Dispose();
                    }

                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(roomEntity);

                    renderMesh.mesh.SetIndices(_sEmptyIndices, MeshTopology.Triangles, 0);
                    renderMesh.mesh.SetVertices(vertices, 0, vertexOffset);
                    renderMesh.mesh.SetNormals(normals, 0, vertexOffset);
                    renderMesh.mesh.SetUVs(0, uvs, 0, vertexOffset);
                    renderMesh.mesh.SetIndices(indices, 0, indexOffset, MeshTopology.Triangles, 0, calculateBounds: false);
                }

                vertices.Dispose();
                normals.Dispose();
                uvs.Dispose();
                indices.Dispose();
            }

            var getLocalToWorld = GetComponentDataFromEntity<LocalToWorld>();

            Entities
                .WithAllReadOnly<Vertex, Hovered, DirtyMaterial>()
                .WithNone<Selected>()
                .WithAll<RenderMesh>()
                .ForEach(entity =>
                {
                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(entity);

                    renderMesh.material = HybridLevel.VertexWidgetHoverMaterial;

                    PostUpdateCommands.SetSharedComponent(entity, renderMesh);
                    PostUpdateCommands.RemoveComponent<DirtyMaterial>(entity);
                });

            Entities
                .WithAllReadOnly<Vertex, Selected, DirtyMaterial>()
                .WithNone<Hovered>()
                .WithAll<RenderMesh>()
                .ForEach(entity =>
                {
                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(entity);

                    renderMesh.material = HybridLevel.VertexWidgetSelectedMaterial;

                    PostUpdateCommands.SetSharedComponent(entity, renderMesh);
                    PostUpdateCommands.RemoveComponent<DirtyMaterial>(entity);
                });

            Entities
                .WithAllReadOnly<Vertex, Hovered, Selected, DirtyMaterial>()
                .WithAll<RenderMesh>()
                .ForEach(entity =>
                {
                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(entity);

                    renderMesh.material = HybridLevel.VertexWidgetHoverSelectedMaterial;

                    PostUpdateCommands.SetSharedComponent(entity, renderMesh);
                    PostUpdateCommands.RemoveComponent<DirtyMaterial>(entity);
                });

            Entities
                .WithAllReadOnly<Vertex, DirtyMaterial>()
                .WithNone<Hovered, Selected>()
                .WithAll<RenderMesh>()
                .ForEach(entity =>
                {
                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(entity);

                    renderMesh.material = HybridLevel.VertexWidgetBaseMaterial;

                    PostUpdateCommands.SetSharedComponent(entity, renderMesh);
                    PostUpdateCommands.RemoveComponent<DirtyMaterial>(entity);
                });

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

            Entities
                .WithAllReadOnly<Vertex, WithinLevel>()
                .WithAll<LocalToWorld, RenderBounds>()
                .ForEach((Entity entity, ref Vertex vertex, ref LocalToWorld localToWorld, ref RenderBounds renderBounds) =>
                {
                    var withinLevel = EntityManager.GetSharedComponentData<WithinLevel>(entity);

                    var translation = new float3(vertex.X, (vertex.MinY + vertex.MaxY) * 0.5f, vertex.Z);
                    var scale = new float3(1f, (vertex.MaxY - vertex.MinY) * 0.5f, 1f);

                    var levelTransform = getLocalToWorld[withinLevel.Level].Value;
                    var localTransform = float4x4.TRS(translation, quaternion.identity, scale);
                    var finalTransform = math.mul(levelTransform, localTransform);

                    localToWorld.Value = finalTransform;

                    renderBounds.Value = new AABB
                    {
                        Center = float3.zero,
                        Extents = scale
                    };
                });
        }
    }
}
