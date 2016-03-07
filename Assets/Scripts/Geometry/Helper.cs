using System;
using System.Collections.Generic;
using UnityEngine;

namespace LevelBuilder.Geometry
{
    public static class Helper
    {
        public static float Cross(Vector2 a, Vector2 b)
        {
            return a.x*b.y - a.y*b.x;
        }

        /// <remarks>
        /// Note: <paramref name="tanA"/> should point towards
        /// the intersection, and <paramref name="tanB"/> away
        /// from it.
        /// </remarks>
        public static bool GetLineIntersection(Vector2 midA, Vector2 tanA, Vector2 midB, Vector2 tanB, out Vector2 pos)
        {
            pos = default(Vector3);

            var cross = Cross(tanA, tanB);
            if (Math.Abs(cross) < float.Epsilon) return false;

            var t = Cross(midB - midA, tanA/cross);

            pos = midB + tanB*t;

            return Vector3.Dot(pos - midA, tanA) >= 0f && Vector3.Dot(midB - pos, tanB) >= 0f;
        }

        public static Vector2 SwizzleXz(Vector3 vec)
        {
            return new Vector2(vec.x, vec.z);
        }

        public static bool LinesCross(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
        {
            var adx = a1.x - a0.x; var ady = a1.y - a0.y;
            var bdx = b1.x - b0.x; var bdy = b1.y - b0.y;
            var dx = b0.x - a0.x;
            var dy = b0.y - a0.y;

            var denom = adx * bdy - ady * bdx;

            if (denom <= float.Epsilon) return false;

            var s = (dx * bdy - dy * bdx) / denom;
            var t = (dx * ady - dy * adx) / denom;

            return s >= 0 && s <= 1 && t >= 0 && t <= 1;
        }

        public static bool AnyLinesCross(Vector2 a, Vector2 b, IList<Vector2> points)
        {
            for (var i = points.Count - 2; i >= 0; --i)
            {
                var c = points[i];
                var d = points[i + 1];

                if (c == a || c == b || d == a || d == b) continue;
                if (LinesCross(a, b, c, d)) return true;
            }

            return false;
        }

        public static bool IsReflex(Vector2 a, Vector2 b, Vector2 c)
        {
            var ab = b - a;
            var bc = c - b;

            return Cross(ab, bc) >= 0f;
        }

        public delegate void TriangleFunc(Vector2 a, Vector2 b, Vector2 c);

        private static void Triangulate(IList<Vector2> points, List<int> indices, TriangleFunc triFunc)
        {
            var ai = indices[0];
            var a = points[ai];
            for (var i = 1; i < indices.Count - 1; ++i)
            {
                var bi = indices[i];
                var ci = indices[i + 1];

                var b = points[bi];
                var c = points[ci];

                if (!IsReflex(a, b, c) && !AnyLinesCross(a, c, points)) continue;

                var split = new List<int> {bi, ci};
                for (var jo = 2; jo < indices.Count - 1; ++jo)
                {
                    var di = indices[(i + jo)%indices.Count];
                    var ei = indices[(i + jo + 1)%indices.Count];
                    var d = points[di];
                    var e = points[ei];

                    split.Add(di);

                    if (ei == ai || (!IsReflex(a, b, d) && !IsReflex(a, d, e) && !AnyLinesCross(a, d, points))) break;
                }

                if (split.Count == indices.Count) return;

                for (var j = split.Count - 2; j >= 1; --j)
                {
                    indices.Remove(split[j]);
                }

                Triangulate(points, split, triFunc);
            }

            for (var i = 1; i < indices.Count - 1; ++i)
            {
                triFunc(points[indices[0]], points[indices[i]], points[indices[i + 1]]);
            }
        }

        public static void Triangulate(IList<Vector2> points, TriangleFunc triFunc)
        {
            var indices = new List<int>(points.Count);
            for (var i = 0; i < points.Count; ++i) indices.Add(i);

            Triangulate(points, indices, triFunc);
        }
    }
}
