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

    public interface IFlatFace
    {
        float Y { get; }
    }

    public struct Plane
    {
        public float3 Point;
        public float3 Normal;
    }

    public struct FlatFloor : IComponentData, IFlatFace
    {
        public float Y;
        float IFlatFace.Y => Y;
    }

    public struct FlatCeiling : IComponentData, IFlatFace
    {
        public float Y;
        float IFlatFace.Y => Y;
    }

    public struct SlopeVertex
    {
        public Entity Vertex;
        public float Y;
    }

    public interface ISlopedFace
    {
        SlopeVertex Anchor0 { get; }
        SlopeVertex Anchor1 { get; }
        SlopeVertex Anchor2 { get; }
    }

    public struct SlopedFloor : IComponentData, ISlopedFace
    {
        public SlopeVertex Anchor0;
        public SlopeVertex Anchor1;
        public SlopeVertex Anchor2;

        SlopeVertex ISlopedFace.Anchor0 => Anchor0;
        SlopeVertex ISlopedFace.Anchor1 => Anchor1;
        SlopeVertex ISlopedFace.Anchor2 => Anchor2;
    }

    public struct SlopedCeiling : IComponentData, ISlopedFace
    {
        public SlopeVertex Anchor0;
        public SlopeVertex Anchor1;
        public SlopeVertex Anchor2;

        SlopeVertex ISlopedFace.Anchor0 => Anchor0;
        SlopeVertex ISlopedFace.Anchor1 => Anchor1;
        SlopeVertex ISlopedFace.Anchor2 => Anchor2;
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
}
