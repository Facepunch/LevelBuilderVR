using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LevelBuilder.Geometry
{
    public abstract class LevelMeshBuilder
    {
        public void BuildLevel(MeshGenerator meshGen, Level level)
        {
            foreach (var room in level.Rooms)
            {
                BuildRoom(meshGen, room);
            }
        }

        public void BuildRoom(MeshGenerator meshGen, Room room)
        {
            OnBuildRoom(meshGen, room);

            foreach (var wall in room.Walls)
            {
                BuildWall(meshGen, wall);
            }
        }

        public void BuildWall(MeshGenerator meshGen, Wall wall)
        {
            OnBuildWall(meshGen, wall);
        }

        protected abstract void OnBuildRoom(MeshGenerator meshGen, Room room);
        protected abstract void OnBuildWall(MeshGenerator meshGen, Wall wall);
    }

    public class EditorMeshBuilder : LevelMeshBuilder
    {
        protected override void OnBuildRoom(MeshGenerator meshGen, Room room)
        {
            var start = room.Walls.FirstOrDefault();
            var wallCount = room.Walls.Count();
            if (start == null) return;

            var wall = start;
            var floorVerts = new List<Vector2>();

            var steps = 0;

            do
            {
                Vector3 left, right;
                wall.GetIntersections(out left, out right);
                
                floorVerts.Add(Helper.SwizzleXz(right));

                wall = wall.RightNeighbour;
            } while (wall != start && wall != null && ++steps < wallCount);

            meshGen.PushSubmesh(1);
            meshGen.AddFloor(Vector3.up, room.transform.position.y, floorVerts);
            meshGen.PopSubmesh();
        }

        protected override void OnBuildWall(MeshGenerator meshGen, Wall wall)
        {
            Vector3 left, right;
            wall.GetIntersections(out left, out right);

            var height = Vector3.up*wall.Room.Height;
            var topLeft = left + height;
            var topRight = right + height;

            var innerLeft = new Vector3(wall.Left.transform.position.x, topLeft.y, wall.Left.transform.position.z);
            var innerRight = new Vector3(wall.Right.transform.position.x, topLeft.y, wall.Right.transform.position.z);

            meshGen.AddWall(left, topRight);
            meshGen.AddFloor(Vector3.up, topLeft.y,
                Helper.SwizzleXz(topLeft),
                Helper.SwizzleXz(innerLeft),
                Helper.SwizzleXz(innerRight),
                Helper.SwizzleXz(topRight));

            if (wall.Opposite != null) return;

            meshGen.AddWall(new Vector3(wall.Right.transform.position.x, left.y, wall.Right.transform.position.z), innerLeft);
        }
    }
}
