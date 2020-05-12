using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace LevelBuilderVR.Entities
{
    public static partial class EntityHelper
    {
        private static EntityArchetype _sLevelArchetype;
        private static EntityArchetype _sRoomArchetype;
        private static EntityArchetype _sFloorCeilingArchetype;
        private static EntityArchetype _sHalfEdgeArchetype;
        private static EntityArchetype _sVertexArchetype;

        private static EntityQuery _sSelectedQuery;

        private static EntityQuery _sRoomsQuery;
        private static EntityQuery _sFloorCeilingsQuery;
        private static EntityQuery _sHalfEdgesQuery;
        private static EntityQuery _sVerticesQuery;

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

            _sFloorCeilingArchetype = em.CreateArchetype(
                typeof(Identifier),
                typeof(FloorCeiling),
                typeof(WithinLevel));

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
                    All = new[] { ComponentType.ReadOnly<Selected>() }
                });

            _sRoomsQuery = em.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<Room>(), ComponentType.ReadOnly<WithinLevel>() }
                });

            _sFloorCeilingsQuery = em.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<FloorCeiling>(), ComponentType.ReadOnly<WithinLevel>() }
                });

            _sHalfEdgesQuery = em.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<HalfEdge>(), ComponentType.ReadOnly<WithinLevel>() }
                });

            _sVerticesQuery = em.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<Vertex>(), ComponentType.ReadOnly<WithinLevel>() },
                    None = new[] { ComponentType.ReadOnly<Virtual>() }
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

        public static void SetWithinLevel(this EntityManager em, Entity entity, Entity level)
        {
            em.SetSharedComponentData(entity, em.GetWithinLevel(level));
        }

        public static WithinLevel GetWithinLevel(this EntityManager em, Entity level)
        {
            return new WithinLevel(em.GetComponentData<Identifier>(level));
        }

        public static Entity CreateLevel(this EntityManager em, Guid? guid = null)
        {
            var level = em.CreateEntity(_sLevelArchetype);

            if (guid == null)
            {
                em.AssignNewIdentifier(level);
            }
            else
            {
                em.SetComponentData(level, new Identifier(guid.Value));
            }

            em.SetComponentData(level, new LocalToWorld
            {
                Value = float4x4.identity
            });

            em.SetComponentData(level, new WorldToLocal
            {
                Value = float4x4.identity
            });

            em.SetWithinLevel(level, level);

            return level;
        }

        public static Entity CreateFloorCeiling(this EntityManager em, Entity level, float y, Entity? above = null, Entity? below = null, Guid? guid = null)
        {
            return em.CreateFloorCeiling(level, new Plane
            {
                Point = new float3(0f, y, 0f),
                Normal = new float3(0f, 1f, 0f)
            }, above, below, guid);
        }

        public static Entity CreateFloorCeiling(this EntityManager em, Entity level, Plane plane, Entity? above = null, Entity? below = null, Guid? guid = null)
        {
            return em.CreateFloorCeiling(em.GetWithinLevel(level), plane, above, below, guid);
        }

        private static Entity CreateFloorCeiling(this EntityManager em, WithinLevel withinLevel, Plane plane, Entity? above = null, Entity? below = null, Guid? guid = null)
        {
            var floorCeiling = em.CreateEntity(_sFloorCeilingArchetype);

            if (guid == null)
            {
                em.AssignNewIdentifier(floorCeiling);
            }
            else
            {
                em.SetComponentData(floorCeiling, new Identifier(guid.Value));
            }

            em.SetSharedComponentData(floorCeiling, withinLevel);

            em.SetComponentData(floorCeiling, new FloorCeiling
            {
                Plane = plane,
                Above = above ?? Entity.Null,
                Below = below ?? Entity.Null
            });

            return floorCeiling;
        }

        public static Entity CreateRoom(this EntityManager em, Entity level, float? floor = null, float? ceiling = null, Guid? guid = null)
        {
            var room = em.CreateEntity(_sRoomArchetype);

            if (guid == null)
            {
                em.AssignNewIdentifier(room);
            }
            else
            {
                em.SetComponentData(room, new Identifier(guid.Value));
            }

            em.SetWithinLevel(room, level);

            var roomData = new Room();

            if (floor.HasValue)
            {
                roomData.Floor = em.CreateFloorCeiling(level, floor.Value, above: room);
            }

            if (ceiling.HasValue)
            {
                roomData.Ceiling = em.CreateFloorCeiling(level, ceiling.Value, below: room);
            }

            em.SetComponentData(room, roomData);

            em.SetupRoomRendering(room);

            return room;
        }

        public static Entity CopyRoom(this EntityManager em, Entity oldRoom)
        {
            var room = em.CreateEntity(_sRoomArchetype);
            var withinLevel = em.GetSharedComponentData<WithinLevel>(oldRoom);

            em.AssignNewIdentifier(room);

            em.SetSharedComponentData(room, withinLevel);

            var oldRoomData = em.GetComponentData<Room>(oldRoom);
            var roomData = new Room();

            if (oldRoomData.Floor != Entity.Null)
            {
                var floorData = em.GetComponentData<FloorCeiling>(oldRoomData.Floor);
                roomData.Floor = em.CreateFloorCeiling(withinLevel, floorData.Plane, above: room);
            }

            if (oldRoomData.Ceiling != Entity.Null)
            {
                var ceilingData = em.GetComponentData<FloorCeiling>(oldRoomData.Ceiling);
                roomData.Ceiling = em.CreateFloorCeiling(withinLevel, ceilingData.Plane, below: room);
            }

            em.SetComponentData(room, roomData);

            em.SetupRoomRendering(room);

            return room;
        }

        public static Entity CreateHalfEdge(this EntityManager em, Entity room, Entity vertex, Guid? guid = null)
        {
            var halfEdge = em.CreateEntity(_sHalfEdgeArchetype);

            if (guid == null)
            {
                em.AssignNewIdentifier(halfEdge);
            }
            else
            {
                em.SetComponentData(halfEdge, new Identifier(guid.Value));
            }

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
            var hePrev = em.GetComponentData<HalfEdge>(prev);
            var entNew = em.CreateHalfEdge(hePrev.Room, vertex);

            var entNext = hePrev.Next;
            hePrev.Next = entNew;

            em.SetComponentData(entNew, new HalfEdge
            {
                Room = hePrev.Room,
                Vertex = vertex,
                Next = entNext,
                BackFace = hePrev.BackFace
            });

            if (hePrev.BackFace == Entity.Null)
            {
                em.SetComponentData(prev, hePrev);
                return entNew;
            }

            // Back face

            var entBackPrev = hePrev.BackFace;
            var heBackPrev = em.GetComponentData<HalfEdge>(entBackPrev);
            var entBackNew = em.CreateHalfEdge(heBackPrev.Room, vertex);

            var entBackNext = heBackPrev.Next;
            heBackPrev.Next = entBackNew;
            heBackPrev.BackFace = entNew;
            hePrev.BackFace = entBackNew;

            em.SetComponentData(prev, hePrev);
            em.SetComponentData(entBackPrev, heBackPrev);

            em.SetComponentData(entBackNew, new HalfEdge
            {
                Room = heBackPrev.Room,
                Vertex = vertex,
                Next = entBackNext,
                BackFace = prev
            });

            return entNew;
        }

        public static Entity CreateVertex(this EntityManager em, Entity level, float x, float z, Guid? guid = null)
        {
            var vertex = em.CreateEntity(_sVertexArchetype);

            if (guid == null)
            {
                em.AssignNewIdentifier(vertex);
            }
            else
            {
                em.SetComponentData(vertex, new Identifier(guid.Value));
            }

            em.SetWithinLevel(vertex, level);

            em.SetComponentData(vertex, new Vertex
            {
                X = x,
                Z = z
            });

            return vertex;
        }
    }
}
