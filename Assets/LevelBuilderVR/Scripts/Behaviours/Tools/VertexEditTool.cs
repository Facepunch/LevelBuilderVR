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
        private EntityQuery _getSelectedVerticesWritable;
        private EntityQuery _getHalfEdges;

        private readonly HashSet<Entity> _tempEntitySet = new HashSet<Entity>();
        private readonly List<Entity> _tempEntityList = new List<Entity>();

        public override bool AllowTwoHanded => false;

        protected override void OnUpdate()
        {
            var verticesMoved = false;

            var hand = LeftHandActive ? Player.leftHand : Player.rightHand;

            if (UpdateHover(hand, ref _state, out var leftHandPos))
            {
                verticesMoved |= UpdateInteract(hand, leftHandPos, ref _state);
            }

            if (verticesMoved)
            {
                UpdateDirtyRooms();
            }
        }

        protected override void OnDeselected()
        {
            ResetState(ref _state);
        }

        protected override void OnStart()
        {
            _getSelectedVertices = EntityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<Selected>(), ComponentType.ReadOnly<Vertex>(), ComponentType.ReadOnly<WithinLevel>()  }
                });

            _getSelectedVerticesWritable = EntityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<Selected>(), ComponentType.ReadOnly<WithinLevel>(), typeof(Vertex) }
                });

            _getHalfEdges = EntityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<HalfEdge>(), ComponentType.ReadOnly<WithinLevel>() }
                });
        }

        protected override void OnSelectLevel(Entity level)
        {
            _getSelectedVertices.SetSharedComponentFilter(new WithinLevel(Level));
            _getSelectedVerticesWritable.SetSharedComponentFilter(new WithinLevel(Level));
            _getHalfEdges.SetSharedComponentFilter(new WithinLevel(Level));
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
            // Find all selected vertices that are connected to non-selected ones
            // For each such vertex:
            //   Create a new vertex at the original position of that vertex
            //   If not connected to any other selected vertices:
            //     Pick half edge that the new vertex can be inserted into, minimizing total length
            //   Otherwise:
            //     Insert half edge(s) between selected and new vertex

            var em = EntityManager;
            var vertexHalfEdgesDict = _tempVertexHalfEdgesDict;
            var vertexHalfEdgesPool = _vertexHalfEdgesPool;
            var vertexHalfEdgesUsed = _vertexHalfEdgesUsed;

            var endVertexList = _tempEntityList;

            endVertexList.Clear();

            foreach (var vhe in vertexHalfEdgesUsed)
            {
                vertexHalfEdgesPool.Add(vhe);
            }

            vertexHalfEdgesUsed.Clear();
            vertexHalfEdgesDict.Clear();

            var allHalfEdgeEntities = _getHalfEdges.ToEntityArray(Allocator.TempJob);
            var allHalfEdges = _getHalfEdges.ToComponentDataArray<HalfEdge>(Allocator.TempJob);

            // Find all selected vertices connected to a non-selected vertex
            // Build up a dict of all connecting half edges for each of these "end" vertices

            for (var i = 0; i < allHalfEdges.Length; ++i)
            {
                var halfEdge = allHalfEdges[i];

                var nextVertex = em.GetComponentData<HalfEdge>(halfEdge.Next).Vertex;
                var thisSelected = em.HasComponent<Selected>(halfEdge.Vertex);
                var nextSelected = em.HasComponent<Selected>(nextVertex);

                if (thisSelected == nextSelected)
                {
                    continue;
                }

                var endVertex = thisSelected ? halfEdge.Vertex : nextVertex;

                if (!vertexHalfEdgesDict.TryGetValue(endVertex, out var vertexHalfEdges))
                {
                    endVertexList.Add(endVertex);

                    vertexHalfEdges = new List<Entity>();
                    vertexHalfEdgesUsed.Add(vertexHalfEdges);
                    vertexHalfEdgesDict.Add(endVertex, vertexHalfEdges);
                }

                vertexHalfEdges.Add(allHalfEdgeEntities[i]);
            }

            allHalfEdgeEntities.Dispose();
            allHalfEdges.Dispose();

            // TODO
            var bestHalfEdges = new List<Entity>();

            // For each "end" vertex, find out which edge(s) are the best to insert
            // the newly created vertex into

            foreach (var entity in endVertexList)
            {
                var vertex = em.GetComponentData<Vertex>(entity);
                var halfEdges = vertexHalfEdgesDict[entity];

                var newPos = new float2(vertex.X - state.DragApplied.x, vertex.Z - state.DragApplied.z);
                var newVertex = em.CreateVertex(Level, newPos.x, newPos.y);

                em.AddComponent<DirtyMesh>(newVertex);

                bestHalfEdges.Clear();
                var bestScore = float.PositiveInfinity;

                foreach (var halfEdgeEntity in halfEdges)
                {
                    var halfEdge = em.GetComponentData<HalfEdge>(halfEdgeEntity);
                    var next = em.GetComponentData<HalfEdge>(halfEdge.Next);

                    var vertex0 = em.GetComponentData<Vertex>(halfEdge.Vertex);
                    var vertex1 = em.GetComponentData<Vertex>(next.Vertex);

                    var pos0 = new float2(vertex0.X, vertex0.Z);
                    var pos1 = new float2(vertex1.X, vertex1.Z);

                    var score = math.length(newPos - pos0) + math.length(pos1 - newPos) - math.length(pos1 - pos0);

                    if (Math.Abs(score - bestScore) < 1f / 65536f)
                    {
                        bestHalfEdges.Add(halfEdgeEntity);
                    }
                    else if (score < bestScore)
                    {
                        bestScore = score;
                        bestHalfEdges.Clear();
                        bestHalfEdges.Add(halfEdgeEntity);
                    }
                }

                foreach (var bestHalfEdgeEntity in bestHalfEdges)
                {
                    em.InsertHalfEdge(bestHalfEdgeEntity, newVertex);
                }
            }

            state.DragOrigin += state.DragApplied;
            state.DragApplied = float3.zero;
            HybridLevel.SetDragOffset(-state.DragApplied);
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

        private bool StopDragging(ref HandState state)
        {
            // Handle merging vertices
            var allSelected = _getSelectedVerticesWritable.ToComponentDataArray<Vertex>(Allocator.TempJob);

            allSelected.Dispose();

            HybridLevel.SetDragOffset(Vector3.zero);
            return false;
        }

        private void UpdateDirtyRooms()
        {
            var halfEdges = _getHalfEdges.ToComponentDataArray<HalfEdge>(Allocator.TempJob);

            var modifiedRoomSet = _tempEntitySet;
            var modifiedRoomList = _tempEntityList;

            modifiedRoomSet.Clear();
            modifiedRoomList.Clear();

            var em = EntityManager;

            foreach (var halfEdge in halfEdges)
            {
                if (em.HasComponent<DirtyMesh>(halfEdge.Vertex))
                {
                    if (modifiedRoomSet.Add(halfEdge.Room))
                    {
                        modifiedRoomList.Add(halfEdge.Room);
                    }
                }
            }

            halfEdges.Dispose();

            var dirtyRooms = new NativeArray<Entity>(modifiedRoomList.Count, Allocator.TempJob);

            for (var i = 0; i < modifiedRoomList.Count; ++i)
            {
                dirtyRooms[i] = modifiedRoomList[i];
            }

            em.RemoveComponent<DirtyMesh>(_getSelectedVertices);
            em.AddComponent<DirtyMesh>(dirtyRooms);

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
