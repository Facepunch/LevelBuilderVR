using System.Collections.Generic;
using LevelBuilderVR.Entities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace LevelBuilderVR.Systems
{
    [UpdateAfter(typeof(VertexEditSystem))]
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

        private readonly Dictionary<Entity, Entity> _vertexReplacements = new Dictionary<Entity, Entity>();

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

        private readonly List<NewRoomAssignment> _newRoomAssignments = new List<NewRoomAssignment>();

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
                            if (_vertexReplacements.TryGetValue(halfEdge.Vertex, out var newVertex))
                            {
                                halfEdge.Vertex = newVertex;
                            }
                        });

                    Entities
                        .WithAll<Room, SlopedFloor>()
                        .ForEach((ref SlopedFloor floor) =>
                        {
                            if (_vertexReplacements.TryGetValue(floor.Anchor0.Vertex, out var newVertex))
                            {
                                floor.Anchor0.Vertex = newVertex;
                            }

                            if (_vertexReplacements.TryGetValue(floor.Anchor1.Vertex, out newVertex))
                            {
                                floor.Anchor1.Vertex = newVertex;
                            }

                            if (_vertexReplacements.TryGetValue(floor.Anchor2.Vertex, out newVertex))
                            {
                                floor.Anchor2.Vertex = newVertex;
                            }
                        });

                    Entities
                        .WithAll<Room, SlopedCeiling>()
                        .ForEach((ref SlopedCeiling ceiling) =>
                        {
                            if (_vertexReplacements.TryGetValue(ceiling.Anchor0.Vertex, out var newVertex))
                            {
                                ceiling.Anchor0.Vertex = newVertex;
                            }

                            if (_vertexReplacements.TryGetValue(ceiling.Anchor1.Vertex, out newVertex))
                            {
                                ceiling.Anchor1.Vertex = newVertex;
                            }

                            if (_vertexReplacements.TryGetValue(ceiling.Anchor2.Vertex, out newVertex))
                            {
                                ceiling.Anchor2.Vertex = newVertex;
                            }
                        });

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
