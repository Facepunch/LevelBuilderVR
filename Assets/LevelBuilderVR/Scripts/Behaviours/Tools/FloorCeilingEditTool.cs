
using LevelBuilderVR.Entities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Valve.VR.InteractionSystem;

namespace LevelBuilderVR.Behaviours.Tools
{
    public class FloorCeilingEditTool : Tool
    {
        private struct HandState
        {
            public Entity HoveredFloorCeiling;
            public bool IsActionHeld;
            public bool IsDragging;
            public bool IsDeselecting;
            public float3 DragOrigin;
            public float3 DragApplied;
        }

        private HandState _state;
        private Vector3 _gridOrigin;

        public ushort HapticPulseDurationMicros = 500;

        private EntityQuery _getSelectedFloorCeilings;

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
            EntityManager.SetComponentData(Level, new WidgetsVisible
            {
                FloorCeiling = true
            });
        }

        protected override void OnDeselected()
        {
            EntityManager.SetComponentData(Level, default(WidgetsVisible));
            HybridLevel.ExtrudeWidget.gameObject.SetActive(false);

            ResetState(ref _state);
        }

        protected override void OnStart()
        {
            _getSelectedFloorCeilings = EntityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<Selected>(), ComponentType.ReadOnly<FloorCeiling>(), ComponentType.ReadOnly<WithinLevel>() }
                });
        }

        protected override void OnSelectLevel(Entity level)
        {
            var withinLevel = EntityManager.GetWithinLevel(level);

            _getSelectedFloorCeilings.SetSharedComponentFilter(withinLevel);
        }

        private bool UpdateHover(Hand hand, ref HandState state, out float3 localHandPos)
        {
            if (!hand.TryGetPointerPosition(out var handPos))
            {
                if (state.HoveredFloorCeiling != Entity.Null)
                {
                    EntityManager.SetHovered(state.HoveredFloorCeiling, false);
                    state.HoveredFloorCeiling = Entity.Null;
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

            // Floor / ceiling widget
            var newHoveredFloorCeiling = Entity.Null;
            if (!state.IsActionHeld && EntityManager.FindClosestFloorCeiling(Level, localHandPos,
                out newHoveredFloorCeiling, out var hoverPos))
            {
                var hoverWorldPos = math.transform(localToWorld, hoverPos);
                var dist2 = math.distancesq(hoverWorldPos, handPos);

                if (dist2 > interactDist2)
                {
                    newHoveredFloorCeiling = Entity.Null;
                }
                else
                {
                    HybridLevel.ExtrudeWidget.transform.position = hoverWorldPos;

                    _gridOrigin.y = hoverPos.y;
                }
            }

            if (state.HoveredFloorCeiling == newHoveredFloorCeiling)
            {
                return true;
            }

            hand.TriggerHapticPulse(HapticPulseDurationMicros);

            if (state.HoveredFloorCeiling != newHoveredFloorCeiling)
            {
                if (state.HoveredFloorCeiling != Entity.Null)
                {
                    EntityManager.SetHovered(state.HoveredFloorCeiling, false);
                }

                if (newHoveredFloorCeiling != Entity.Null)
                {
                    EntityManager.SetHovered(newHoveredFloorCeiling, true);
                }

                HybridLevel.ExtrudeWidget.gameObject.SetActive(newHoveredFloorCeiling != Entity.Null);
                state.HoveredFloorCeiling = newHoveredFloorCeiling;
            }

            return true;
        }

        private void UpdateInteract(Hand hand, float3 handPos, ref HandState state)
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
            if (state.HoveredFloorCeiling != Entity.Null)
            {
                state.IsDeselecting = EntityManager.GetSelected(state.HoveredFloorCeiling);
                EntityManager.SetSelected(state.HoveredFloorCeiling, !state.IsDeselecting);
            }
            else
            {
                state.IsDeselecting = false;
            }

            state.IsDragging = false;
        }

        private void UpdateSelecting(ref HandState state)
        {
            if (state.HoveredFloorCeiling != Entity.Null)
            {
                EntityManager.SetSelected(state.HoveredFloorCeiling, !state.IsDeselecting);
            }
        }

        private void StartDragging(float3 handPos, ref HandState state)
        {
            if (state.HoveredFloorCeiling == Entity.Null || !EntityManager.GetSelected(state.HoveredFloorCeiling))
            {
                EntityManager.DeselectAll();

                if (state.HoveredFloorCeiling != Entity.Null)
                {
                    EntityManager.SetSelected(state.HoveredFloorCeiling, true);
                }
            }

            state.IsDragging = true;
            state.DragOrigin = handPos;
            state.DragApplied = float3.zero;

            HybridLevel.SetDragOffset(-state.DragApplied);
        }

        private void UpdateDragging(Hand hand, float3 handPos, ref HandState state)
        {
            _gridOrigin.x = handPos.x;
            _gridOrigin.z = handPos.z;

            var offset = handPos - state.DragOrigin - state.DragApplied;

            var intOffset = new int3(math.round(offset / GridSnap)) { x = 0, z = 0 };

            if (math.lengthsq(intOffset) <= 0)
            {
                return;
            }

            offset = new float3(intOffset) * GridSnap;

            state.DragApplied += offset;
            HybridLevel.SetDragOffset(-state.DragApplied);

            var selectedEntities = _getSelectedFloorCeilings.ToEntityArray(Allocator.TempJob);
            var moves = new NativeArray<Move>(selectedEntities.Length, Allocator.TempJob);

            var move = new Move { Offset = offset };

            for (var i = 0; i < moves.Length; ++i)
            {
                moves[i] = move;
            }

            EntityManager.AddComponentData(_getSelectedFloorCeilings, moves);

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
            if (state.HoveredFloorCeiling != Entity.Null)
            {
                EntityManager.SetHovered(state.HoveredFloorCeiling, false);
                state.HoveredFloorCeiling = Entity.Null;
            }

            state.IsDragging = false;
        }
    }
}
