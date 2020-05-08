using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace LevelBuilderVR.Systems
{
    public class VertexEditSystem : ComponentSystem
    {
        private EntityQuery _movedVertices;

        protected override void OnCreate()
        {
            _movedVertices = Entities
                .WithAllReadOnly<Move, Vertex>()
                .ToEntityQuery();
        }

        private static bool IsOverlapping(in Vertex a, in Vertex b)
        {
            var diff = new float2(a.X - b.X, a.Z - b.Z);
            return math.lengthsq(diff) <= 1f / (256f * 256f);
        }

        protected override void OnUpdate()
        {
            Entities
                .WithAllReadOnly<Move>()
                .WithAll<Vertex>()
                .ForEach((Entity entity, ref Vertex vertex, ref Move move) =>
                {
                    vertex.X += move.Offset.x;
                    vertex.Z += move.Offset.z;
                });

            var getVertex = GetComponentDataFromEntity<Vertex>(true);
            var getHalfEdge = GetComponentDataFromEntity<HalfEdge>(false);

            var verticesToRemove = new TempEntitySet(SetAccess.Enumerate);

            Entities
                .WithAllReadOnly<Level, MergeOverlappingVertices>()
                .ForEach(level =>
                {
                    verticesToRemove.Clear();

                    PostUpdateCommands.RemoveComponent<MergeOverlappingVertices>(level);

                    // TODO: move to query with shared data

                    // Destroy degenerate HalfEdges, and add their vertices to verticesToRemove

                    Entities
                        .WithAll<HalfEdge>()
                        .ForEach((ref HalfEdge halfEdge) =>
                        {
                            if (halfEdge.Vertex == Entity.Null)
                            {
                                // Already marked this HalfEdge as degenerate
                                return;
                            }

                            var vertex = getVertex[halfEdge.Vertex];
                            var next = getHalfEdge[halfEdge.Next];

                            // Loop to make sure halfEdge.Next has a non-overlapping vertex

                            while (next.Vertex != halfEdge.Vertex)
                            {
                                var nextVertex = getVertex[next.Vertex];

                                if (!IsOverlapping(in vertex, in nextVertex))
                                {
                                    break;
                                }

                                verticesToRemove.Add(next.Vertex);

                                getHalfEdge[halfEdge.Next] = default;
                                PostUpdateCommands.DestroyEntity(halfEdge.Next);

                                halfEdge.Next = next.Next;
                                next = getHalfEdge[next.Next];
                            }
                        });

                    // Some vertices in verticesToRemove might still be valid,
                    // if they are used by a HalfEdge in a different Room

                    Entities
                        .WithAllReadOnly<HalfEdge>()
                        .ForEach((ref HalfEdge halfEdge) =>
                        {
                            if (halfEdge.Vertex != Entity.Null)
                            {
                                verticesToRemove.Remove(halfEdge.Vertex);
                            }
                        });

                    foreach (var vertex in verticesToRemove)
                    {
                        PostUpdateCommands.DestroyEntity(vertex);
                    }
                });

            verticesToRemove.Dispose();

            PostUpdateCommands.AddComponent<DirtyMesh>(_movedVertices);
            PostUpdateCommands.RemoveComponent<Move>(_movedVertices);
        }
    }
}
