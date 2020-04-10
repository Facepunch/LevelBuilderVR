using System;
using Unity.Entities;

namespace LevelBuilderVR
{
    public struct Identifier : IComponentData
    {
        public readonly Guid Guid;

        public Identifier(Guid guid)
        {
            Guid = guid;
        }
    }

    public struct Level : IComponentData
    {

    }

    public struct WithinLevel : ISharedComponentData, IEquatable<WithinLevel>
    {
        public readonly Entity Level;

        public WithinLevel(Entity level)
        {
            Level = level;
        }

        public bool Equals(WithinLevel other)
        {
            return Level.Equals(other.Level);
        }

        public override bool Equals(object obj)
        {
            return obj is WithinLevel other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Level.GetHashCode();
        }
    }

    public struct Room : IComponentData
    {

    }

    public struct WithinRoom : ISharedComponentData, IEquatable<WithinRoom>
    {
        public readonly Entity Room;

        public WithinRoom(Entity room)
        {
            Room = room;
        }

        public bool Equals(WithinRoom other)
        {
            return Room.Equals(other.Room);
        }

        public override bool Equals(object obj)
        {
            return obj is WithinRoom other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Room.GetHashCode();
        }
    }

    public struct FlatFloor : IComponentData
    {
        public float Y;
    }

    public struct FlatCeiling : IComponentData
    {
        public float Y;
    }

    public struct SlopeVertex
    {
        public Entity Vertex;
        public float Y;
    }

    public struct SlopedFloor : IComponentData
    {
        public SlopeVertex Anchor0;
        public SlopeVertex Anchor1;
        public SlopeVertex Anchor2;
    }

    public struct SlopedCeiling : IComponentData
    {
        public SlopeVertex Anchor0;
        public SlopeVertex Anchor1;
        public SlopeVertex Anchor2;
    }

    public struct Vertex : IComponentData
    {
        public float X;
        public float Z;
    }

    public struct HalfEdge : IComponentData
    {
        /// <summary>
        /// Left vertex of the edge.
        /// </summary>
        public Entity Vertex0;

        /// <summary>
        /// Right vertex of the edge.
        /// </summary>
        public Entity Vertex1;
    }

    public struct Selected : IComponentData
    {

    }
}
