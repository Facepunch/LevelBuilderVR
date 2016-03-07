using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LevelBuilder.Geometry
{
    [ExecuteInEditMode]
    public class Wall : LevelObject
    {
        public static Wall Create(Room room, Corner left, Corner right)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/Wall");
            var inst = Instantiate(prefab).GetComponent<Wall>();

            inst.transform.SetParent(room.transform, false);

            inst._room = room;
            inst.Left = left;
            inst.Right = right;

            inst.Invalidate();

            return inst;
        }

        [SerializeField, HideInInspector]
        private Room _room;

        [SerializeField, HideInInspector]
        private Vector3 _normal;

        [Range(1f / 16f, 0.5f)]
        [DataMember(Name = "thickness")]
        public float Thickness = 0.125f;

        private Vector3 _oldLeftPos;
        private Vector3 _oldRightPos;
        private float _oldThickness;

        public Corner Left;
        public Corner Right;

        [DataMember(Name = "opening_bottom")]
        public float OpeningBottom;
        [DataMember(Name = "opening_top")]
        public float OpeningTop;

        public Vector3 Normal { get { return _normal; } }

        public Room Room { get { return _room; } }

        public Wall LeftNeighbour { get; private set; }
        public Wall RightNeighbour { get; private set; }

        public Wall Opposite { get; private set; }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (Left == null || Right == null) return;

            if (!_oldLeftPos.Equals(Left.transform.position) ||
                !_oldRightPos.Equals(Right.transform.position)) Invalidate();

            _oldLeftPos = Left.transform.position;
            _oldRightPos = Right.transform.position;
        }

        private Wall FindNeighbour(IEnumerable<Wall> walls, bool left)
        {
            return walls.FirstOrDefault(x => left ? x.Right == Left : x.Left == Right);
        }

        protected override void OnRefresh()
        {
            if (_room == null || _room.transform != transform.parent)
            {
                _room = transform.parent.GetComponent<Room>();
            }

            if (Left != null && Right != null)
            {
                var pos = (Left.transform.position + Right.transform.position) * 0.5f;
                transform.position = new Vector3(pos.x, Room.transform.position.y + Room.Height*0.5f, pos.z);

                if (_room != null)
                {
                    LeftNeighbour = FindNeighbour(Room.Walls, true);
                    RightNeighbour = FindNeighbour(Room.Walls, false);
                }

                _normal = Vector3.Cross(Vector3.up, Right.transform.position - Left.transform.position).normalized;

                Opposite = Find<Wall>().FirstOrDefault(x => x.Left == Right && x.Right == Left);
            }
        }

        public bool GetIntersections(out Vector3 left, out Vector3 right)
        {
            left = right = default(Vector3);

            if (_room == null || LeftNeighbour == null || RightNeighbour == null) return false;

            var bottom = _room.transform.position.y;

            var midLeft = Helper.SwizzleXz(LeftNeighbour.transform.position + LeftNeighbour.Normal * LeftNeighbour.Thickness);
            var midThis = Helper.SwizzleXz(transform.position + Normal * Thickness);
            var midRight = Helper.SwizzleXz(RightNeighbour.transform.position + RightNeighbour.Normal * RightNeighbour.Thickness);

            var tanLeft = Helper.SwizzleXz(LeftNeighbour.Right.transform.position - LeftNeighbour.Left.transform.position);
            var tanThis = Helper.SwizzleXz(Right.transform.position - Left.transform.position);
            var tanRight = Helper.SwizzleXz(RightNeighbour.Right.transform.position - RightNeighbour.Left.transform.position);

            Vector2 joinLeft, joinRight;
            if (!Helper.GetLineIntersection(midLeft, tanLeft, midThis, tanThis, out joinLeft))
            {
                joinLeft = Helper.SwizzleXz(Left.transform.position + (_normal * Thickness + LeftNeighbour.Normal * LeftNeighbour.Thickness) * 0.5f);
            }

            if (!Helper.GetLineIntersection(midThis, tanThis, midRight, tanRight, out joinRight))
            {
                joinRight = Helper.SwizzleXz(Right.transform.position + (_normal * Thickness + RightNeighbour.Normal * RightNeighbour.Thickness) * 0.5f);
            }

            left = new Vector3(joinLeft.x, bottom, joinLeft.y);
            right = new Vector3(joinRight.x, bottom, joinRight.y);

            return true;
        }

        protected override JToken OnSerialize(JsonSerializer serializer)
        {
            var token = (JObject) base.OnSerialize(serializer);

            token.Add("left", Left.Guid.ToString());
            token.Add("right", Right.Guid.ToString());

            return token;
        }
    }
}
