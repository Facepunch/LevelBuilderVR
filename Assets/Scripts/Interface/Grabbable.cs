using JetBrains.Annotations;
using UnityEngine;

namespace LevelBuilder.Interface
{
    public enum GrabMode
    {
        Parent,
        Follow,
        FollowXz
    }

    public class Grabbable : MonoBehaviour
    {
        private Vector3 _grabOffset;
        private Transform _oldParent;

        public GrabMode GrabMode;
        public float GridSize = 0f;

        public Grabber CurrentGrabber { get; private set; }
        
        internal void Grab(Grabber grabber)
        {
            if (CurrentGrabber == grabber) return;
            if (CurrentGrabber != null) CurrentGrabber.Release();

            CurrentGrabber = grabber;
            _oldParent = transform.parent;

            OnGrabbed();

            switch (GrabMode)
            {
                case GrabMode.Parent:
                    transform.SetParent(CurrentGrabber.transform, true);
                    break;
                default:
                    UpdatePosition();
                    break;
            }
        }

        internal void Release()
        {
            if (CurrentGrabber == null) return;

            CurrentGrabber = null;

            switch (GrabMode)
            {
                case GrabMode.Parent:
                    transform.SetParent(_oldParent, true);
                    break;
            }

            OnReleased();
        }

        private void UpdatePosition()
        {
            if (CurrentGrabber == null || GrabMode == GrabMode.Parent) return;

            var dest = CurrentGrabber.transform.position;

            if (GridSize > 0f)
            {
                dest /= GridSize;
                dest = new Vector3(Mathf.Round(dest.x), Mathf.Round(dest.y), Mathf.Round(dest.z)) * GridSize;
            }

            if (GrabMode == GrabMode.FollowXz)
            {
                dest.y = transform.position.y;
            }

            OnDragged(dest);
        }

        [UsedImplicitly]
        private void Update()
        {
            UpdatePosition();
        }

        protected virtual void OnDragged(Vector3 destPos)
        {
            transform.position = destPos;
        }

        protected virtual void OnGrabbed() { }
        protected virtual void OnReleased() { }
    }
}
