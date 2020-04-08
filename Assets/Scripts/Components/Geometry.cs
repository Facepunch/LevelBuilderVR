using Unity.Entities;

namespace LevelBuilderVR
{
    public struct Level : IComponentData
    {

    }

    public struct WithinLevel : ISharedComponentData
    {
        public Entity Level;
    }

    public struct Room : IComponentData
    {

    }

    public struct WithinRoom : ISharedComponentData
    {
        public Entity Room;
    }

    public struct FlatFloor : IComponentData
    {
        public float Y;
    }

    public struct FlatCeiling : IComponentData
    {
        public float Y;
    }

    public struct SlopeAnchor
    {
        public Entity Corner;
        public float Y;
    }

    public struct SlopedFloor : IComponentData
    {
        public SlopeAnchor Anchor0;
        public SlopeAnchor Anchor1;
        public SlopeAnchor Anchor2;
    }

    public struct SlopedCeiling : IComponentData
    {
        public SlopeAnchor Anchor0;
        public SlopeAnchor Anchor1;
        public SlopeAnchor Anchor2;
    }

    public struct Corner : IComponentData
    {
        public float X;
        public float Z;
    }

    public struct WallAnchor
    {
        public Entity Corner;
        public float MinY;
        public float MaxY;
    }

    public struct Wall : IComponentData
    {
        public float Offset;
        public WallAnchor Anchor0;
        public WallAnchor Anchor1;
    }

    public struct Selected : IComponentData
    {

    }
}
