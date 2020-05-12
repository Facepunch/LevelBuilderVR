using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Entities;
using Unity.Mathematics;

namespace LevelBuilderVR
{
    public struct Plane : IEquatable<Plane>
    {
        public float3 Point;
        public float3 Normal;

        [Pure]
        public float3 GetClosestPoint(float3 pos)
        {
            return pos - math.dot(pos - Point, Normal) * Normal;
        }

        [Pure]
        public float3 ProjectOnto(float3 pos, float3 dir)
        {
            var t = (math.dot(Normal, Point) - math.dot(Normal, pos)) / math.dot(Normal, dir);
            return pos + dir * t;
        }

        public bool Equals(Plane other)
        {
            return Point.Equals(other.Point) && Normal.Equals(other.Normal);
        }

        public override bool Equals(object obj)
        {
            return obj is Plane other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Point.GetHashCode() * 397) ^ Normal.GetHashCode();
            }
        }
    }

    public struct VertexPair : IEquatable<VertexPair>
    {
        public readonly Vertex Prev;
        public readonly Vertex Next;

        public VertexPair(Vertex prev, Vertex next)
        {
            Prev = prev;
            Next = next;
        }

        public bool Equals(VertexPair other)
        {
            return Prev.Equals(other.Prev) && Next.Equals(other.Next);
        }

        public override bool Equals(object obj)
        {
            return obj is VertexPair other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Prev.GetHashCode() * 397) ^ Next.GetHashCode();
            }
        }
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
}
