using System;
using JetBrains.Annotations;
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
    }

    public struct Room : IComponentData
    {

    }

    public enum FaceKind
    {
        None,
        Floor,
        Ceiling
    }

    public interface IFlatFace
    {
        float Y { get; set; }
    }

    public struct Plane
    {
        public float3 Point;
        public float3 Normal;

        [Pure]
        public float3 GetClosestPoint(float3 pos)
        {
            return pos - math.dot(pos - Point, Normal) * Normal;
        }
    }

    public struct FlatFloor : IComponentData, IFlatFace
    {
        public float Y;
        float IFlatFace.Y
        {
            get => Y;
            set => Y = value;
        }
    }

    public struct FlatCeiling : IComponentData, IFlatFace
    {
        public float Y;
        float IFlatFace.Y
        {
            get => Y;
            set => Y = value;
        }
    }

    public struct SlopeVertex
    {
        // TODO: new format without referencing vertices
        public Entity Vertex;
        public float Y;
    }

    public interface ISlopedFace
    {
        SlopeVertex Anchor0 { get; set; }
        SlopeVertex Anchor1 { get; set; }
        SlopeVertex Anchor2 { get; set; }
    }

    public struct SlopedFloor : IComponentData, ISlopedFace
    {
        public SlopeVertex Anchor0;
        public SlopeVertex Anchor1;
        public SlopeVertex Anchor2;

        SlopeVertex ISlopedFace.Anchor0
        {
            get => Anchor0;
            set => Anchor0 = value;
        }

        SlopeVertex ISlopedFace.Anchor1
        {
            get => Anchor1;
            set => Anchor1 = value;
        }

        SlopeVertex ISlopedFace.Anchor2
        {
            get => Anchor2;
            set => Anchor2 = value;
        }
    }

    public struct SlopedCeiling : IComponentData, ISlopedFace
    {
        public SlopeVertex Anchor0;
        public SlopeVertex Anchor1;
        public SlopeVertex Anchor2;

        SlopeVertex ISlopedFace.Anchor0
        {
            get => Anchor0;
            set => Anchor0 = value;
        }

        SlopeVertex ISlopedFace.Anchor1
        {
            get => Anchor1;
            set => Anchor1 = value;
        }

        SlopeVertex ISlopedFace.Anchor2
        {
            get => Anchor2;
            set => Anchor2 = value;
        }
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

    public struct MergeOverlappingVertices : IComponentData
    {

    }

    public struct AssignToNewRoom : IComponentData
    {
        public Entity OldRoom;
        public Entity NewRoom;
    }
}
