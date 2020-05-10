using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LevelBuilderVR.Behaviours;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Valve.Newtonsoft.Json;
using Valve.Newtonsoft.Json.Linq;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace LevelBuilderVR.Entities
{ 
    public static class Geometry
    {
        private static EntityArchetype _sLevelArchetype;
        private static EntityArchetype _sRoomArchetype;
        private static EntityArchetype _sHalfEdgeArchetype;
        private static EntityArchetype _sVertexArchetype;

        private static HybridLevel _sHybridLevel;
        private static EntityQuery _sSelectedQuery;

        private static EntityQuery _sRoomsQuery;
        private static EntityQuery _sHalfEdgesQuery;
        private static EntityQuery _sVerticesQuery;

        private static HybridLevel HybridLevel => _sHybridLevel ?? (_sHybridLevel = Object.FindObjectOfType<HybridLevel>());

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeScene()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            _sLevelArchetype = em.CreateArchetype(
                typeof(Identifier),
                typeof(Level),
                typeof(WithinLevel),
                typeof(WidgetsVisible),
                typeof(LocalToWorld),
                typeof(WorldToLocal));

            _sRoomArchetype = em.CreateArchetype(
                typeof(Identifier),
                typeof(Room),
                typeof(WithinLevel),
                typeof(RenderMesh),
                typeof(LocalToWorld),
                typeof(RenderBounds));

            _sHalfEdgeArchetype = em.CreateArchetype(
                typeof(Identifier),
                typeof(HalfEdge),
                typeof(WithinLevel));

            _sVertexArchetype = em.CreateArchetype(
                typeof(Identifier),
                typeof(Vertex),
                typeof(WithinLevel));

            _sSelectedQuery = em.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] {ComponentType.ReadOnly<Selected>()}
                });

            _sRoomsQuery = em.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<Room>(), ComponentType.ReadOnly<WithinLevel>() }
                });

            _sHalfEdgesQuery = em.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<HalfEdge>(), ComponentType.ReadOnly<WithinLevel>() }
                });

            _sVerticesQuery = em.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] {ComponentType.ReadOnly<Vertex>(), ComponentType.ReadOnly<WithinLevel>() },
                    None = new [] {ComponentType.ReadOnly<Virtual>() }
                });
        }

        public static Entity CreateLevelTemplate(this EntityManager em, float3 size)
        {
            var level = em.CreateLevel();

            var room = em.CreateRoom(level, 0f, size.y);

            var halfWidth = size.x * 0.5f;
            var halfDepth = size.z * 0.5f;

            var prev = em.CreateHalfEdge(room, em.CreateVertex(level, -halfWidth, -halfDepth));
            prev = em.InsertHalfEdge(prev, em.CreateVertex(level, -halfWidth, halfDepth));
            prev = em.InsertHalfEdge(prev, em.CreateVertex(level, halfWidth, halfDepth));
            prev = em.InsertHalfEdge(prev, em.CreateVertex(level, halfWidth, -halfDepth));

            return level;
        }

        private static void AssignNewIdentifier(this EntityManager em, Entity entity)
        {
            em.SetComponentData(entity, new Identifier(Guid.NewGuid()));
        }

        private static void AssignNewIdentifier(this EntityCommandBuffer cmb, Entity entity)
        {
            cmb.SetComponent(entity, new Identifier(Guid.NewGuid()));
        }

        public static void SetWithinLevel(this EntityManager em, Entity entity, Entity level)
        {
            em.SetSharedComponentData(entity, em.GetWithinLevel(level));
        }

        public static WithinLevel GetWithinLevel(this EntityManager em, Entity level)
        {
            return new WithinLevel(em.GetComponentData<Identifier>(level));
        }

        public static Entity CreateLevel(this EntityManager em, Guid? guid = null)
        {
            var level = em.CreateEntity(_sLevelArchetype);

            if (guid == null)
            {
                em.AssignNewIdentifier(level);
            }
            else
            {
                em.SetComponentData(level, new Identifier(guid.Value));
            }

            em.SetComponentData(level, new LocalToWorld
            {
                Value = float4x4.identity
            });

            em.SetComponentData(level, new WorldToLocal
            {
                Value = float4x4.identity
            });

            em.SetWithinLevel(level, level);

            return level;
        }

        public static Entity CreateRoom(this EntityManager em, Entity level, float? floor = null, float? ceiling = null, Guid? guid = null)
        {
            var room = em.CreateEntity(_sRoomArchetype);

            if (guid == null)
            {
                em.AssignNewIdentifier(room);
            }
            else
            {
                em.SetComponentData(room, new Identifier(guid.Value));
            }

            em.SetWithinLevel(room, level);

            if (floor.HasValue)
            {
                em.AddComponentData(room, new FlatFloor
                {
                    Y = floor.Value
                });
            }

            if (ceiling.HasValue)
            {
                em.AddComponentData(room, new FlatCeiling
                {
                    Y = ceiling.Value
                });
            }

            em.SetupRoomRendering(room);

            return room;
        }

        public static void SetupRoomRendering(this EntityManager em, Entity room)
        {
            em.SetComponentData(room, new LocalToWorld
            {
                Value = float4x4.identity
            });

            var mesh = new Mesh();
            var material = Object.Instantiate(HybridLevel.Material);

            material.color = Color.HSVToRGB(Random.value, 0.125f, 1f);

            mesh.MarkDynamic();

            em.SetSharedComponentData(room, new RenderMesh
            {
                mesh = mesh,
                material = material,
                castShadows = ShadowCastingMode.Off,
                receiveShadows = true
            });

            em.AddComponent<DirtyMesh>(room);
        }

        public static Entity CopyRoom(this EntityManager em, Entity oldRoom)
        {
            var room = em.CreateEntity(_sRoomArchetype);

            em.AssignNewIdentifier(room);

            em.SetSharedComponentData(room, em.GetSharedComponentData<WithinLevel>(oldRoom));

            if (em.HasComponent<FlatFloor>(oldRoom))
            {
                em.AddComponentData(room, em.GetComponentData<FlatFloor>(oldRoom));
            }
            else if (em.HasComponent<SlopedFloor>(oldRoom))
            {
                em.AddComponentData(room, em.GetComponentData<SlopedFloor>(oldRoom));
            }

            if (em.HasComponent<FlatCeiling>(oldRoom))
            {
                em.AddComponentData(room, em.GetComponentData<FlatCeiling>(oldRoom));
            }
            else if (em.HasComponent<SlopedCeiling>(oldRoom))
            {
                em.AddComponentData(room, em.GetComponentData<SlopedCeiling>(oldRoom));
            }

            em.SetupRoomRendering(room);

            return room;
        }

        public static Entity CreateHalfEdge(this EntityManager em, Entity room, Entity vertex, Guid? guid = null)
        {
            var halfEdge = em.CreateEntity(_sHalfEdgeArchetype);

            if (guid == null)
            {
                em.AssignNewIdentifier(halfEdge);
            }
            else
            {
                em.SetComponentData(halfEdge, new Identifier(guid.Value));
            }

            var withinLevelData = em.GetSharedComponentData<WithinLevel>(room);

            em.SetSharedComponentData(halfEdge, withinLevelData);
            em.SetComponentData(halfEdge, new HalfEdge
            {
                Room = room,
                Vertex = vertex,
                Next = halfEdge
            });

            em.AddComponent<DirtyMesh>(room);

            return halfEdge;
        }

        public static Entity InsertHalfEdge(this EntityManager em, Entity prev, Entity vertex)
        {
            var hePrev = em.GetComponentData<HalfEdge>(prev);
            var entNew = em.CreateHalfEdge(hePrev.Room, vertex);

            var entNext = hePrev.Next;
            hePrev.Next = entNew;

            em.SetComponentData(entNew, new HalfEdge
            {
                Room = hePrev.Room,
                Vertex = vertex,
                Next = entNext,
                BackFace = hePrev.BackFace
            });

            if (hePrev.BackFace == Entity.Null)
            {
                em.SetComponentData(prev, hePrev);
                return entNew;
            }

            // Back face

            var entBackPrev = hePrev.BackFace;
            var heBackPrev = em.GetComponentData<HalfEdge>(entBackPrev);
            var entBackNew = em.CreateHalfEdge(heBackPrev.Room, vertex);

            var entBackNext = heBackPrev.Next;
            heBackPrev.Next = entBackNew;
            hePrev.BackFace = entBackNew;

            em.SetComponentData(prev, hePrev);
            em.SetComponentData(entBackPrev, heBackPrev);

            em.SetComponentData(entBackNew, new HalfEdge
            {
                Room = heBackPrev.Room,
                Vertex = vertex,
                Next = entBackNext,
                BackFace = prev
            });

            return entNew;
        }

        public static Entity CreateVertex(this EntityManager em, Entity level, float x, float z, Guid? guid = null)
        {
            var vertex = em.CreateEntity(_sVertexArchetype);

            if (guid == null)
            {
                em.AssignNewIdentifier(vertex);
            }
            else
            {
                em.SetComponentData(vertex, new Identifier(guid.Value));
            }

            em.SetWithinLevel(vertex, level);

            em.SetComponentData(vertex, new Vertex
            {
                X = x,
                Z = z
            });

            return vertex;
        }

        private static bool SetMaterialFlag<TFlag>(this EntityManager em, Entity entity, bool enabled)
            where TFlag : struct, IComponentData
        {
            var changed = false;

            if (enabled && !em.HasComponent<TFlag>(entity))
            {
                em.AddComponent<TFlag>(entity);
                changed = true;
            }
            else if (!enabled && em.HasComponent<TFlag>(entity))
            {
                em.RemoveComponent<TFlag>(entity);
                changed = true;
            }

            if (changed && em.HasComponent<RenderMesh>(entity))
            {
                em.AddComponent<DirtyMaterial>(entity);
            }

            return changed;
        }

        public static bool SetHovered(this EntityManager em, Entity entity, bool hovered)
        {
            return em.SetMaterialFlag<Hovered>(entity, hovered);
        }

        public static bool GetSelected(this EntityManager em, Entity entity)
        {
            return em.HasComponent<Selected>(entity);
        }

        public static bool SetSelected(this EntityManager em, Entity entity, bool selected)
        {
            return em.SetMaterialFlag<Selected>(entity, selected);
        }

        public static void DeselectAll(this EntityManager em)
        {
            em.AddComponent<DirtyMaterial>(_sSelectedQuery);
            em.RemoveComponent<Selected>(_sSelectedQuery);
        }

        public static void SetVisible(this EntityManager em, Entity entity, bool value)
        {
            if (value)
            {
                em.RemoveComponent<Hidden>(entity);
            }
            else
            {
                em.AddComponent<Hidden>(entity);
            }
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

        private static bool GetFacePlane<TFlat, TSloped>(this EntityManager em, Entity roomEntity, out Plane plane)
            where TFlat : struct, IComponentData, IFlatFace
            where TSloped : struct, IComponentData, ISlopedFace
        {
            if (em.HasComponent<TFlat>(roomEntity))
            {
                var flatFloor = em.GetComponentData<TFlat>(roomEntity);

                plane = new Plane
                {
                    Normal = new float3(0f, 1f, 0f),
                    Point = new float3(0f, flatFloor.Y, 0f)
                };

                return true;
            }

            if (em.HasComponent<TSloped>(roomEntity))
            {
                var slopedFloor = em.GetComponentData<TSloped>(roomEntity);

                var vertex0 = em.GetComponentData<Vertex>(slopedFloor.Anchor0.Vertex);
                var vertex1 = em.GetComponentData<Vertex>(slopedFloor.Anchor1.Vertex);
                var vertex2 = em.GetComponentData<Vertex>(slopedFloor.Anchor2.Vertex);

                var a = new float3(vertex0.X, slopedFloor.Anchor0.Y, vertex0.Z);
                var b = new float3(vertex1.X, slopedFloor.Anchor1.Y, vertex1.Z);
                var c = new float3(vertex2.X, slopedFloor.Anchor2.Y, vertex2.Z);

                var n = math.normalize(math.cross(b - a, c - a));

                plane = new Plane
                {
                    Normal = n.y < 0f ? -n : n,
                    Point = (a + b + c) / 3f
                };

                return true;
            }

            plane = new Plane
            {
                Normal = new float3(0f, 1f, 0f),
                Point = float3.zero
            };

            return false;
        }

        public static HalfLoopVertexPairEnumerable EnumerateVertexPairsInHalfLoop(this EntityManager em, Entity first)
        {
            return new HalfLoopVertexPairEnumerable(em, first);
        }

        public struct HalfLoopVertexPairEnumerable : IEnumerable<VertexPair>
        {
            public struct Enumerator : IEnumerator<VertexPair>
            {
                private readonly EntityManager _em;
                private readonly Entity _entFirst;

                private Entity _entNext;
                private Entity _entLast;
                private bool _continueNext;

                private VertexPair _current;

                public Enumerator(EntityManager em, Entity first)
                {
                    _em = em;
                    _entFirst = first;

                    _entNext = default;
                    _entLast = default;
                    _current = default;
                    _continueNext = default;

                    Reset();
                }

                public void Reset()
                {
                    var heFirst = _em.GetComponentData<HalfEdge>(_entFirst);
                    var vFirst = _em.GetComponentData<Vertex>(heFirst.Vertex);

                    _current = new VertexPair(default, vFirst);
                    _entNext = _entLast = heFirst.Next;
                    _continueNext = true;
                }

                public bool MoveNext()
                {
                    var heNext = _em.GetComponentData<HalfEdge>(_entNext);
                    var vNext = _em.GetComponentData<Vertex>(heNext.Vertex);

                    _current = new VertexPair(_current.Next, vNext);
                    _entNext = heNext.Next;

                    var shouldContinue = _continueNext;
                    _continueNext = _entNext != _entLast;

                    return shouldContinue;
                }

                public VertexPair Current => _current;

                object IEnumerator.Current => Current;

                public void Dispose() { }
            }

            private readonly EntityManager _em;
            private readonly Entity _entFirst;

            public HalfLoopVertexPairEnumerable(EntityManager em, Entity first)
            {
                _em = em;
                _entFirst = first;
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(_em, _entFirst);
            }

            IEnumerator<VertexPair> IEnumerable<VertexPair>.GetEnumerator()
            {
                return GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        public static float2 Get2DCentroidOfEdgeLoop(this EntityManager em, Entity first)
        {
            var signedArea = 0f;
            var totals = new float2(0f, 0f);

            foreach (var pair in em.EnumerateVertexPairsInHalfLoop(first))
            {
                var a = new float2(pair.Prev.X, pair.Prev.Z);
                var b = new float2(pair.Next.X, pair.Next.Z);

                var cross = a.x * b.y - b.x * a.y;

                totals += new float2(a.x + b.x, a.y + b.y) * cross;
                signedArea += cross;
            }

            // NB: signedArea is actually 2x the signed area
            return totals / (3f * signedArea);
        }

        public static bool IsPointWithinHalfEdgeLoop(this EntityManager em, Entity first, float3 localPos)
        {
            var winding = 0;

            var p = new float2(localPos.x, localPos.z);
            var n = new float2(0f, 1f);

            var np = math.dot(n, p);

            foreach (var pair in em.EnumerateVertexPairsInHalfLoop(first))
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
            bool alwaysAtMidpoint, out Entity outRoom, out FaceKind outKind, out float3 outClosestPoint)
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

            outRoom = Entity.Null;
            outKind = FaceKind.None;
            outClosestPoint = localPos;

            var up = new float3(0f, 1f, 0f);

            _sRoomsQuery.SetSharedComponentFilter(withinLevel);
            using (var roomEntities = _sRoomsQuery.ToEntityArray(Allocator.TempJob))
            {
                foreach (var roomEntity in roomEntities)
                {
                    var firstHalfEdgeEntity = _sRoomHalfEdges[roomEntity];
                    var centroid2D = alwaysAtMidpoint
                        ? em.Get2DCentroidOfEdgeLoop(firstHalfEdgeEntity)
                        : default;

                    var centroid = new float3(centroid2D.x, 0f, centroid2D.y);

                    if (em.GetFacePlane<FlatFloor, SlopedFloor>(roomEntity, out var floor))
                    {
                        var planePos = alwaysAtMidpoint
                            ? floor.ProjectOnto(centroid, up)
                            : floor.GetClosestPoint(localPos);
                        var dist2 = math.distancesq(planePos, localPos);

                        if (dist2 < bestDist2 && em.IsPointWithinHalfEdgeLoop(firstHalfEdgeEntity, planePos))
                        {
                            bestDist2 = dist2;
                            outRoom = roomEntity;
                            outKind = FaceKind.Floor;
                            outClosestPoint = planePos;
                        }
                    }

                    if (em.GetFacePlane<FlatCeiling, SlopedCeiling>(roomEntity, out var ceiling))
                    {
                        var planePos = alwaysAtMidpoint
                            ? ceiling.ProjectOnto(centroid, up)
                            : ceiling.GetClosestPoint(localPos);
                        var dist2 = math.distancesq(planePos, localPos);

                        if (dist2 < bestDist2 && em.IsPointWithinHalfEdgeLoop(firstHalfEdgeEntity, planePos))
                        {
                            bestDist2 = dist2;
                            outRoom = roomEntity;
                            outKind = FaceKind.Ceiling;
                            outClosestPoint = planePos;
                        }
                    }
                }
            }

            return outRoom != Entity.Null;
        }

        private static string GetIdentifierString(this EntityManager em, Entity entity)
        {
            if (!em.Exists(entity))
            {
                return null;
            }

            var ident = em.GetComponentData<Identifier>(entity);
            return ident.Guid.ToString();
        }

        public static JObject ToJObject(this EntityManager em, SlopeVertex anchor)
        {
            return new JObject
            {
                { "vertex", em.GetIdentifierString(anchor.Vertex) },
                { "y", anchor.Y }
            };
        }

        public static JObject ToJObject(this EntityManager em, ISlopedFace face)
        {
            return new JObject
            {
                { "a", em.ToJObject(face.Anchor0) },
                { "b", em.ToJObject(face.Anchor1) },
                { "c", em.ToJObject(face.Anchor2) }
            };
        }

        public const int JsonFormatVersion = 1;

        public static void SaveLevel(this EntityManager em, Entity level, TextWriter writer)
        {
            var roomsObj = new JObject();
            var halfEdgesObj = new JObject();
            var verticesObj = new JObject();

            var withinLevel = em.GetWithinLevel(level);

            _sRoomsQuery.SetSharedComponentFilter(withinLevel);
            _sHalfEdgesQuery.SetSharedComponentFilter(withinLevel);
            _sVerticesQuery.SetSharedComponentFilter(withinLevel);

            var rooms = _sRoomsQuery.ToEntityArray(Allocator.TempJob);

            foreach (var roomEnt in rooms)
            {
                var ident = em.GetIdentifierString(roomEnt);
                var roomObj = new JObject();

                if (em.HasComponent<FlatFloor>(roomEnt))
                {
                    var floor = em.GetComponentData<FlatFloor>(roomEnt);

                    roomObj.Add("floor", floor.Y);
                }
                else if (em.HasComponent<SlopedFloor>(roomEnt))
                {
                    var floor = em.GetComponentData<SlopedFloor>(roomEnt);

                    roomObj.Add("floor", em.ToJObject(floor));
                }

                if (em.HasComponent<FlatCeiling>(roomEnt))
                {
                    var ceiling = em.GetComponentData<FlatCeiling>(roomEnt);

                    roomObj.Add("ceiling", ceiling.Y);
                }
                else if (em.HasComponent<SlopedFloor>(roomEnt))
                {
                    var ceiling = em.GetComponentData<SlopedCeiling>(roomEnt);

                    roomObj.Add("ceiling", em.ToJObject(ceiling));
                }

                roomsObj.Add(ident, roomObj);
            }

            rooms.Dispose();

            var halfEdges = _sHalfEdgesQuery.ToEntityArray(Allocator.TempJob);

            foreach (var halfEdgeEnt in halfEdges)
            {
                var ident = em.GetIdentifierString(halfEdgeEnt);
                var halfEdge = em.GetComponentData<HalfEdge>(halfEdgeEnt);

                halfEdgesObj.Add(ident, new JObject
                {
                    { "room", em.GetIdentifierString(halfEdge.Room) },
                    { "vertex", em.GetIdentifierString(halfEdge.Vertex) },
                    { "next", em.GetIdentifierString(halfEdge.Next) },
                    { "backFace", em.GetIdentifierString(halfEdge.BackFace) }
                });
            }

            halfEdges.Dispose();

            var vertices = _sVerticesQuery.ToEntityArray(Allocator.TempJob);

            foreach (var vertexEnt in vertices)
            {
                var ident = em.GetIdentifierString(vertexEnt);
                var vertex = em.GetComponentData<Vertex>(vertexEnt);

                verticesObj.Add(ident, new JObject
                {
                    { "x", vertex.X },
                    { "z", vertex.Z }
                });
            }

            vertices.Dispose();

            var levelData = em.GetComponentData<Level>(level);
            var revision = ++levelData.Revision;
            em.SetComponentData(level, levelData);

            var root = new JObject
            {
                { "formatVersion", JsonFormatVersion },
                { "level", new JObject
                {
                    { "guid", em.GetIdentifierString(level) },
                    { "revision", revision }
                } },
                { "rooms", roomsObj },
                { "halfEdges", halfEdgesObj },
                { "vertices", verticesObj }
            };

            writer.Write(root.ToString(Formatting.Indented));
        }

        private struct EntityJObject
        {
            public readonly Entity Entity;
            public readonly JObject JObject;

            public EntityJObject(Entity entity, JObject jObject)
            {
                Entity = entity;
                JObject = jObject;
            }
        }

        private static Entity FindEntity(this Dictionary<Guid, EntityJObject> dict, JToken jValue)
        {
            if (jValue == null || jValue.Type == JTokenType.Null || jValue.Type == JTokenType.None)
            {
                return Entity.Null;
            }

            return dict[Guid.Parse((string) jValue)].Entity;
        }

        private static SlopeVertex FromJObject(this EntityManager em, Dictionary<Guid, EntityJObject> vertices,
            JObject jObject)
        {
            return new SlopeVertex
            {
                Vertex = vertices.FindEntity(jObject["vertex"]),
                Y = (float) jObject["y"]
            };
        }

        private static void LoadFaceFromJToken<TFlat, TSloped>(this EntityManager em, Dictionary<Guid, EntityJObject> vertices,
            Entity room, JToken jToken)
            where TFlat : struct, IFlatFace, IComponentData
            where TSloped : struct, ISlopedFace, IComponentData
        {
            if (em.HasComponent<TFlat>(room)) em.RemoveComponent<TFlat>(room);
            if (em.HasComponent<TSloped>(room)) em.RemoveComponent<TSloped>(room);

            if (jToken == null) return;

            switch (jToken.Type)
            {
                case JTokenType.Float:
                case JTokenType.Integer:
                    em.AddComponentData(room, new TFlat
                    {
                        Y = (float) jToken
                    });
                    return;
                case JTokenType.Object:
                    em.AddComponentData(room, new TSloped
                    {
                        Anchor0 = em.FromJObject(vertices, (JObject) jToken["a"]),
                        Anchor1 = em.FromJObject(vertices, (JObject) jToken["b"]),
                        Anchor2 = em.FromJObject(vertices, (JObject) jToken["c"])
                    });
                    return;
            }
        }

        public static Entity LoadLevel(this EntityManager em, TextReader reader)
        {
            var root = (JObject) JToken.ReadFrom(new JsonTextReader(reader));

            var version = (int?) root["formatVersion"] ?? 1;
            var levelObj = (JObject) root["level"];
            var roomsObj = (JObject) root["rooms"];
            var halfEdgesObj = (JObject) root["halfEdges"];
            var verticesObj = (JObject) root["vertices"];

            var level = em.CreateLevel(Guid.Parse((string) levelObj["guid"]));

            var levelData = em.GetComponentData<Level>(level);
            levelData.Revision = (uint) levelObj["revision"];
            em.SetComponentData(level, levelData);

            var rooms = new Dictionary<Guid, EntityJObject>();
            var halfEdges = new Dictionary<Guid, EntityJObject>();
            var vertices = new Dictionary<Guid, EntityJObject>();

            // Create entities

            foreach (var property in roomsObj)
            {
                var guid = Guid.Parse(property.Key);
                var room = em.CreateRoom(level, guid: guid);

                rooms.Add(guid, new EntityJObject(room, (JObject) property.Value));
            }

            foreach (var property in halfEdgesObj)
            {
                var guid = Guid.Parse(property.Key);
                var halfEdge = em.CreateHalfEdge(level, Entity.Null, guid);

                halfEdges.Add(guid, new EntityJObject(halfEdge, (JObject)property.Value));
            }

            foreach (var property in verticesObj)
            {
                var guid = Guid.Parse(property.Key);
                var vertex = em.CreateVertex(level, 0f, 0f, guid);

                vertices.Add(guid, new EntityJObject(vertex, (JObject)property.Value));
            }

            // Set component data

            foreach (var pair in rooms.Values)
            {
                em.LoadFaceFromJToken<FlatFloor, SlopedFloor>(vertices, pair.Entity, pair.JObject["floor"]);
                em.LoadFaceFromJToken<FlatCeiling, SlopedCeiling>(vertices, pair.Entity, pair.JObject["ceiling"]);
            }

            foreach (var pair in halfEdges.Values)
            {
                em.SetComponentData(pair.Entity, new HalfEdge
                {
                    Room = rooms.FindEntity(pair.JObject["room"]),
                    Vertex = vertices.FindEntity(pair.JObject["vertex"]),
                    Next = halfEdges.FindEntity(pair.JObject["next"]),
                    BackFace = halfEdges.FindEntity(pair.JObject["backFace"])
                });
            }

            foreach (var pair in vertices.Values)
            {
                em.SetComponentData(pair.Entity, new Vertex
                {
                    X = (float) pair.JObject["x"],
                    Z = (float) pair.JObject["z"]
                });
            }

            return level;
        }
    }
}
