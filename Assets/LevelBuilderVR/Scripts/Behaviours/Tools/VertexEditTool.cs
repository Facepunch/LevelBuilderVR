using System.Collections.Generic;
using LevelBuilderVR.Entities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Valve.VR.InteractionSystem;

namespace LevelBuilderVR.Behaviours.Tools
{
    public class VertexEditTool : Tool
    {
        private Entity _leftHovered;
        private Entity _rightHovered;

        private bool _leftIsDragging;
        private bool _rightIsDragging;

        private float3 _leftDragOrigin;
        private float3 _rightDragOrigin;

        public float InteractRadius = 0.05f;
        public float GridSnap = 0.25f;

        public ushort HapticPulseDurationMicros = 500;

        private EntityQuery _getSelectedVertices;
        private EntityQuery _getSelectedVerticesWritable;
        private EntityQuery _getHalfEdges;

        private void Start()
        {
            _getSelectedVertices = EntityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<Selected>(), ComponentType.ReadOnly<Vertex>() }
                });

            _getSelectedVerticesWritable = EntityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<Selected>(), typeof(Vertex) }
                });

            _getHalfEdges = EntityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<HalfEdge>() }
                });
        }

        private bool UpdateHover(Hand hand, ref Entity hovered, out float3 localHandPos)
        {
            if (!hand.TryGetPointerPosition(out var handPos))
            {
                if (hovered != Entity.Null)
                {
                    EntityManager.SetHovered(hovered, false);
                    hovered = Entity.Null;
                }

                localHandPos = float3.zero;
                return false;
            }

            var localToWorld = EntityManager.GetComponentData<LocalToWorld>(Level).Value;
            var worldToLocal = EntityManager.GetComponentData<WorldToLocal>(Level).Value;
            localHandPos = math.transform(worldToLocal, handPos);

            if (EntityManager.FindClosestVertex(Level, localHandPos, out var newHovered, out var hoverPos))
            {
                var hoverWorldPos = math.transform(localToWorld, hoverPos);
                var dist2 = math.distancesq(hoverWorldPos, handPos);

                if (dist2 > InteractRadius * InteractRadius)
                {
                    newHovered = Entity.Null;
                }
            }

            if (hovered == newHovered)
            {
                return true;
            }

            hand.TriggerHapticPulse(HapticPulseDurationMicros);

            if (hovered != null)
            {
                EntityManager.SetHovered(hovered, false);
            }

            if (newHovered != Entity.Null)
            {
                EntityManager.SetHovered(newHovered, true);
            }

            hovered = newHovered;

            return true;
        }

        private bool UpdateInteract(Hand hand, float3 handPos, Entity hovered, ref bool isDragging, ref float3 dragOrigin)
        {
            if (UseToolAction.GetStateDown(hand.handType))
            {
                if (MultiSelectAction.GetState(hand.handType))
                {
                    if (hovered != Entity.Null)
                    {
                        EntityManager.SetSelected(hovered, !EntityManager.GetSelected(hovered));
                    }

                    isDragging = false;
                }
                else
                {
                    if (hovered == Entity.Null || !EntityManager.GetSelected(hovered))
                    {
                        EntityManager.DeselectAll();

                        if (hovered != Entity.Null)
                        {
                            EntityManager.SetSelected(hovered, true);
                        }
                    }

                    isDragging = true;
                    dragOrigin = handPos;
                }
            }
            else if (UseToolAction.GetState(hand.handType) && isDragging)
            {
                var offset = handPos - dragOrigin;

                var intOffset = new int3(math.round(offset / GridSnap))
                {
                    y = 0
                };


                if (math.lengthsq(intOffset) <= 0)
                {
                    return false;
                }

                offset = new float3(intOffset) * GridSnap;

                dragOrigin += offset;

                var allSelected = _getSelectedVerticesWritable.ToComponentDataArray<Vertex>(Allocator.TempJob);

                for (var i = 0; i < allSelected.Length; ++i)
                {
                    var vertex = allSelected[i];

                    vertex.X += offset.x;
                    vertex.Z += offset.z;

                    allSelected[i] = vertex;
                }

                _getSelectedVerticesWritable.CopyFromComponentDataArray(allSelected);

                allSelected.Dispose();

                EntityManager.AddComponent<DirtyMesh>(_getSelectedVertices);

                return true;
            }

            return false;
        }

        private readonly HashSet<Entity> _modifiedRoomSet = new HashSet<Entity>();
        private readonly List<Entity> _modifiedRoomList = new List<Entity>();

        protected override void OnUpdate()
        {
            var verticesMoved = false;

            if (UpdateHover(Player.leftHand, ref _leftHovered, out var leftHandPos))
            {
                verticesMoved |= UpdateInteract(Player.leftHand, leftHandPos, _leftHovered, ref _leftIsDragging, ref _leftDragOrigin);
            }

            if (UpdateHover(Player.rightHand, ref _rightHovered, out var rightHandPos))
            {
                verticesMoved |= UpdateInteract(Player.rightHand, rightHandPos, _rightHovered, ref _rightIsDragging, ref _rightDragOrigin);
            }

            if (!verticesMoved)
            {
                return;
            }

            var halfEdges = _getHalfEdges.ToComponentDataArray<HalfEdge>(Allocator.TempJob);

            _modifiedRoomSet.Clear();
            _modifiedRoomList.Clear();

            foreach (var halfEdge in halfEdges)
            {
                if (EntityManager.HasComponent<DirtyMesh>(halfEdge.Vertex))
                {
                    if (_modifiedRoomSet.Add(halfEdge.Room))
                    {
                        _modifiedRoomList.Add(halfEdge.Room);
                    }
                }
            }

            halfEdges.Dispose();

            var dirtyRooms = new NativeArray<Entity>(_modifiedRoomList.Count, Allocator.TempJob);

            for (var i = 0; i < _modifiedRoomList.Count; ++i)
            {
                dirtyRooms[i] = _modifiedRoomList[i];
            }

            EntityManager.RemoveComponent<DirtyMesh>(_getSelectedVertices);
            EntityManager.AddComponent<DirtyMesh>(dirtyRooms);

            dirtyRooms.Dispose();
        }

        protected override void OnDeselected()
        {
            if (_leftHovered != Entity.Null)
            {
                EntityManager.SetHovered(_leftHovered, false);
                _leftHovered = Entity.Null;
            }

            if (_rightHovered != Entity.Null)
            {
                EntityManager.SetHovered(_rightHovered, false);
                _rightHovered = Entity.Null;
            }

            _leftIsDragging = false;
            _rightIsDragging = false;
        }
    }
}
