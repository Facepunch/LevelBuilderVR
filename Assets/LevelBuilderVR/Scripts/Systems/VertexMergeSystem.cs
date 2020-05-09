using System.Collections.Generic;
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

        protected override void OnUpdate()
        {
            var getVertex = GetComponentDataFromEntity<Vertex>(true);
            var getHalfEdge = GetComponentDataFromEntity<HalfEdge>(false);

            Entities
                .WithAllReadOnly<Level, MergeOverlappingVertices>()
                .ForEach(level =>
                {
                    PostUpdateCommands.RemoveComponent<MergeOverlappingVertices>(level);

                    // Find unmoved vertices that overlap with a moved vertex

                    _vertexReplacements.Clear();

                    _selectedVertices.SetSharedComponentFilter(new WithinLevel(level));
                    _unselectedVertices.SetSharedComponentFilter(new WithinLevel(level));

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
                        .ForEach((ref HalfEdge halfEdge) =>
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

                    // Destroy degenerate HalfEdges

                    _halfEdgesMutable.SetSharedComponentFilter(new WithinLevel(level));

                    var halfEdges = _halfEdgesMutable.ToEntityArray(Allocator.TempJob);

                    foreach (var entity in halfEdges)
                    {
                        var halfEdge = getHalfEdge[entity];

                        if (halfEdge.Vertex == Entity.Null)
                        {
                            // Already marked this HalfEdge as degenerate
                            continue;
                        }

                        var next = getHalfEdge[halfEdge.Next];
                        var roomEntity = halfEdge.Room;

                        // Keep moving halfEdge.Next along until it doesn't
                        // share its Vertex with halfEdge, or until we discover
                        // halfEdge is degenerate too

                        while (next.Vertex == halfEdge.Vertex)
                        {
                            getHalfEdge[halfEdge.Next] = default;
                            PostUpdateCommands.DestroyEntity(halfEdge.Next);

                            if (halfEdge.Next == entity)
                            {
                                break;
                            }

                            halfEdge.Next = next.Next;

                            next = getHalfEdge[next.Next];
                        }

                        if (halfEdge.Vertex == Entity.Null)
                        {
                            // The whole room was degenerate!
                            PostUpdateCommands.DestroyEntity(roomEntity);
                        }

                        getHalfEdge[entity] = halfEdge;
                    }

                    halfEdges.Dispose();
                });
        }
    }
}
