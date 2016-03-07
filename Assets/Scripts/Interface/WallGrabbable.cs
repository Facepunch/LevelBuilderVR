using LevelBuilder.Geometry;
using UnityEngine;

namespace LevelBuilder.Interface
{
    [RequireComponent(typeof(Wall))]
    public class WallGrabbable : Grabbable
    {
        private Wall _wall;

        public Wall Wall { get { return _wall ?? (_wall = GetComponent<Wall>()); } }
        
        protected override void OnGrabbed()
        {
            var newCorner = Corner.Create(Wall.Room.Level, transform.position);

            Wall.Create(Wall.Room, Wall.Left, newCorner).Thickness = Wall.Thickness;
            Wall.Left = newCorner;

            if (Wall.Opposite != null)
            {
                Wall.Create(Wall.Opposite.Room, Wall.Opposite.Left, newCorner).Thickness = Wall.Opposite.Thickness;
                Wall.Opposite.Left = newCorner;
            }

            Wall.Invalidate();

            CurrentGrabber.Grab(newCorner.GetComponent<Grabbable>());
        }

        protected override void OnDragged(Vector3 destPos)
        {
            return;
        }
    }
}
