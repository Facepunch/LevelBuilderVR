using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LevelBuilder.Geometry
{
    [ExecuteInEditMode]
    [DataContract]
    public abstract class LevelObject : MonoBehaviour
    {
        private static readonly HashSet<LevelObject> _sObjects = new HashSet<LevelObject>();

        public static IEnumerable<T> Find<T>()
            where T : LevelObject
        {
            return _sObjects.OfType<T>();
        }

        [SerializeField, HideInInspector]
        [DataMember(Name = "guid")]
        private string _guid;

        public bool NeedsRefresh { get; private set; }

        public Guid Guid
        {
            get { return _guid == null ? Guid.Empty : new Guid(_guid); }
            private set { _guid = value.ToString(); }
        }

        public void Invalidate()
        {
            NeedsRefresh = true;

            var parent = transform.parent == null ? null : transform.parent.GetComponent<LevelObject>();
            if (parent != null) parent.Invalidate();
        }

        protected virtual void OnRefresh() { }

        public void Refresh()
        {
            NeedsRefresh = false;
            OnRefresh();
        }

        [UsedImplicitly]
        private void Start()
        {
            while (string.IsNullOrEmpty(_guid))
            {
                Guid = Guid.NewGuid();
            }

            _sObjects.Add(this);

            name = string.Format("{0} ({1})", GetType().Name, _guid.Substring(0, 4));

            Invalidate();
            OnStart();
        }

        [UsedImplicitly]
        private void OnValidate()
        {
            Invalidate();
        }

        [UsedImplicitly]
        private void Update()
        {
            OnUpdate();
        }

        [UsedImplicitly]
        private void OnDestroy()
        {
            _sObjects.Remove(this);
        }

        protected virtual void OnStart() { }
        protected virtual void OnUpdate() { }

        public JToken Serialize(JsonSerializer serializer = null)
        {
            return OnSerialize(serializer ?? JsonSerializer.CreateDefault());
        }

        protected virtual JToken OnSerialize(JsonSerializer serializer)
        {
            return JToken.FromObject(this, serializer);
        }

        public void Deserialize(JToken token, JsonSerializer serializer = null)
        {
            Invalidate();
            OnDeserialize(token, serializer ?? JsonSerializer.CreateDefault());
        }

        protected virtual void OnDeserialize(JToken token, JsonSerializer serializer)
        {
            using (var reader = new JTokenReader(token))
            {
                serializer.Populate(reader, this);
            }
        }
    }
}
