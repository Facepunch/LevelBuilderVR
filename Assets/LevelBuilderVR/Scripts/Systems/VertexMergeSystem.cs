using System;
using System.Collections.Generic;
using System.Linq;
using LevelBuilderVR.Entities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace LevelBuilderVR.Systems
{
    [UpdateAfter(typeof(MoveSystem))]
    public class VertexMergeSystem : ComponentSystem
    {
        private EntityQuery _selectedVertices;
        private EntityQuery _unselectedVertices;

        private EntityQuery _halfEdgesMutable;

        protected override void OnCreate()
        {
            _selectedVertices = Entities
                .WithAllReadOnly<Vertex, Selected, WithinLevel>()
                .WithNone<Virtual>()
                .ToEntityQuery();

            _unselectedVertices = Entities
                .WithAllReadOnly<Vertex, WithinLevel>()
                .WithNone<Selected, Virtual>()
                .ToEntityQuery();

            _halfEdgesMutable = Entities
                .WithAllReadOnly<WithinLevel>()
                .WithAll<HalfEdge>()
                .ToEntityQuery();
        }

        private static bool IsOverlapping(in Vertex a, in Vertex b)
        {
            if (a.MinY > b.MaxY || a.MaxY < b.MinY) return false;

            var diff = new float2(a.X - b.X, a.Z - b.Z);
            return math.lengthsq(diff) <= 1f / (256f * 256f);
        }

        private int CountHalfEdgesInLoop(Entity first, ComponentDataFromEntity<HalfEdge> getHalfEdge)
        {
            var next = first;
            var count = 0;

            do
            {
                var halfEdge = getHalfEdge[next];

                ++count;

                next = halfEdge.Next;
            } while (next != first);

            return count;
        }

        private void SetRoomInEdgeLoop(Entity first, Entity room, ComponentDataFromEntity<HalfEdge> getHalfEdge)
        {
            var next = first;

            do
            {
                var halfEdge = getHalfEdge[next];
                halfEdge.Room = room;
                getHalfEdge[next] = halfEdge;

                next = halfEdge.Next;
            } while (next != first);
        }

        private void RemoveHalfEdgesInLoop(Entity first, TempEntitySet verticesToRemove, ComponentDataFromEntity<HalfEdge> getHalfEdge)
        {
            var next = first;

            do
            {
                var halfEdge = getHalfEdge[next];

                // Make sure BackFace doesn't reference this HalfEdge

                if (halfEdge.BackFace != Entity.Null)
                {
                    var complement = getHalfEdge[halfEdge.BackFace];

                    if (complement.BackFace != next)
                    {
                        Debug.LogError("Invalid BackFace!");
                    }
                    else
                    {
                        complement.BackFace = Entity.Null;

                        AddBackFaceCandidate(halfEdge.BackFace, complement.Vertex, complement);
                        getHalfEdge[halfEdge.BackFace] = complement;
                    }
                }

                verticesToRemove.Add(halfEdge.Vertex);

                getHalfEdge[next] = default;

                PostUpdateCommands.DestroyEntity(next);

                next = halfEdge.Next;
            } while (next != first);
        }

        private struct NewRoomAssignment
        {
            public Entity FirstHalfEdge;
            public Entity OldRoom;
        }

        private struct HalfEdgeVertices : IEquatable<HalfEdgeVertices>
        {
            public readonly Entity Prev;
            public readonly Entity Next;

            public HalfEdgeVertices(Entity prev, Entity next)
            {
                Prev = prev;
                Next = next;
            }

            public HalfEdgeVertices Complement => new HalfEdgeVertices(Next, Prev);

            public bool Equals(HalfEdgeVertices other)
            {
                return Prev.Equals(other.Prev) && Next.Equals(other.Next);
            }

            public override bool Equals(object obj)
            {
                return obj is HalfEdgeVertices other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Prev.GetHashCode() * 397) ^ Next.GetHashCode();
                }
            }

            public override string ToString()
            {
                return $"{{ {Prev}, {Next} }}";
            }
        }

        private struct BackFaceCandidate : IEquatable<BackFaceCandidate>, IComparable<BackFaceCandidate>
        {
            public readonly Entity HalfEdge;
            public readonly float MinY;
            public readonly float MaxY;

            public BackFaceCandidate(Entity halfEdge, float minY, float maxY)
            {
                HalfEdge = halfEdge;
                MinY = minY;
                MaxY = maxY;
            }

            public bool Equals(BackFaceCandidate other)
            {
                return HalfEdge.Equals(other.HalfEdge);
            }

            public override bool Equals(object obj)
            {
                return obj is BackFaceCandidate other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HalfEdge.GetHashCode();
            }

            public int CompareTo(BackFaceCandidate other)
            {
                return MinY.CompareTo(other.MinY);
            }

            public override string ToString()
            {
                return $"{{ Entity: {HalfEdge}, MinY: {MinY}, MaxY: {MaxY} }}";
            }
        }

        private readonly List<NewRoomAssignment> _newRoomAssignments = new List<NewRoomAssignment>();

        private readonly Dictionary<Entity, Entity> _vertexReplacements = new Dictionary<Entity, Entity>();
        private readonly Dictionary<HalfEdgeVertices, List<BackFaceCandidate>> _backfaceCandidates = new Dictionary<HalfEdgeVertices, List<BackFaceCandidate>>();
        private readonly List<List<BackFaceCandidate>> _backfaceCandidateListPool = new List<List<BackFaceCandidate>>();

        private void ClearBackfaceCandidates()
        {
            foreach (var pair in _backfaceCandidates)
            {
                _backfaceCandidateListPool.Add(pair.Value);
            }

            _backfaceCandidates.Clear();
        }

        private void HandleBackfaceCandidate(List<BackFaceCandidate> setA, List<BackFaceCandidate> setB, ref int indexA, ref int indexB, ComponentDataFromEntity<HalfEdge> getHalfEdge)
        {
            var firstIndexA = indexA;
            var firstIndexB = indexB;

            var a = setA[indexA++];
            var maxY = a.MaxY;

            // Taking first item from setA, check to see what overlaps it from both setA and setB.
            // Expand overlapping area each time.

            while (indexA < setA.Count || indexB < setB.Count)
            {
                var minA = indexA < setA.Count ? setA[indexA].MinY : float.PositiveInfinity;
                var minB = indexB < setB.Count ? setB[indexB].MinY : float.PositiveInfinity;

                if (minA >= maxY && minB >= maxY)
                {
                    break;
                }

                maxY = Math.Max(maxY, minA <= minB
                    ? setA[indexA++].MaxY
                    : setB[indexB++].MaxY);
            }

            // Can only resolve a 1:1 matching of overlapping candidates, anything else is skipped.

            if (indexA - firstIndexA != 1 || indexB - firstIndexB != 1)
            {
                return;
            }

            var b = setB[firstIndexB];

            var halfEdgeA = getHalfEdge[a.HalfEdge];
            var halfEdgeB = getHalfEdge[b.HalfEdge];

            halfEdgeA.BackFace = b.HalfEdge;
            halfEdgeB.BackFace = a.HalfEdge;

            getHalfEdge[a.HalfEdge] = halfEdgeA;
            getHalfEdge[b.HalfEdge] = halfEdgeB;
        }

        private void HandleBackfaceCandidates(List<BackFaceCandidate> setA, List<BackFaceCandidate> setB, ComponentDataFromEntity<HalfEdge> getHalfEdge)
        {
            // Sort by MinY
            setA.Sort();
            setB.Sort();

            var indexA = 0;
            var indexB = 0;

            while (indexA < setA.Count && indexB < setB.Count)
            {
                var firstA = setA[indexA];
                var firstB = setB[indexB];

                if (firstA.MinY <= firstB.MinY)
                {
                    HandleBackfaceCandidate(setA, setB, ref indexA, ref indexB, getHalfEdge);
                }
                else
                {
                    HandleBackfaceCandidate(setB, setA, ref indexB, ref indexA, getHalfEdge);
                }
            }
        }

        private void HandleBackfaceCandidates(ComponentDataFromEntity<HalfEdge> getHalfEdge)
        {
            while (_backfaceCandidates.Count > 0)
            {
                var firstPair = _backfaceCandidates.First();

                _backfaceCandidates.Remove(firstPair.Key);
                _backfaceCandidateListPool.Add(firstPair.Value);

                var complementKey = firstPair.Key.Complement;

                if (!_backfaceCandidates.TryGetValue(complementKey, out var complementValue))
                {
                    continue;
                }

                _backfaceCandidates.Remove(complementKey);
                _backfaceCandidateListPool.Add(complementValue);

                HandleBackfaceCandidates(firstPair.Value, complementValue, getHalfEdge);
            }
        }

        private void AddBackFaceCandidate(Entity entity, Entity nextVertex, HalfEdge halfEdge)
        {
            var key = new HalfEdgeVertices(halfEdge.Vertex, nextVertex);

            if (!_backfaceCandidates.TryGetValue(key, out var list))
            {
                if (_backfaceCandidateListPool.Count > 0)
                {
                    list = _backfaceCandidateListPool[_backfaceCandidateListPool.Count - 1];
                    _backfaceCandidateListPool.RemoveAt(_backfaceCandidateListPool.Count - 1);

                    list.Clear();
                }
                else
                {
                    list = new List<BackFaceCandidate>();
                }

                _backfaceCandidates.Add(key, list);
            }

            var candidate = new BackFaceCandidate(entity, halfEdge.MinY, halfEdge.MaxY);

            if (list.Contains(candidate))
            {
                return;
            }

            list.Add(candidate);
        }

        protected override void OnUpdate()
        {
            var getVertex = GetComponentDataFromEntity<Vertex>(true);
            var getHalfEdge = GetComponentDataFromEntity<HalfEdge>(false);

            _newRoomAssignments.Clear();

            Entities
                .WithAllReadOnly<Level, MergeOverlappingVertices>()
                .ForEach(level =>
                {
                    var withinLevel = EntityManager.GetWithinLevel(level);

                    PostUpdateCommands.RemoveComponent<MergeOverlappingVertices>(level);

                    // Find unmoved vertices that overlap with a moved vertex

                    _vertexReplacements.Clear();

                    ClearBackfaceCandidates();

                    _selectedVertices.SetSharedComponentFilter(withinLevel);
                    _unselectedVertices.SetSharedComponentFilter(withinLevel);

                    var selectedVertices = _selectedVertices.ToEntityArray(Allocator.TempJob);
                    var unselectedVertices = _unselectedVertices.ToEntityArray(Allocator.TempJob);

                    foreach (var unselectedEntity in unselectedVertices)
                    {
                        var unselectedVertex = getVertex[unselectedEntity];

                        foreach (var selectedEntity in selectedVertices)
                        {
                            var selectedVertex = getVertex[selectedEntity];

                            if (!IsOverlapping(in unselectedVertex, in selectedVertex))
                            {
                                continue;
                            }

                            _vertexReplacements.Add(unselectedEntity, selectedEntity);

                            if (!_vertexReplacements.ContainsKey(selectedEntity))
                            {
                                // A bit of a hack to help find new back faces
                                _vertexReplacements.Add(selectedEntity, selectedEntity);
                            }

                            PostUpdateCommands.AddComponent<DirtyMesh>(selectedEntity);
                            PostUpdateCommands.DestroyEntity(unselectedEntity);
                            break;
                        }
                    }

                    selectedVertices.Dispose();
                    unselectedVertices.Dispose();

                    if (_vertexReplacements.Count == 0)
                    {
                        return;
                    }

                    // Fix up Vertex references

                    Entities
                        .WithAll<HalfEdge>()
                        .ForEach((Entity entity, ref HalfEdge halfEdge) =>
                        {
                            var backfaceCandidate = false;

                            if (_vertexReplacements.TryGetValue(halfEdge.Vertex, out var newVertex))
                            {
                                backfaceCandidate = true;
                                halfEdge.Vertex = newVertex;
                            }

                            var next = getHalfEdge[halfEdge.Next];

                            if (_vertexReplacements.TryGetValue(next.Vertex, out var nextVertex))
                            {
                                backfaceCandidate = true;
                            } 
                            else
                            {
                                nextVertex = next.Vertex;
                            }

                            if (!backfaceCandidate || halfEdge.BackFace != Entity.Null) return;

                            AddBackFaceCandidate(entity, nextVertex, halfEdge);
                        });

                    _vertexReplacements.Clear();

                    HandleBackfaceCandidates(getHalfEdge);

                    // If two vertices of a room have been merged, the room will need to
                    // either be split in two, or some HalfEdges will need to be removed
                    // if the split would lead to a degenerate room.

                    _halfEdgesMutable.SetSharedComponentFilter(withinLevel);

                    var halfEdges = _halfEdgesMutable.ToEntityArray(Allocator.TempJob);
                    var verticesToRemove = new TempEntitySet(SetAccess.Enumerate);

                    foreach (var entBeforeFirst in halfEdges)
                    {
                        var heBeforeFirst = getHalfEdge[entBeforeFirst];

                        if (heBeforeFirst.Next == Entity.Null)
                        {
                            // Already marked this HalfEdge as degenerate
                            continue;
                        }

                        var oldRoom = heBeforeFirst.Room;

                        var entFirst = heBeforeFirst.Next;
                        var heFirst = getHalfEdge[entFirst];

                        var entPrev = entFirst;
                        var hePrev = heFirst;

                        var hasSplit = false;

                        // Find split point

                        var newRoomCount = 0;

                        while (hePrev.Next != entFirst)
                        {
                            ++newRoomCount;

                            var entNext = hePrev.Next;
                            var heNext = getHalfEdge[entNext];

                            if (heNext.Vertex == heFirst.Vertex)
                            {
                                // Room has been split!

                                hePrev.Next = entFirst;
                                heBeforeFirst.Next = entNext;

                                getHalfEdge[entPrev] = hePrev;
                                getHalfEdge[entBeforeFirst] = heBeforeFirst;

                                hasSplit = true;

                                break;
                            }

                            entPrev = entNext;
                            hePrev = heNext;
                        }

                        if (!hasSplit)
                        {
                            continue;
                        }

                        var oldRoomCount = CountHalfEdgesInLoop(entBeforeFirst, getHalfEdge);

                        var oldRoomHalfEdge = entBeforeFirst;
                        var newRoomHalfEdge = entFirst;

                        ClearBackfaceCandidates();

                        if (oldRoomCount < 3 && newRoomCount < 3)
                        {
                            // This whole room is degenerate!

                            RemoveHalfEdgesInLoop(oldRoomHalfEdge, verticesToRemove, getHalfEdge);
                            RemoveHalfEdgesInLoop(newRoomHalfEdge, verticesToRemove, getHalfEdge);

                            PostUpdateCommands.DestroyEntity(oldRoom);
                        }
                        else if (oldRoomCount < 3)
                        {
                            RemoveHalfEdgesInLoop(oldRoomHalfEdge, verticesToRemove, getHalfEdge);
                        }
                        else if (newRoomCount < 3)
                        {
                            RemoveHalfEdgesInLoop(newRoomHalfEdge, verticesToRemove, getHalfEdge);
                        }
                        else
                        {
                            // Need to create a new room

                            var smallestRoomHalfEdge = newRoomCount < oldRoomCount ? newRoomHalfEdge : oldRoomHalfEdge;

                            _newRoomAssignments.Add(new NewRoomAssignment
                            {
                                FirstHalfEdge = smallestRoomHalfEdge,
                                OldRoom = oldRoom
                            });
                        }

                        HandleBackfaceCandidates(getHalfEdge);
                    }

                    // Find out which vertices are still referenced

                    foreach (var entity in halfEdges)
                    {
                        var halfEdge = getHalfEdge[entity];
                        verticesToRemove.Remove(halfEdge.Vertex);
                    }

                    halfEdges.Dispose();

                    // Destroy unreferenced vertices

                    foreach (var vertex in verticesToRemove)
                    {
                        PostUpdateCommands.DestroyEntity(vertex);
                    }

                    verticesToRemove.Dispose();
                });

            if (_newRoomAssignments.Count == 0)
            {
                return;
            }

            foreach (var newRoomAssignment in _newRoomAssignments)
            {
                var newRoom = EntityManager.CopyRoom(newRoomAssignment.OldRoom);
                getHalfEdge = GetComponentDataFromEntity<HalfEdge>(false);

                SetRoomInEdgeLoop(newRoomAssignment.FirstHalfEdge, newRoom, getHalfEdge);
            }
        }
    }
}
