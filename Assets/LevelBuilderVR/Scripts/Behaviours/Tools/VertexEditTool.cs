using System;
using System.Collections.Generic;
using LevelBuilderVR.Entities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Valve.VR.InteractionSystem;

namespace LevelBuilderVR.Behaviours.Tools
{
    public class VertexEditTool : Tool
    {
        private struct HandState
        {
            public Entity Hovered;
            public bool IsActionHeld;
            public bool IsDragging;
            public bool IsDeselecting;
            public float3 DragOrigin;
            public float3 DragApplied;
        }

        private HandState _state;

        public float InteractRadius = 0.05f;
        public float GridSnap = 0.25f;

        public ushort HapticPulseDurationMicros = 500;

        private EntityQuery _getSelectedVertices;
        private EntityQuery _getHalfEdges;
        private EntityQuery _getHalfEdgesWritable;

        public override bool AllowTwoHanded => false;

        protected override void OnUpdate()
        {
            var hand = LeftHandActive ? Player.leftHand : Player.rightHand;

            if (UpdateHover(hand, ref _state, out var leftHandPos))
            {
                UpdateInteract(hand, leftHandPos, ref _state);
            }
        }

        protected override void OnSelected()
        {
            var widgetsVisible = EntityManager.GetComponentData<WidgetsVisible>(Level);
            widgetsVisible.Vertex = true;
            EntityManager.SetComponentData(Level, widgetsVisible);
        }

        protected override void OnDeselected()
        {
            var widgetsVisible = EntityManager.GetComponentData<WidgetsVisible>(Level);
            widgetsVisible.Vertex = false;
            EntityManager.SetComponentData(Level, widgetsVisible);

            ResetState(ref _state);
        }

        protected override void OnStart()
        {
            _getSelectedVertices = EntityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<Selected>(), ComponentType.ReadOnly<Vertex>(), ComponentType.ReadOnly<WithinLevel>()  }
                });

            _getHalfEdges = EntityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<HalfEdge>(), ComponentType.ReadOnly<WithinLevel>() }
                });

            _getHalfEdgesWritable = EntityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { typeof(HalfEdge), ComponentType.ReadOnly<WithinLevel>() }
                });
        }

        protected override void OnSelectLevel(Entity level)
        {
            _getSelectedVertices.SetSharedComponentFilter(new WithinLevel(Level));
            _getHalfEdges.SetSharedComponentFilter(new WithinLevel(Level));
            _getHalfEdgesWritable.SetSharedComponentFilter(new WithinLevel(Level));
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
                state.IsActionHeld = true;
                
                if (MultiSelectAction.GetState(hand.handType))
                {
                    StartSelecting(ref state);
                }
                else
                {
                    StartDragging(handPos, ref state);
                }
            }
            else if (state.IsActionHeld && UseToolAction.GetState(hand.handType))
            {
                if (state.IsDragging)
                {
                    var roomsDirty = false;

                    if (MultiSelectAction.GetStateDown(hand.handType))
                    {
                        DuplicateVertices(ref state);
                        roomsDirty = true;
                    }

                    return UpdateDragging(hand, handPos, ref state) || roomsDirty;
                }

                UpdateSelecting(ref state);
            }

            if (state.IsActionHeld && UseToolAction.GetStateUp(hand.handType))
            {
                state.IsActionHeld = false;

                if (state.IsDragging)
                {
                    return StopDragging(ref state);
                }
            }

            return false;
        }

        private void StartSelecting(ref HandState state)
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

        private void UpdateSelecting(ref HandState state)
        {
            if (state.Hovered != Entity.Null)
            {
                EntityManager.SetSelected(state.Hovered, !state.IsDeselecting);
            }
        }

        private void StartDragging(float3 handPos, ref HandState state)
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

            HybridLevel.SetDragOffset(-state.DragApplied);
        }

        private readonly List<List<Entity>> _vertexHalfEdgesPool = new List<List<Entity>>();
        private readonly List<List<Entity>> _vertexHalfEdgesUsed = new List<List<Entity>>();

        private readonly Dictionary<Entity, List<Entity>> _tempVertexHalfEdgesDict =
            new Dictionary<Entity, List<Entity>>();

        private void DuplicateVertices(ref HandState state)
        {
            // TODO
        }

        private bool UpdateDragging(Hand hand, float3 handPos, ref HandState state)
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
                    offset.x = 0f;
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
            HybridLevel.SetDragOffset(-state.DragApplied);

            var selectedEntities = _getSelectedVertices.ToEntityArray(Allocator.TempJob);
            var moves = new NativeArray<Move>(selectedEntities.Length, Allocator.TempJob);

            var move = new Move {Offset = offset};

            for (var i = 0; i < moves.Length; ++i)
            {
                moves[i] = move;
            }

            EntityManager.AddComponentData(_getSelectedVertices, moves);
            EntityManager.AddComponent<DirtyMesh>(_getSelectedVertices);

            selectedEntities.Dispose();
            moves.Dispose();

            return true;
        }

        private const int HashResolution = 256;

        private static int2 GetPositionHash(Vertex vertex)
        {
            return new int2((int) math.round(vertex.X * HashResolution), (int) math.round(vertex.Z * HashResolution));
        }

        private bool CleanupGeometry()
        {
            return false;

            // Remove zero-length edges, then get rid of unreferenced vertices

            var halfEdges = _getHalfEdgesWritable.ToComponentDataArray<HalfEdge>(Allocator.TempJob);


            for (var i = 0; i < halfEdges.Length; ++i)
            {
                var halfEdge = halfEdges[i];
            }

            halfEdges.Dispose();

            // Remove unreferenced vertices

            var unreferenced = new TempEntitySet(SetAccess.Enumerate);

            EntityManager.GetUnreferencedVertices(Level, unreferenced);
            EntityManager.DestroyEntities(unreferenced);

            unreferenced.Dispose();
        }

        private bool StopDragging(ref HandState state)
        {
            HybridLevel.SetDragOffset(Vector3.zero);

            return CleanupGeometry();
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
