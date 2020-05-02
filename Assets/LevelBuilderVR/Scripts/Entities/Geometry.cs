﻿using System;
using LevelBuilderVR.Behaviours;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LevelBuilderVR.Entities
{ 
    public static class Geometry
    {
        private static EntityArchetype _sLevelArchetype;
        private static EntityArchetype _sRoomArchetype;
        private static EntityArchetype _sHalfEdgeArchetype;
        private static EntityArchetype _sVertexArchetype;

        private static HybridLevel _sHybridLevel;
        private static EntityQuery _sSelectedQuery;

        private static EntityQuery _sHalfEdgesQuery;
        private static EntityQuery _sVerticesQuery;

        private static HybridLevel HybridLevel => _sHybridLevel ?? (_sHybridLevel = Object.FindObjectOfType<HybridLevel>());

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeScene()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            _sLevelArchetype = em.CreateArchetype(
                typeof(Identifier),
                typeof(Level),
                typeof(WithinLevel),
                typeof(WidgetsVisible),
                typeof(LocalToWorld),
                typeof(WorldToLocal));

            _sRoomArchetype = em.CreateArchetype(
                typeof(Identifier),
                typeof(Room),
                typeof(WithinLevel),
                typeof(RenderMesh),
                typeof(LocalToWorld),
                typeof(RenderBounds));

            _sHalfEdgeArchetype = em.CreateArchetype(
                typeof(Identifier),
                typeof(HalfEdge),
                typeof(WithinLevel));

            _sVertexArchetype = em.CreateArchetype(
                typeof(Identifier),
                typeof(Vertex),
                typeof(WithinLevel));

            _sSelectedQuery = em.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] {ComponentType.ReadOnly<Selected>()}
                });

            _sHalfEdgesQuery = em.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<HalfEdge>(), ComponentType.ReadOnly<WithinLevel>() }
                });

            _sVerticesQuery = em.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] {ComponentType.ReadOnly<Vertex>(), ComponentType.ReadOnly<WithinLevel>() },
                    None = new [] {ComponentType.ReadOnly<Virtual>() }
                });
        }

        public static Entity CreateLevelTemplate(this EntityManager em, float3 size)
        {
            var level = em.CreateLevel();

            var room = em.CreateRoom(level, 0f, size.y);

            var halfWidth = size.x * 0.5f;
            var halfDepth = size.z * 0.5f;

            var prev = em.CreateHalfEdge(room, em.CreateVertex(level, -halfWidth, -halfDepth));
            prev = em.InsertHalfEdge(prev, em.CreateVertex(level, -halfWidth, halfDepth));
            prev = em.InsertHalfEdge(prev, em.CreateVertex(level, halfWidth, halfDepth));
            prev = em.InsertHalfEdge(prev, em.CreateVertex(level, halfWidth, -halfDepth));

            return level;
        }

        private static void AssignNewIdentifier(this EntityManager em, Entity entity)
        {
            em.SetComponentData(entity, new Identifier(Guid.NewGuid()));
        }

        public static Entity CreateLevel(this EntityManager em)
        {
            var level = em.CreateEntity(_sLevelArchetype);

            em.AssignNewIdentifier(level);

            em.SetComponentData(level, new LocalToWorld
            {
                Value = float4x4.identity
            });

            em.SetComponentData(level, new WorldToLocal
            {
                Value = float4x4.identity
            });

            em.SetSharedComponentData(level, new WithinLevel(level));

            return level;
        }

        public static Entity CreateRoom(this EntityManager em, Entity level, float? floor = null, float? ceiling = null)
        {
            var room = em.CreateEntity(_sRoomArchetype);

            em.AssignNewIdentifier(room);

            em.SetSharedComponentData(room, new WithinLevel(level));

            if (floor.HasValue)
            {
                em.AddComponentData(room, new FlatFloor
                {
                    Y = floor.Value
                });
            }

            if (ceiling.HasValue)
            {
                em.AddComponentData(room, new FlatCeiling
                {
                    Y = ceiling.Value
                });
            }

            // Rendering

            em.SetComponentData(room, new LocalToWorld
            {
                Value = float4x4.identity
            });

            var mesh = new Mesh();

            mesh.MarkDynamic();

            em.SetSharedComponentData(room, new RenderMesh
            {
                mesh = mesh,
                material = HybridLevel.Material,
                castShadows = ShadowCastingMode.Off,
                receiveShadows = true
            });

            em.AddComponent<DirtyMesh>(room);

            return room;
        }

        public static Entity CreateHalfEdge(this EntityManager em, Entity room, Entity vertex)
        {
            var halfEdge = em.CreateEntity(_sHalfEdgeArchetype);

            em.AssignNewIdentifier(halfEdge);

            var withinLevelData = em.GetSharedComponentData<WithinLevel>(room);

            em.SetSharedComponentData(halfEdge, withinLevelData);
            em.SetComponentData(halfEdge, new HalfEdge
            {
                Room = room,
                Vertex = vertex,
                Next = halfEdge
            });

            em.AddComponent<DirtyMesh>(room);

            return halfEdge;
        }

        public static Entity InsertHalfEdge(this EntityManager em, Entity prev, Entity vertex)
        {
            var prevHalfEdge = em.GetComponentData<HalfEdge>(prev);
            var halfEdgeEntity = em.CreateHalfEdge(prevHalfEdge.Room, vertex);

            var next = prevHalfEdge.Next;
            prevHalfEdge.Next = halfEdgeEntity;

            em.SetComponentData(prev, prevHalfEdge);
            em.SetComponentData(halfEdgeEntity, new HalfEdge
            {
                Room = prevHalfEdge.Room,
                Vertex = vertex,
                Next = next
            });

            return halfEdgeEntity;
        }

        public static Entity CreateVertex(this EntityManager em, Entity level, float x, float z)
        {
            var vertex = em.CreateEntity(_sVertexArchetype);

            em.AssignNewIdentifier(vertex);

            em.SetSharedComponentData(vertex, new WithinLevel(level));

            em.SetComponentData(vertex, new Vertex
            {
                X = x,
                Z = z
            });

            return vertex;
        }

        private static bool SetMaterialFlag<TFlag>(this EntityManager em, Entity entity, bool enabled)
            where TFlag : struct, IComponentData
        {
            var changed = false;

            if (enabled && !em.HasComponent<TFlag>(entity))
            {
                em.AddComponent<TFlag>(entity);
                changed = true;
            }
            else if (!enabled && em.HasComponent<TFlag>(entity))
            {
                em.RemoveComponent<TFlag>(entity);
                changed = true;
            }

            if (changed && em.HasComponent<RenderMesh>(entity))
            {
                em.AddComponent<DirtyMaterial>(entity);
            }

            return changed;
        }

        public static bool SetHovered(this EntityManager em, Entity entity, bool hovered)
        {
            return em.SetMaterialFlag<Hovered>(entity, hovered);
        }

        public static bool GetSelected(this EntityManager em, Entity entity)
        {
            return em.HasComponent<Selected>(entity);
        }

        public static bool SetSelected(this EntityManager em, Entity entity, bool selected)
        {
            return em.SetMaterialFlag<Selected>(entity, selected);
        }

        public static void DeselectAll(this EntityManager em)
        {
            em.AddComponent<DirtyMaterial>(_sSelectedQuery);
            em.RemoveComponent<Selected>(_sSelectedQuery);
        }

        public static void SetVisible(this EntityManager em, Entity entity, bool value)
        {
            if (value)
            {
                em.RemoveComponent<Hidden>(entity);
            }
            else
            {
                em.AddComponent<Hidden>(entity);
            }
        }

        public static bool FindClosestVertex(this EntityManager em, Entity level, float3 localPos,
            out Entity outEntity, out float3 outClosestPoint)
        {
            outEntity = Entity.Null;
            outClosestPoint = localPos;

            var closestDist2 = float.PositiveInfinity;

            _sVerticesQuery.SetSharedComponentFilter(new WithinLevel(level));
            using (var entities = _sVerticesQuery.ToEntityArray(Allocator.TempJob))
            {
                foreach (var entity in entities)
                {
                    var vertex = em.GetComponentData<Vertex>(entity);
                    var pos = new float3(vertex.X, math.clamp(localPos.y, vertex.MinY, vertex.MaxY), vertex.Z);
                    var dist2 = math.lengthsq(pos - localPos);

                    if (dist2 >= closestDist2)
                    {
                        continue;
                    }

                    closestDist2 = dist2;
                    outClosestPoint = pos;
                    outEntity = entity;
                }
            }

            return outEntity != Entity.Null;
        }

        public static bool FindClosestHalfEdge(this EntityManager em, Entity level, float3 localPos,
            out Entity outEntity, out float3 outClosestPoint, out Vertex outVirtualVertex)
        {
            const float epsilon = 1f / 65536f;

            outEntity = Entity.Null;
            outClosestPoint = localPos;
            outVirtualVertex = default;

            var closestDist2 = float.PositiveInfinity;
            var closestV = 0f;

            _sHalfEdgesQuery.SetSharedComponentFilter(new WithinLevel(level));
            using (var entities = _sHalfEdgesQuery.ToEntityArray(Allocator.TempJob))
            {
                foreach (var entity in entities)
                {
                    var halfEdge = em.GetComponentData<HalfEdge>(entity);
                    var nextHalfEdge = em.GetComponentData<HalfEdge>(halfEdge.Next);
                    var vertex0 = em.GetComponentData<Vertex>(halfEdge.Vertex);
                    var vertex1 = em.GetComponentData<Vertex>(nextHalfEdge.Vertex);

                    var p0 = new float3(vertex0.X, 0f, vertex0.Z);
                    var p1 = new float3(vertex1.X, 0f, vertex1.Z);
                    var length = math.length(p1 - p0);

                    if (length < epsilon)
                    {
                        continue;
                    }

                    var tangent = math.normalize(p1 - p0);
                    var normal = new float3(-tangent.y, 0f, tangent.x);

                    var diff = localPos - p0;

                    var u = math.dot(diff, tangent);
                    var v = math.dot(diff, normal);

                    var clampedU = math.clamp(u, 0f, length);
                    
                    var t = clampedU / length;
                    var minY = math.lerp(vertex0.MinY, vertex1.MinY, t);
                    var maxY = math.lerp(vertex0.MaxY, vertex1.MaxY, t);

                    var onEdgePos = p0 + math.clamp(u, 0f, length) * tangent;

                    onEdgePos.y = math.clamp(localPos.y, minY, maxY);

                    var dist2 = math.lengthsq(localPos - onEdgePos);

                    if (dist2 < closestDist2 || math.abs(dist2 - closestDist2) <= epsilon * epsilon && closestV < 0f && v > 0f)
                    {
                        closestDist2 = dist2;
                        closestV = v;

                        outClosestPoint = onEdgePos;
                        outEntity = entity;

                        outVirtualVertex = new Vertex
                        {
                            X = outClosestPoint.x,
                            Z = outClosestPoint.z,
                            MinY = minY,
                            MaxY = maxY
                        };
                    }
                }
            }

            return outEntity != Entity.Null;
        }

        public static bool DestroyEntities(this EntityManager em, TempEntitySet entities)
        {
            if (entities.Count == 0)
            {
                return false;
            }

            var toDestroy = new NativeArray<Entity>(entities.Count, Allocator.TempJob);

            for (var i = 0; i < entities.Count; ++i)
            {
                toDestroy[i] = entities[i];
            }

            em.DestroyEntity(toDestroy);

            toDestroy.Dispose();

            return true;
        }

        /// <summary>
        /// Find all <see cref="Entity"/> instances with a <see cref="Vertex"/>, that
        /// aren't referenced by any <see cref="HalfEdge"/>s.
        /// </summary>
        public static int GetUnreferencedVertices(this EntityManager em, Entity level, TempEntitySet outEntities)
        {
            outEntities.Clear();

            // Get all vertices

            _sVerticesQuery.SetSharedComponentFilter(new WithinLevel(level));

            outEntities.AddRange(_sVerticesQuery);

            // Remove vertices from vertexSet that are referenced

            _sHalfEdgesQuery.SetSharedComponentFilter(new WithinLevel(level));

            var halfEdges = _sHalfEdgesQuery.ToComponentDataArray<HalfEdge>(Allocator.TempJob);

            for (var i = 0; i < halfEdges.Length; ++i)
            {
                var halfEdge = halfEdges[i];
                outEntities.Remove(halfEdge.Vertex);
            }

            halfEdges.Dispose();

            return outEntities.Count;
        }
    }
}
