using System;
using System.Collections.Generic;
using LevelBuilderVR.Behaviours;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Valve.VR.InteractionSystem;

namespace LevelBuilderVR
{
    public static class Helpers
    {
        public static bool TryGetPointerPosition(this Hand hand, out Vector3 worldPos)
        {
            if (hand.isActive && hand.mainRenderModel != null && hand.currentAttachedObject == null)
            {
                try
                {
                    worldPos = hand.mainRenderModel.GetControllerPosition(hand.controllerHoverComponent) + hand.transform.forward * 0.02f;
                    return true;
                }
                catch
                {
                    worldPos = hand.transform.position;
                    return false;
                }
            }

            worldPos = hand.transform.position;
            return false;
        }

        public static void ResetCrosshairTexture(this Hand hand)
        {
            hand.GetComponentInChildren<Crosshair>().ResetTexture();
        }

        public static void SetCrosshairTexture(this Hand hand, Texture2D texture)
        {
            hand.GetComponentInChildren<Crosshair>().SetTexture(texture);
        }

        public static float Cross(float2 a, float2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        public static int GetIndexCount(int vertexCount)
        {
            return (vertexCount - 2) * 3;
        }

        private struct IndexScore
        {
            public int PrevIndex;
            public int CurrIndex;
            public int NextIndex;

            public float Score;
        }

        [ThreadStatic]
        private static List<IndexScore> _sTempIndexScores;

        private static void CalculateScore(NativeArray<float2> vertices, ref IndexScore indexScore)
        {
            var prev = vertices[indexScore.PrevIndex];
            var curr = vertices[indexScore.CurrIndex];
            var next = vertices[indexScore.NextIndex];

            indexScore.Score = Cross(next - curr, curr - prev);
        }

        public static void Triangulate(NativeArray<float2> vertices, NativeArray<int> outIndices, out int outIndexCount)
        {
            outIndexCount = 0;

            if (vertices.Length < 3)
            {
                return;
            }

            var open = _sTempIndexScores ?? (_sTempIndexScores = new List<IndexScore>());

            open.Clear();

            var indexScore = new IndexScore
            {
                PrevIndex = vertices.Length - 2,
                CurrIndex = vertices.Length - 1
            };

            for (var i = 0; i < vertices.Length; ++i)
            {
                indexScore.NextIndex = i;

                CalculateScore(vertices, ref indexScore);

                open.Add(indexScore);

                indexScore.PrevIndex = indexScore.CurrIndex;
                indexScore.CurrIndex = indexScore.NextIndex;
            }

            // Ear clipping

            while (open.Count > 2)
            {
                var bestScore = float.PositiveInfinity;
                var bestIndex = -1;

                for (var i = 0; i < open.Count; ++i)
                {
                    var score = open[i].Score;

                    if (score > 0f && score < bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                if (bestIndex == -1)
                {
                    break;
                }

                var best = open[bestIndex];

                outIndices[outIndexCount++] = best.PrevIndex;
                outIndices[outIndexCount++] = best.CurrIndex;
                outIndices[outIndexCount++] = best.NextIndex;

                var prevIndex = (bestIndex - 1 + open.Count) % open.Count;
                var nextIndex = (bestIndex + 1) % open.Count;

                var prev = open[prevIndex];
                var next = open[nextIndex];

                prev.NextIndex = best.NextIndex;
                next.PrevIndex = best.PrevIndex;

                CalculateScore(vertices, ref prev);
                CalculateScore(vertices, ref next);

                open[prevIndex] = prev;
                open[nextIndex] = next;

                open.RemoveAt(bestIndex);
            }

            open.Clear();
        }
    }
}
