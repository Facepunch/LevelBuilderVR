using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LevelBuilder.Geometry
{
    [ExecuteInEditMode]
    public class Room : LevelObject
    {
        public static Room Create(Level level)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/Room");
            var inst = Instantiate(prefab).GetComponent<Room>();

            inst.transform.SetParent(level.transform, false);

            inst._level = level;

            return inst;
        }

        [SerializeField, HideInInspector]
        private Level _level;

        [SerializeField, HideInInspector]
        private readonly List<Wall> _walls = new List<Wall>();

        public IEnumerable<Wall> Walls { get { return _walls; } }

        public Level Level { get { return _level; } }

        [Range(1f, 5f)]
        [DataMember(Name = "height")]
        public float Height = 2.5f;
        
        protected override void OnRefresh()
        {
            if (transform.parent == null) return;

            if (_level == null || _level.transform != transform.parent)
            {
                _level = transform.parent.GetComponent<Level>();
            }

            _walls.Clear();
            _walls.AddRange(transform.Cast<Transform>().Select(x => x.GetComponent<Wall>()).Where(x => x != null));

            if (_walls.Count > 0)
            {
                var y = transform.position.y;
                var mid = _walls.Aggregate(Vector3.zero, (s, x) => x.transform.position + s) / _walls.Count;

                transform.position = new Vector3(mid.x, y, mid.z);
            }

            foreach (var wall in Walls)
            {
                wall.Refresh();
            }

            RebuildMesh();
        }

        public void Split(Corner a, Corner b)
        {
            var toSplit = new List<Wall>();

            var start = Walls.FirstOrDefault(x => x.Left == a);
            if (start == null) return;

            var curWall = start;
            do
            {
                toSplit.Add(curWall);
                curWall = curWall.RightNeighbour;
            } while (curWall != null && curWall.Left != b);

            var newRoom = Create(Level);
            newRoom.Height = Height;

            foreach (var wall in toSplit)
            {
                _walls.Remove(wall);
                wall.transform.SetParent(newRoom.transform, true);
            }

            Wall.Create(this, a, b);
            Wall.Create(newRoom, b, a);
            
            Invalidate();
            newRoom.Invalidate();
        }

        private void Awake()
        {
            OnRefresh();
            RebuildMesh();
        }

        private void RebuildMesh()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) return;

            using (var meshGen = MeshGenerator.Create())
            {
                var builder = new EditorMeshBuilder();

                meshGen.PushOffset(-transform.position);
                builder.BuildRoom(meshGen, this);
                meshGen.PopOffset();

                if (meshFilter.sharedMesh == null) meshFilter.sharedMesh = new Mesh();

                meshGen.CopyToMesh(meshFilter.sharedMesh);
            }
        }

        protected override JToken OnSerialize(JsonSerializer serializer)
        {
            var token = (JObject) base.OnSerialize(serializer);
            var walls = new JArray();

            foreach (var wall in Walls)
            {
                walls.Add(wall.Serialize(serializer));
            }

            token.Add("walls", walls);

            return token;
        }

        protected override void OnDeserialize(JToken token, JsonSerializer serializer)
        {
            base.OnDeserialize(token, serializer);

            var walls = (JArray) token["walls"];
            foreach (var wallToken in walls)
            {
                var left = Level.GetCorner(new Guid((string) wallToken["left"]));
                var right = Level.GetCorner(new Guid((string) wallToken["right"]));

                if (left == null || right == null)
                {
                    Debug.LogErrorFormat("Failed to deserialize wall {0}", wallToken["guid"]);
                    continue;
                }

                Wall.Create(this, left, right).Deserialize(wallToken, serializer);
            }
        }
    }
}
