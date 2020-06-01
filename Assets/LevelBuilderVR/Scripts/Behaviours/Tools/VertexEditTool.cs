﻿using LevelBuilderVR.Entities;
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
            public Entity HoveredVertex;
            public Entity HoveredHalfEdge;
            public bool IsActionHeld;
            public bool IsDragging;
            public bool IsDeselecting;
            public float3 DragOrigin;
            public float3 DragApplied;
        }

        private HandState _state;
        private Vector3 _gridOrigin;

        public ushort HapticPulseDurationMicros = 500;

        private EntityQuery _getSelectedVertices;
        private EntityQuery _getHalfEdges;
        private EntityQuery _getHalfEdgesWritable;

        private Entity _halfEdgeWidgetVertex;

        public override bool AllowTwoHanded => false;

        public override bool ShowGrid => _state.IsActionHeld && _state.IsDragging;
        public override Vector3 GridOrigin => _gridOrigin;

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

            if (_halfEdgeWidgetVertex == Entity.Null)
            {
                _halfEdgeWidgetVertex = EntityManager.CreateVertex(Level, 0f, 0f);
                EntityManager.AddComponent<Virtual>(_halfEdgeWidgetVertex);
                EntityManager.SetHovered(_halfEdgeWidgetVertex, true);
                EntityManager.SetVisible(_halfEdgeWidgetVertex, false);
            }
        }

        protected override void OnDeselected()
        {
            var widgetsVisible = EntityManager.GetComponentData<WidgetsVisible>(Level);
            widgetsVisible.Vertex = false;
            EntityManager.SetComponentData(Level, widgetsVisible);

            ResetState(ref _state);

            if (_halfEdgeWidgetVertex != Entity.Null)
            {
                EntityManager.DestroyEntity(_halfEdgeWidgetVertex);
                _halfEdgeWidgetVertex = Entity.Null;
            }
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
            var withinLevel = EntityManager.GetWithinLevel(level);

            _getSelectedVertices.SetSharedComponentFilter(withinLevel);
            _getHalfEdges.SetSharedComponentFilter(withinLevel);
            _getHalfEdgesWritable.SetSharedComponentFilter(withinLevel);
        }

        private bool UpdateHover(Hand hand, ref HandState state, out float3 localHandPos)
        {
            if (!hand.TryGetPointerPosition(out var handPos))
            {
                if (state.HoveredVertex != Entity.Null)
                {
                    EntityManager.SetHovered(state.HoveredVertex, false);
                    state.HoveredVertex = Entity.Null;
                }

                localHandPos = float3.zero;
                return false;
            }

            var localToWorld = EntityManager.GetComponentData<LocalToWorld>(Level).Value;
            var worldToLocal = EntityManager.GetComponentData<WorldToLocal>(Level).Value;
            localHandPos = math.transform(worldToLocal, handPos);

            if (state.IsActionHeld && state.IsDragging)
            {
                return true;
            }

            var interactDist2 = InteractRadius * InteractRadius;

            var newHoveredVertexWorldPos = float3.zero;
            var newHoveredVertexDist2 = float.PositiveInfinity;

            // Vertex widget
            if (EntityManager.FindClosestVertex(Level, localHandPos, out var newHoveredVertex, out var hoverPos))
            {
                var hoverWorldPos = math.transform(localToWorld, hoverPos);
                var dist2 = math.distancesq(hoverWorldPos, handPos);

                if (dist2 > interactDist2)
                {
                    newHoveredVertex = Entity.Null;
                }
                else
                {
                    newHoveredVertexDist2 = dist2;
                    newHoveredVertexWorldPos = hoverWorldPos;
                }
            }

            // HalfEdge / new Vertex widget
            var newHoveredHalfEdge = Entity.Null;
            if (!state.IsActionHeld && EntityManager.FindClosestHalfEdge(Level, localHandPos, newHoveredVertex != Entity.Null, out newHoveredHalfEdge, out hoverPos, out var virtualVertex))
            {
                var hoverWorldPos = math.transform(localToWorld, hoverPos);
                var dist2 = math.distancesq(hoverWorldPos, handPos);

                if (newHoveredVertex != Entity.Null)
                {
                    var vertexEdgeDist2 = math.distancesq(hoverWorldPos, newHoveredVertexWorldPos);
                    if (vertexEdgeDist2 > interactDist2)
                    {
                        // To fail the distance check later
                        dist2 = float.PositiveInfinity;
                    }
                }

                if (dist2 > interactDist2 || dist2 > newHoveredVertexDist2)
                {
                    newHoveredHalfEdge = Entity.Null;
                }
                else
                {
                    newHoveredVertex = Entity.Null;

                    EntityManager.SetComponentData(_halfEdgeWidgetVertex, virtualVertex);
                }
            }

            if (state.HoveredVertex == newHoveredVertex && state.HoveredHalfEdge == newHoveredHalfEdge)
            {
                return true;
            }

            hand.TriggerHapticPulse(HapticPulseDurationMicros);

            if (state.HoveredVertex != newHoveredVertex)
            {
                if (state.HoveredVertex != Entity.Null)
                {
                    EntityManager.SetHovered(state.HoveredVertex, false);
                }

                if (newHoveredVertex != Entity.Null)
                {
                    EntityManager.SetHovered(newHoveredVertex, true);
                }

                state.HoveredVertex = newHoveredVertex;
            }

            if (state.HoveredHalfEdge != newHoveredHalfEdge)
            {
                EntityManager.SetVisible(_halfEdgeWidgetVertex, newHoveredHalfEdge != Entity.Null);
                state.HoveredHalfEdge = newHoveredHalfEdge;
            }

            return true;
        }

        private void CreateNewVertexAtHover(ref HandState state)
        {
            var virtualVertex = EntityManager.GetComponentData<Vertex>(_halfEdgeWidgetVertex);

            var newVertex = EntityManager.CreateVertex(Level,
                math.round(virtualVertex.X / GridSnap) * GridSnap,
                math.round(virtualVertex.Z / GridSnap) * GridSnap);

            var halfEdgeStack = new TempEntitySet(SetAccess.Enumerate);

            EntityManager.FindAllStackedHalfEdges(state.HoveredHalfEdge, halfEdgeStack);

            var newHalfEdges = new NativeArray<Entity>(halfEdgeStack.Count, Allocator.TempJob);

            for (var i = 0; i < halfEdgeStack.Count; ++i)
            {
                newHalfEdges[i] = EntityManager.InsertHalfEdge(halfEdgeStack[i], newVertex);
            }

            if (newHalfEdges.Length > 1)
            {
                for (var i = 0; i < newHalfEdges.Length; ++i)
                {
                    var halfEdge = EntityManager.GetComponentData<HalfEdge>(newHalfEdges[i]);

                    if (i > 0) halfEdge.Below = newHalfEdges[i - 1];
                    if (i < newHalfEdges.Length - 1) halfEdge.Above = newHalfEdges[i + 1];

                    EntityManager.SetComponentData(newHalfEdges[i], halfEdge);
                }
            }

            halfEdgeStack.Dispose();
            newHalfEdges.Dispose();

            EntityManager.SetHovered(newVertex, true);

            state.HoveredVertex = newVertex;
            state.HoveredHalfEdge = Entity.Null;
        }

        private void UpdateInteract(Hand hand, float3 handPos, ref HandState state)
        {
            if (UseToolAction.GetStateDown(hand.handType))
            {
                state.IsActionHeld = true;

                if (state.HoveredHalfEdge != Entity.Null)
                {
                    CreateNewVertexAtHover(ref state);
                }

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
                    UpdateDragging(hand, handPos, ref state);
                    return;
                }

                UpdateSelecting(ref state);
            }

            if (state.IsActionHeld && UseToolAction.GetStateUp(hand.handType))
            {
                state.IsActionHeld = false;

                if (state.IsDragging)
                {
                    StopDragging(ref state);
                }
            }
        }

        private void StartSelecting(ref HandState state)
        {
            if (state.HoveredVertex != Entity.Null)
            {
                state.IsDeselecting = EntityManager.GetSelected(state.HoveredVertex);
                EntityManager.SetSelected(state.HoveredVertex, !state.IsDeselecting);
            }
            else
            {
                state.IsDeselecting = false;
            }

            state.IsDragging = false;
        }

        private void UpdateSelecting(ref HandState state)
        {
            if (state.HoveredVertex != Entity.Null)
            {
                EntityManager.SetSelected(state.HoveredVertex, !state.IsDeselecting);
            }
        }

        private void StartDragging(float3 handPos, ref HandState state)
        {
            if (state.HoveredVertex == Entity.Null || !EntityManager.GetSelected(state.HoveredVertex))
            {
                EntityManager.DeselectAll();

                if (state.HoveredVertex != Entity.Null)
                {
                    EntityManager.SetSelected(state.HoveredVertex, true);
                }
                else
                {
                    return;
                }
            }

            state.IsDragging = true;
            state.DragOrigin = handPos;
            state.DragApplied = float3.zero;

            HybridLevel.SetDragOffset(-state.DragApplied);

            if (state.HoveredVertex != Entity.Null)
            {
                var vertex = EntityManager.GetComponentData<Vertex>(state.HoveredVertex);

                _gridOrigin.y = vertex.MinY;
            }
        }

        private void UpdateDragging(Hand hand, float3 handPos, ref HandState state)
        {
            _gridOrigin.x = handPos.x;
            _gridOrigin.z = handPos.z;

            var offset = handPos - state.DragOrigin;

            if (AxisAlignAction.GetState(hand.handType))
            {
                var threshold = math.tan(math.PI / 8f);

                var xScore = math.abs(offset.x);
                var zScore = math.abs(offset.z);

                if (xScore * threshold > zScore)
                {
                    offset.z = 0f;
                }
                else if (zScore * threshold > xScore)
                {
                    offset.x = 0f;
                }
                else
                {
                    var avg = (xScore + zScore) * 0.5f;
                    offset.x = math.sign(offset.x) * avg;
                    offset.z = math.sign(offset.z) * avg;
                }
            }

            offset -= state.DragApplied;

            var intOffset = new int3(math.round(offset / GridSnap)) { y = 0 };

            if (math.lengthsq(intOffset) <= 0)
            {
                return;
            }

            offset = new float3(intOffset) * GridSnap;

            state.DragApplied += offset;
            HybridLevel.SetDragOffset(-state.DragApplied);

            var selectedEntities = _getSelectedVertices.ToEntityArray(Allocator.TempJob);
            var moves = new NativeArray<Move>(selectedEntities.Length, Allocator.TempJob);

            var move = new Move { Offset = offset };

            for (var i = 0; i < moves.Length; ++i)
            {
                moves[i] = move;
            }

            EntityManager.AddComponentData(_getSelectedVertices, moves);

            selectedEntities.Dispose();
            moves.Dispose();
        }

        private void StopDragging(ref HandState state)
        {
            HybridLevel.SetDragOffset(Vector3.zero);

            EntityManager.AddComponent<MergeOverlappingVertices>(Level);

            state.IsDragging = false;
        }

        private void ResetState(ref HandState state)
        {
            if (state.HoveredVertex != Entity.Null)
            {
                EntityManager.SetHovered(state.HoveredVertex, false);
                state.HoveredVertex = Entity.Null;
            }

            state.IsDragging = false;
        }
    }
}
