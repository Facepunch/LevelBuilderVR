using System;
using Unity.Entities;
using UnityEngine;

namespace LevelBuilderVR.Entities
{ 
    public static class Geometry
    {
        private static EntityArchetype _sLevelArchetype;
        private static EntityArchetype _sRoomArchetype;
        private static EntityArchetype _sHalfEdgeArchetype;
        private static EntityArchetype _sVertexArchetype;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeScene()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            _sLevelArchetype = em.CreateArchetype(
                typeof(Identifier),
                typeof(Level),
                typeof(WithinLevel));

            _sRoomArchetype = em.CreateArchetype(
                typeof(Identifier),
                typeof(Room),
                typeof(WithinLevel));

            _sHalfEdgeArchetype = em.CreateArchetype(
                typeof(Identifier),
                typeof(HalfEdge),
                typeof(WithinLevel),
                typeof(WithinRoom));

            _sVertexArchetype = em.CreateArchetype(
                typeof(Identifier),
                typeof(Vertex),
                typeof(WithinLevel));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterScene()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            em.CreateLevelTemplate();
        }

        public static Entity CreateLevelTemplate(this EntityManager em)
        {
            var level = em.CreateLevel();

            var corner0 = em.CreateVertex(level, -4f, -6f);
            var corner1 = em.CreateVertex(level, 4f, -6f);
            var corner2 = em.CreateVertex(level, -4f, 6f);
            var corner3 = em.CreateVertex(level, 4f, 6f);

            var room = em.CreateRoom(level, 0f, 3f);

            em.CreateHalfEdge(room, corner0, corner1);
            em.CreateHalfEdge(room, corner1, corner3);
            em.CreateHalfEdge(room, corner3, corner2);
            em.CreateHalfEdge(room, corner2, corner0);

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

            return room;
        }

        public static Entity CreateHalfEdge(this EntityManager em, Entity room, Entity vertex0, Entity vertex1)
        {
            var halfEdge = em.CreateEntity(_sHalfEdgeArchetype);

            em.AssignNewIdentifier(halfEdge);

            var withinLevelData = em.GetSharedComponentData<WithinLevel>(room);

            em.SetSharedComponentData(halfEdge, withinLevelData);

            em.SetSharedComponentData(halfEdge, new WithinRoom(room));

            em.SetComponentData(halfEdge, new HalfEdge
            {
                Vertex0 = vertex0,
                Vertex1 = vertex1
            });

            return halfEdge;
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
    }
}
