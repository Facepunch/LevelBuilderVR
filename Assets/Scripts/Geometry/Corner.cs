using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LevelBuilder.Geometry
{
    [ExecuteInEditMode, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Corner : LevelObject
    {
        public static Corner Create(Level level, Vector3 pos)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/Corner");
            var inst = Instantiate(prefab).GetComponent<Corner>();

            inst.transform.SetParent(level.CornerParent, false);
            inst.transform.position = new Vector3(pos.x, level.transform.position.y, pos.z);

            return inst;
        }

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        
        protected override void OnStart()
        {
            base.OnStart();

            _meshFilter = _meshFilter ?? GetComponent<MeshFilter>();
            _meshRenderer = _meshRenderer ?? GetComponent<MeshRenderer>();

            transform.localScale = new Vector3(0.125f, 4f, 0.125f);
        }

        public bool TryMergeFrom(Corner other)
        {
            var mustSplit = true;
            var thisRooms = new HashSet<Room>();
            var thatRooms = new HashSet<Room>();

            foreach (var wall in Find<Wall>())
            {
                if (wall.Left == this && wall.Right == other ||
                    wall.Right == this && wall.Left == other) mustSplit = false;

                if (wall.Left == this || wall.Right == this) thisRooms.Add(wall.Room);
                if (wall.Left == other || wall.Right == other) thatRooms.Add(wall.Room);
            }

            if (mustSplit && thisRooms.Intersect(thatRooms).Any())
            {
                thisRooms.Intersect(thatRooms).First().Split(this, other);
                return false;
            }

            foreach (var wall in Find<Wall>())
            {
                if (wall.Left == other)
                {
                    wall.Left = this;
                    wall.Invalidate();
                }

                if (wall.Right == other)
                {
                    wall.Right = this;
                    wall.Invalidate();
                }

                if (wall.Left == wall.Right)
                {
                    wall.Room.Invalidate();
                    Destroy(wall.gameObject);
                }
            }
            
            return true;
        }

        protected override JToken OnSerialize(JsonSerializer serializer)
        {
            var token = (JObject) base.OnSerialize(serializer);

            token.Add("x", transform.position.x);
            token.Add("z", transform.position.z);

            return token;
        }

        protected override void OnDeserialize(JToken token, JsonSerializer serializer)
        {
            base.OnDeserialize(token, serializer);

            transform.position = new Vector3((float) token["x"], transform.position.y, (float) token["z"]);
        }
    }
}
