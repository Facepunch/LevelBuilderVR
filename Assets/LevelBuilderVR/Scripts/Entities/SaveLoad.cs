using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Valve.Newtonsoft.Json;
using Valve.Newtonsoft.Json.Linq;

namespace LevelBuilderVR.Entities
{
    public static partial class EntityHelper
    {
        public const int JsonFormatVersion = 2;

        private static string GetIdentifierString(this EntityManager em, Entity entity)
        {
            if (!em.Exists(entity))
            {
                return null;
            }

            var ident = em.GetComponentData<Identifier>(entity);
            return ident.Guid.ToString();
        }

        public static void SaveLevel(this EntityManager em, Entity level, TextWriter writer)
        {
            var roomsObj = new JObject();
            var floorCeilingsObj = new JObject();
            var halfEdgesObj = new JObject();
            var verticesObj = new JObject();

            var withinLevel = em.GetWithinLevel(level);

            _sRoomsQuery.SetSharedComponentFilter(withinLevel);
            _sFloorCeilingsQuery.SetSharedComponentFilter(withinLevel);
            _sHalfEdgesQuery.SetSharedComponentFilter(withinLevel);
            _sVerticesQuery.SetSharedComponentFilter(withinLevel);

            var rooms = _sRoomsQuery.ToEntityArray(Allocator.TempJob);

            foreach (var roomEnt in rooms)
            {
                var ident = em.GetIdentifierString(roomEnt);
                var room = em.GetComponentData<Room>(roomEnt);

                roomsObj.Add(ident, new JObject
                {
                    { "floor", em.GetIdentifierString(room.Floor) },
                    { "ceiling", em.GetIdentifierString(room.Ceiling) }
                });
            }

            rooms.Dispose();

            var floorCeilings = _sFloorCeilingsQuery.ToEntityArray(Allocator.TempJob);

            foreach (var floorCeilingEnt in floorCeilings)
            {
                var ident = em.GetIdentifierString(floorCeilingEnt);
                var floorCeiling = em.GetComponentData<FloorCeiling>(floorCeilingEnt);
                var plane = floorCeiling.Plane;

                floorCeilingsObj.Add(ident, new JObject
                {
                    { "plane", new JObject
                    {
                        { "point", new JObject
                        {
                            { "x", plane.Point.x },
                            { "y", plane.Point.y },
                            { "z", plane.Point.z }
                        } },
                        { "normal", new JObject
                        {
                            { "x", plane.Normal.x },
                            { "y", plane.Normal.y },
                            { "z", plane.Normal.z }
                        } }
                    } },
                    { "above", em.GetIdentifierString(floorCeiling.Above) },
                    { "below", em.GetIdentifierString(floorCeiling.Below) }
                });
            }

            floorCeilings.Dispose();

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
                { "floorCeilings", floorCeilingsObj },
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

        private static Entity FindEntity(this Dictionary<Guid, EntityJObject> dict, JToken token)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.None)
            {
                return Entity.Null;
            }

            return dict[Guid.Parse((string)token)].Entity;
        }

        public static Entity LoadLevel(this EntityManager em, TextReader reader)
        {
            var root = (JObject)JToken.ReadFrom(new JsonTextReader(reader));

            var version = (int?)root["formatVersion"] ?? 1;
            var levelObj = (JObject)root["level"];
            var roomsObj = (JObject)root["rooms"];
            var floorCeilingsObj = version >= 2 ? (JObject)root["floorCeilings"] : new JObject();
            var halfEdgesObj = (JObject)root["halfEdges"];
            var verticesObj = (JObject)root["vertices"];

            var level = em.CreateLevel(Guid.Parse((string)levelObj["guid"]));

            var levelData = em.GetComponentData<Level>(level);
            levelData.Revision = (uint)levelObj["revision"];
            em.SetComponentData(level, levelData);

            var rooms = new Dictionary<Guid, EntityJObject>();
            var floorCeilings = new Dictionary<Guid, EntityJObject>();
            var halfEdges = new Dictionary<Guid, EntityJObject>();
            var vertices = new Dictionary<Guid, EntityJObject>();

            // Create entities

            foreach (var property in roomsObj)
            {
                var guid = Guid.Parse(property.Key);
                var room = em.CreateRoom(level, guid: guid);

                rooms.Add(guid, new EntityJObject(room, (JObject)property.Value));
            }

            foreach (var property in floorCeilingsObj)
            {
                var guid = Guid.Parse(property.Key);
                var floorCeiling = em.CreateFloorCeiling(level, 0f, guid: guid);

                floorCeilings.Add(guid, new EntityJObject(floorCeiling, (JObject)property.Value));
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
                if (version >= 2)
                {
                    em.SetComponentData(pair.Entity, new Room
                    {
                        Floor = floorCeilings.FindEntity(pair.JObject["floor"]),
                        Ceiling = floorCeilings.FindEntity(pair.JObject["ceiling"]),
                    });
                }
                else
                {
                    em.SetComponentData(pair.Entity, new Room
                    {
                        Floor = em.CreateFloorCeiling(level, (float)pair.JObject["floor"], above: pair.Entity),
                        Ceiling = em.CreateFloorCeiling(level, (float)pair.JObject["ceiling"], below: pair.Entity)
                    });
                }
            }

            foreach (var pair in floorCeilings.Values)
            {
                var point = pair.JObject["plane"]["point"];
                var normal = pair.JObject["plane"]["normal"];

                em.SetComponentData(pair.Entity, new FloorCeiling
                {
                    Plane = new Plane
                    {
                        Point = new float3((float)point["x"], (float)point["y"], (float)point["z"]),
                        Normal = new float3((float)normal["x"], (float)normal["y"], (float)normal["z"])
                    },
                    Above = rooms.FindEntity(pair.JObject["above"]),
                    Below = rooms.FindEntity(pair.JObject["below"]),
                });
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
                    X = (float)pair.JObject["x"],
                    Z = (float)pair.JObject["z"]
                });
            }

            return level;
        }
    }
}
