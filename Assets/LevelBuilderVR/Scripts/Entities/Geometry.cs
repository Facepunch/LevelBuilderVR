using System;
using System.Collections.Generic;
using LevelBuilderVR.Behaviours;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Object = UnityEngine.Object;

namespace LevelBuilderVR.Entities
{ 
    public static partial class EntityHelper
    {
        private static HybridLevel _sHybridLevel;

        private static HybridLevel HybridLevel => _sHybridLevel ?? (_sHybridLevel = Object.FindObjectOfType<HybridLevel>());

        public static int FindRoomHalfEdges(this EntityManager em, Entity room, TempEntitySet outHalfEdges)
        {
            var withinLevel = em.GetSharedComponentData<WithinLevel>(room);

            var count = 0;

            _sHalfEdgesQuery.SetSharedComponentFilter(withinLevel);
            using (var halfEdges = _sHalfEdgesQuery.ToComponentDataArray<HalfEdge>(Allocator.TempJob))
            {
                foreach (var halfEdge in halfEdges)
                {
                    if (halfEdge.Room == room && outHalfEdges.Add(halfEdge.Next))
                    {
                        ++count;
                    }
                }
            }

            return count;
        }

        public static bool FindClosestVertex(this EntityManager em, Entity level, float3 localPos,
            out Entity outEntity, out float3 outClosestPoint)
        {
            outEntity = Entity.Null;
            outClosestPoint = localPos;

            var closestDist2 = float.PositiveInfinity;

            _sVerticesQuery.SetSharedComponentFilter(em.GetWithinLevel(level));
            using (var entities = _sVerticesQuery.ToEntityArray(Allocator.TempJob))
            {
                foreach (var entity in entities)
                {
                    var vertex = em.GetComponentData<Vertex>(entity);
                    var pos = new float3(vertex.X, math.clamp(localPos.y, vertex.MinY, vertex.MaxY), vertex.Z);
                    var dist2 = math.lengthsq(pos - localPos);

                    if (dist2 >= closestDist2)
                    {
                        continue;
                    }

                    closestDist2 = dist2;
                    outClosestPoint = pos;
                    outEntity = entity;
                }
            }

            return outEntity != Entity.Null;
        }

        public static bool FindClosestHalfEdge(this EntityManager em, Entity level, float3 localPos, bool alwaysAtMidpoint,
            out Entity outEntity, out float3 outClosestPoint, out Vertex outVirtualVertex)
        {
            const float epsilon = 1f / 65536f;

            outEntity = Entity.Null;
            outClosestPoint = localPos;
            outVirtualVertex = default;

            var closestDist2 = float.PositiveInfinity;
            var closestV = 0f;

            _sHalfEdgesQuery.SetSharedComponentFilter(em.GetWithinLevel(level));
            using (var entities = _sHalfEdgesQuery.ToEntityArray(Allocator.TempJob))
            {
                foreach (var entity in entities)
                {
                    var halfEdge = em.GetComponentData<HalfEdge>(entity);
                    var nextHalfEdge = em.GetComponentData<HalfEdge>(halfEdge.Next);
                    var vertex0 = em.GetComponentData<Vertex>(halfEdge.Vertex);
                    var vertex1 = em.GetComponentData<Vertex>(nextHalfEdge.Vertex);

                    var p0 = new float3(vertex0.X, 0f, vertex0.Z);
                    var p1 = new float3(vertex1.X, 0f, vertex1.Z);
                    var length = math.length(p1 - p0);

                    if (length < epsilon)
                    {
                        continue;
                    }

                    var tangent = math.normalize(p1 - p0);
                    var normal = new float3(-tangent.y, 0f, tangent.x);

                    var diff = localPos - p0;

                    var u = alwaysAtMidpoint ? length * 0.5f : math.dot(diff, tangent);
                    var v = math.dot(diff, normal);

                    var clampedU = math.clamp(u, 0f, length);
                    
                    var t = clampedU / length;
                    var minY = math.lerp(vertex0.MinY, vertex1.MinY, t);
                    var maxY = math.lerp(vertex0.MaxY, vertex1.MaxY, t);

                    var onEdgePos = p0 + math.clamp(u, 0f, length) * tangent;

                    onEdgePos.y = math.clamp(localPos.y, minY, maxY);

                    var dist2 = math.lengthsq(localPos - onEdgePos);

                    if (dist2 < closestDist2 || math.abs(dist2 - closestDist2) <= epsilon * epsilon && closestV < 0f && v > 0f)
                    {
                        closestDist2 = dist2;
                        closestV = v;

                        outClosestPoint = onEdgePos;
                        outEntity = entity;

                        outVirtualVertex = new Vertex
                        {
                            X = outClosestPoint.x,
                            Z = outClosestPoint.z,
                            MinY = minY,
                            MaxY = maxY
                        };
                    }
                }
            }

            return outEntity != Entity.Null;
        }
        public static HalfEdgeLoopEnumerable EnumerateHalfEdgeLoop(this EntityManager em, Entity first)
        {
            return new HalfEdgeLoopEnumerable(em, first);
        }

