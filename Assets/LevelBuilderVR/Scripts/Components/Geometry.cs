using System;
using Unity.Entities;
using Unity.Mathematics;

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
        public uint Revision;
    }

    public struct WithinLevel : ISharedComponentData, IEquatable<WithinLevel>
    {
        public readonly Guid LevelGuid;

        public WithinLevel(Identifier levelIdent)
        {
            LevelGuid = levelIdent.Guid;
        }

        public bool Equals(WithinLevel other)
        {
            return LevelGuid.Equals(other.LevelGuid);
        }

        public override bool Equals(object obj)
        {
            return obj is WithinLevel other && Equals(other);
        }

        public override int GetHashCode()
        {
            return LevelGuid.GetHashCode();
        }

        public static bool operator ==(WithinLevel left, WithinLevel right)
        {
            return left.LevelGuid.Equals(right.LevelGuid);
        }

        public static bool operator !=(WithinLevel left, WithinLevel right)
        {
            return !left.LevelGuid.Equals(right.LevelGuid);
        }
    }

    public struct Room : IComponentData
    {
        public Entity Floor;
        public Entity Ceiling;
    }

    public struct FloorCeiling : IComponentData
    {
        public Plane Plane;
        public Entity Above;
        public Entity Below;
    }

    public struct Vertex : IComponentData
    {
        public float X;
        public float Z;

        public float MinY;
        public float MaxY;
    }

    public struct HalfEdge : IComponentData
    {
        public Entity Room;

        /// <summary>
        /// Left vertex of the edge.
        /// </summary>
        public Entity Vertex;

        /// <summary>
        /// Next <see cref="HalfEdge"/>, on the right of this one.
        /// </summary>
        public Entity Next;

        /// <summary>
        /// Complementary <see cref="HalfEdge"/> going in the opposite
        /// direction (can be Null).
        /// </summary>
        public Entity BackFace;

        public Entity Above;
        public Entity Below;

        public float MinY;
        public float MaxY;
    }

    public struct DirtyMesh : IComponentData
    {

    }

    public struct DirtyMaterial : IComponentData
    {

    }

    public struct WidgetsVisible : IComponentData
    {
        public bool Vertex;
        public bool FloorCeiling;
    }

    public struct Selected : IComponentData
    {

    }

    public struct Hovered : IComponentData
    {

    }

    public struct Virtual : IComponentData
    {

    }

    public struct Hidden : IComponentData
    {

    }

    public struct Move : IComponentData
    {
        public float3 Offset;
    }

    public struct MergeOverlappingVertices : IComponentData
    {

    }
}
