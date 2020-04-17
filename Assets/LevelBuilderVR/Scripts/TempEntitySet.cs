using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Debug = UnityEngine.Debug;

namespace LevelBuilderVR
{
    public enum SetAccess
    {
        Default,
        Enumerate
    }

    public struct TempEntitySet : IDisposable, IEnumerable<Entity>
    {
        [ThreadStatic]
        private static List<HashSet<Entity>> _sSetPool;

        [ThreadStatic]
        private static List<List<Entity>> _sListPool;

        public const int ActiveWarningThreshold = 4;
        public const int MaxPooled = ActiveWarningThreshold;

        private static volatile int _sActive;

        private readonly HashSet<Entity> _set;
        private readonly List<Entity> _list;

        public int Count => _set.Count;

        public TempEntitySet(SetAccess access)
        {
            var active = ++_sActive;

            if (active > ActiveWarningThreshold)
            {
                Debug.LogWarning($"More than {ActiveWarningThreshold} {nameof(TempEntitySet)}s have been created without being disposed, is there a leak?");
            }

            if (_sSetPool != null && _sSetPool.Count > 0)
            {
                _set = _sSetPool[_sSetPool.Count - 1];
                _sSetPool.RemoveAt(_sSetPool.Count - 1);

                _set.Clear();
            }
            else
            {
                _set = new HashSet<Entity>();
            }

            if (access == SetAccess.Enumerate)
            {
                if (_sListPool != null && _sListPool.Count > 0)
                {
                    _list = _sListPool[_sListPool.Count - 1];
                    _sListPool.RemoveAt(_sListPool.Count - 1);

                    _list.Clear();
                }
                else
                {
                    _list = new List<Entity>();
                }
            }
            else
            {
                _list = null;
            }
        }

        private void AssertEnumerable()
        {
            if (_list != null) return;

            throw new InvalidOperationException($"This operation is only valid for {nameof(TempEntitySet)}s " +
                $"created with {nameof(SetAccess)}.{nameof(SetAccess.Enumerate)}.");
        }

        public Entity this[int index]
        {
            get
            {
                AssertEnumerable();
                return _list[index];
            }
        }

        public bool Add(Entity entity)
        {
            if (_set.Add(entity))
            {
                _list?.Add(entity);
                return true;
            }

            return false;
        }

        public int AddRange(EntityQuery query)
        {
            var entities = query.ToEntityArray(Allocator.TempJob);
            var count = AddRange(entities);
            entities.Dispose();

            return count;
        }

        public int AddRange(NativeArray<Entity> entities)
        {
            return AddRange(entities, 0, entities.Length);
        }

        public int AddRange(NativeArray<Entity> entities, int offset, int length)
        {
            var added = 0;
            var end = offset + length;

            for (var i = offset; i < end; ++i)
            {
                added += Add(entities[i]) ? 1 : 0;
            }

            return added;
        }

        public bool Remove(Entity entity)
        {
            if (_set.Remove(entity))
            {
                _list?.Remove(entity);
                return true;
            }

            return false;
        }

        public bool Contains(Entity entity)
        {
            return _set.Contains(entity);
        }

        public void Clear()
        {
            _set.Clear();
            _list?.Clear();
        }

        public void Dispose()
        {
            --_sActive;

            if (_sSetPool == null)
            {
                _sSetPool = new List<HashSet<Entity>>();
            }

            if (_sSetPool.Count < MaxPooled)
            {
                _set.Clear();
                _sSetPool.Add(_set);
            }

            if (_list == null)
            {
                return;
            }

            if (_sListPool == null)
            {
                _sListPool = new List<List<Entity>>();
            }

            _list.Clear();
            _sListPool.Add(_list);
        }

        public List<Entity>.Enumerator GetEnumerator()
        {
            AssertEnumerable();
            return _list.GetEnumerator();
        }

        IEnumerator<Entity> IEnumerable<Entity>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
