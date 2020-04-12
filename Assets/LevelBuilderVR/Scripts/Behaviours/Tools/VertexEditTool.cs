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
        private struct HandState
        {
            public Entity Hovered;
            public bool IsDragging;
            public bool IsDeselecting;
            public float3 DragOrigin;
            public float3 DragApplied;
        }

        private HandState _leftState;
        private HandState _rightState;

        public float InteractRadius = 0.05f;
        public float GridSnap = 0.25f;

        public ushort HapticPulseDurationMicros = 500;

        private EntityQuery _getSelectedVertices;
        private EntityQuery _getSelectedVerticesWritable;
        private EntityQuery _getHalfEdges;

        protected override void OnUpdate()
        {
            var verticesMoved = false;

            if (UpdateHover(Player.leftHand, ref _leftState, out var leftHandPos))
            {
                verticesMoved |= UpdateInteract(Player.leftHand, leftHandPos, ref _leftState);
            }

            if (UpdateHover(Player.rightHand, ref _rightState, out var rightHandPos))
            {
                verticesMoved |= UpdateInteract(Player.rightHand, rightHandPos, ref _rightState);
            }

            if (verticesMoved)
            {
                UpdateDirtyRooms();
            }
        }

        protected override void OnDeselected()
        {
            ResetState(ref _leftState);
            ResetState(ref _rightState);
        }

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

        private bool UpdateHover(Hand hand, ref HandState state, out float3 localHandPos)
        {
            if (!hand.TryGetPointerPosition(out var handPos))
            {
                if (state.Hovered != Entity.Null)
                {
                    EntityManager.SetHovered(state.Hovered, false);
                    state.Hovered = Entity.Null;
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

            if (state.Hovered == newHovered)
            {
                return true;
            }

            hand.TriggerHapticPulse(HapticPulseDurationMicros);

            if (state.Hovered != Entity.Null)
            {
                EntityManager.SetHovered(state.Hovered, false);
            }

            if (newHovered != Entity.Null)
            {
                EntityManager.SetHovered(newHovered, true);
            }

            state.Hovered = newHovered;

            return true;
        }

        private bool UpdateInteract(Hand hand, float3 handPos, ref HandState state)
        {
            if (UseToolAction.GetStateDown(hand.handType))
            {
                if (MultiSelectAction.GetState(hand.handType))
                {
                    if (state.Hovered != Entity.Null)
                    {
                        state.IsDeselecting = EntityManager.GetSelected(state.Hovered);
                        EntityManager.SetSelected(state.Hovered, !state.IsDeselecting);
                    }
                    else
                    {
                        state.IsDeselecting = false;
                    }

                    state.IsDragging = false;
                }
                else
                {
                    if (state.Hovered == Entity.Null || !EntityManager.GetSelected(state.Hovered))
                    {
                        EntityManager.DeselectAll();

                        if (state.Hovered != Entity.Null)
                        {
                            EntityManager.SetSelected(state.Hovered, true);
                        }
                    }

                    state.IsDragging = true;
                    state.DragOrigin = handPos;
                    state.DragApplied = float3.zero;
                }
            }
            else if (UseToolAction.GetState(hand.handType))
            {
                if (state.IsDragging)
                {
                    var offset = handPos - state.DragOrigin;

                    if (AxisAlignAction.GetState(hand.handType))
                    {
                        var xScore = math.abs(offset.x);
                        var zScore = math.abs(offset.z);

                        if (xScore > zScore)
                        {
                            offset.z = 0f;
                        }
                        else
                        {
                            var avg = (offset.x + offset.z) * 0.5f;
                            offset.x = math.sign(offset.x) * avg;
                            offset.z = math.sign(offset.z) * avg;
                        }
                    }

                    offset -= state.DragApplied;

                    var intOffset = new int3(math.round(offset / GridSnap))
                    {
                        y = 0
                    };

                    if (math.lengthsq(intOffset) <= 0)
                    {
                        return false;
                    }

                    offset = new float3(intOffset) * GridSnap;

                    state.DragApplied += offset;

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
                else if (state.Hovered != Entity.Null)
                {
                    EntityManager.SetSelected(state.Hovered, !state.IsDeselecting);
                }
            }

            return false;
        }

        private readonly HashSet<Entity> _modifiedRoomSet = new HashSet<Entity>();
        private readonly List<Entity> _modifiedRoomList = new List<Entity>();

        private void UpdateDirtyRooms()
        {
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

        private void ResetState(ref HandState state)
        {
            if (state.Hovered != Entity.Null)
            {
                EntityManager.SetHovered(state.Hovered, false);
                state.Hovered = Entity.Null;
            }

            state.IsDragging = false;
        }
    }
}
