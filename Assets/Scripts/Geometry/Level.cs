using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LevelBuilder.Geometry
{
    [ExecuteInEditMode]
    public class Level : LevelObject
    {
        [SerializeField, HideInInspector]
        private readonly List<Room> _rooms = new List<Room>();

        public Transform CornerParent;

        public IEnumerable<Room> Rooms { get { return _rooms; } }

        protected override void OnRefresh()
        {
            _rooms.Clear();
            _rooms.AddRange(transform.Cast<Transform>().Select(x => x.GetComponent<Room>()).Where(x => x != null));

            foreach (var room in Rooms)
            {
                if (room.NeedsRefresh) room.Refresh();
            }
        }

        protected override void OnStart()
        {
            base.OnStart();

            Refresh();
        }

        public void Clear()
        {
            foreach (var room in _rooms)
            {
                Destroy(room.gameObject);
            }

            _rooms.Clear();

            foreach (var cornerTransform in CornerParent.Cast<Transform>())
            {
                var corner = cornerTransform.GetComponent<Corner>();
                if (corner == null) continue;

                Destroy(corner.gameObject);
            }
            
            Refresh();
        }

        public Corner GetCorner(Guid guid)
        {
            foreach (var child in CornerParent.Cast<Transform>())
            {
                var corner = child.GetComponent<Corner>();
                if (corner != null && corner.Guid == guid) return corner;
            }

            return null;
        }

        [UsedImplicitly]
        private void Update()
        {
            if (NeedsRefresh) Refresh();
        }

        protected override JToken OnSerialize(JsonSerializer serializer)
        {
            if (NeedsRefresh) Refresh();

            var token = (JObject) base.OnSerialize(serializer);
            var corners = new JArray();
            var rooms = new JArray();

            foreach (var child in CornerParent.Cast<Transform>())
            {
                var corner = child.GetComponent<Corner>();
                if (corner == null) continue;

                corners.Add(corner.Serialize(serializer));
            }

            foreach (var room in Rooms)
            {
                rooms.Add(room.Serialize(serializer));
            }

            token.Add("corners", corners);
            token.Add("rooms", rooms);

            return token;
        }

        protected override void OnDeserialize(JToken token, JsonSerializer serializer)
        {
            Clear();

            base.OnDeserialize(token, serializer);

            var corners = (JArray) token["corners"];
            foreach (var cornerToken in corners)
            {
                var corner = Corner.Create(this, transform.position);
                corner.Deserialize(cornerToken, serializer);
            }

            var rooms = (JArray) token["rooms"];
            foreach (var roomToken in rooms)
            {
                var room = Room.Create(this);
                room.Deserialize(roomToken, serializer);
            }
            
            Refresh();
        }
    }
}