using System;
using System.Collections;
using System.Collections.Generic;

namespace LevelBuilder.Geometry
{
    internal class DynamicArray<T> : IEnumerable<T>
        where T : struct
    {
        private const int DefaultSize = 8;
        private const int MaxPoolCount = 1024;

        [ThreadStatic] private static List<Queue<T[]>> _sPool;

        private static int GetShift(int size)
        {
            var shift = -1;
            while (1 << ++shift < size) ;
            return shift;
        }

        private static T[] CreateArray(int size)
        {
            var shift = GetShift(size);

            if (_sPool == null || _sPool.Count <= shift) return new T[1 << shift];

            var queue = _sPool[shift];
            return queue.Count == 0 ? new T[1 << shift] : queue.Dequeue();
        }

        private static void PoolArray(T[] array)
        {
            var shift = GetShift(array.Length);

            if (_sPool == null) _sPool = new List<Queue<T[]>>();

            while (_sPool.Count <= shift) _sPool.Add(new Queue<T[]>());

            if (_sPool[shift].Count > MaxPoolCount) return;
            _sPool[shift].Enqueue(array);
        }

        private T[] _array;

        public int Count { get; private set; }

        public DynamicArray()
        {
            _array = CreateArray(DefaultSize);
        }

        public T[] GetArray()
        {
            return _array;
        }

        private void SetCapacity(int count)
        {
            if (_array.Length >= count) return;

            var shift = GetShift(count);
            var newArray = CreateArray(1 << shift);
            Array.Copy(_array, newArray, Count);
            PoolArray(_array);
            _array = newArray;
        }

        public void Add(T value)
        {
            if (_array.Length == Count) SetCapacity(Count << 1);
            _array[Count++] = value;
        }

        public void Clear()
        {
            Count = 0;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < Count; ++i)
            {
                yield return _array[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            return string.Format("DynamicArray<{0}>[{1}]", typeof (T).Name, Count);
        }
    }
}
