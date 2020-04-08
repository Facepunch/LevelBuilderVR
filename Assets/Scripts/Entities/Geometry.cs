using Unity.Entities;
using UnityEngine;

namespace LevelBuilderVR.Entities
{ 
    public static class Geometry
    {
        private static EntityArchetype _sLevelArchetype;
        private static EntityArchetype _sRoomArchetype;
        private static EntityArchetype _sWallArchetype;
        private static EntityArchetype _sCornerArchetype;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeScene()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            _sLevelArchetype = em.CreateArchetype(
                typeof(Level),
                typeof(WithinLevel));

            _sRoomArchetype = em.CreateArchetype(
                typeof(Room),
                typeof(WithinLevel));

            _sWallArchetype = em.CreateArchetype(
                typeof(Wall),
                typeof(WithinLevel),
                typeof(WithinRoom));

            _sCornerArchetype = em.CreateArchetype(
                typeof(Corner),
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

            var corner0 = em.CreateCorner(level, -4f, -6f);
            var corner1 = em.CreateCorner(level, 4f, -6f);
            var corner2 = em.CreateCorner(level, -4f, 6f);
            var corner3 = em.CreateCorner(level, 4f, 6f);

            var room = em.CreateRoom(level, 0f, 3f);

            em.CreateWall(room, corner0, corner1);
            em.CreateWall(room, corner1, corner3);
            em.CreateWall(room, corner3, corner2);
            em.CreateWall(room, corner2, corner0);

            return level;
        }

        public static Entity CreateLevel(this EntityManager em)
        {
            var level = em.CreateEntity(_sLevelArchetype);

            em.SetSharedComponentData(level, new WithinLevel
            {
                Level = level
            });

            return level;
        }

        public static Entity CreateRoom(this EntityManager em, Entity level, float? floor = null, float? ceiling = null)
        {
            var room = em.CreateEntity(_sRoomArchetype);

            em.SetSharedComponentData(room, new WithinLevel
            {
                Level = level
            });

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

        public static Entity CreateWall(this EntityManager em, Entity room, Entity corner0, Entity corner1, float offset = 1f / 8f, float? bottom = null, float? top = null)
        {
            var wall = em.CreateEntity(_sWallArchetype);

            var withinLevelData = em.GetSharedComponentData<WithinLevel>(room);

            em.SetSharedComponentData(wall, new WithinLevel
            {
                Level = withinLevelData.Level
            });

            em.SetSharedComponentData(wall, new WithinRoom
            {
                Room = room
            });

            em.SetComponentData(wall, new Wall
            {
                Offset = offset,
                Anchor0 = new WallAnchor
                {
                    Corner = corner0,
                    MinY = bottom ?? float.NegativeInfinity,
                    MaxY = top ?? float.PositiveInfinity
                },
                Anchor1 = new WallAnchor
                {
                    Corner = corner1,
                    MinY = bottom ?? float.NegativeInfinity,
                    MaxY = top ?? float.PositiveInfinity
                }
            });

            return wall;
        }

        public static Entity CreateCorner(this EntityManager em, Entity level, float x, float z)
        {
            var corner = em.CreateEntity(_sCornerArchetype);

            em.SetSharedComponentData(corner, new WithinLevel
            {
                Level = level
            });

            em.SetComponentData(corner, new Corner
            {
                X = x,
                Z = z
            });

            return corner;
        }
    }
}