        public static HalfEdgeLoopVertexPairEnumerable EnumerateVertexPairsInHalfEdgeLoop(this EntityManager em, Entity first)
        {
            return new HalfEdgeLoopVertexPairEnumerable(em, first);
        }

        public static bool IsPointWithinHalfEdgeLoop(this EntityManager em, Entity first, float3 localPos)
        {
            var winding = 0;

            var p = new float2(localPos.x, localPos.z);
            var n = new float2(0f, 1f);

            var np = math.dot(n, p);

            foreach (var pair in em.EnumerateVertexPairsInHalfEdgeLoop(first))
            {
                var a = new float2(pair.Prev.X, pair.Prev.Z);
                var b = new float2(pair.Next.X, pair.Next.Z);

                var diff = b - a;

                var t = (np - math.dot(n, a)) / math.dot(n, diff);
                var intersection = a + diff * t;

                if (t >= 0f && t <= 1f && intersection.x >= p.x)
                {
                    winding += Math.Sign(a.y - b.y);
                }
            }

            return winding != 0;
        }

        [ThreadStatic] private static Dictionary<Entity, Entity> _sRoomHalfEdges;

        public static bool FindClosestFloorCeiling(this EntityManager em, Entity level, float3 localPos,
            out Entity outFloorCeiling, out float3 outClosestPoint)
        {
            if (_sRoomHalfEdges == null)
            {
                _sRoomHalfEdges = new Dictionary<Entity, Entity>();
            }

            var withinLevel = em.GetWithinLevel(level);

            _sRoomHalfEdges.Clear();

            _sHalfEdgesQuery.SetSharedComponentFilter(withinLevel);
            using (var halfEdges = _sHalfEdgesQuery.ToComponentDataArray<HalfEdge>(Allocator.TempJob))
            {
                foreach (var halfEdge in halfEdges)
                {
                    if (_sRoomHalfEdges.ContainsKey(halfEdge.Room))
                    {
                        continue;
                    }

                    _sRoomHalfEdges.Add(halfEdge.Room, halfEdge.Next);
                }
            }

            var bestDist2 = float.PositiveInfinity;

            outFloorCeiling = Entity.Null;
            outClosestPoint = localPos;

            _sRoomsQuery.SetSharedComponentFilter(withinLevel);
            using (var roomEntities = _sRoomsQuery.ToEntityArray(Allocator.TempJob))
            {
                foreach (var roomEntity in roomEntities)
                {
                    var firstHalfEdgeEntity = _sRoomHalfEdges[roomEntity];
                    var roomData = em.GetComponentData<Room>(roomEntity);

                    if (roomData.Floor != Entity.Null)
                    {
                        var floor = em.GetComponentData<FloorCeiling>(roomData.Floor);
                        var planePos = floor.Plane.GetClosestPoint(localPos);
                        var dist2 = math.distancesq(planePos, localPos);

                        if (dist2 < bestDist2 && em.IsPointWithinHalfEdgeLoop(firstHalfEdgeEntity, planePos))
                        {
                            bestDist2 = dist2;
                            outFloorCeiling = roomData.Floor;
                            outClosestPoint = planePos;
                        }
                    }

                    if (roomData.Ceiling != Entity.Null)
                    {
                        var ceiling = em.GetComponentData<FloorCeiling>(roomData.Ceiling);
                        var planePos = ceiling.Plane.GetClosestPoint(localPos);
                        var dist2 = math.distancesq(planePos, localPos);

                        if (dist2 < bestDist2 && em.IsPointWithinHalfEdgeLoop(firstHalfEdgeEntity, planePos))
                        {
                            bestDist2 = dist2;
                            outFloorCeiling = roomData.Ceiling;
                            outClosestPoint = planePos;
                        }
                    }
                }
            }

            return outFloorCeiling != Entity.Null;
        }
    }
}
