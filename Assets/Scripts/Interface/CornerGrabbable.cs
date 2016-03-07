using LevelBuilder.Geometry;
using UnityEngine;

namespace LevelBuilder.Interface
{
    public class CornerGrabbable : Grabbable
    {
        private Corner _corner;
        private Vector3 _originalPos;

        public Corner Corner { get { return _corner ?? (_corner = GetComponent<Corner>()); } }

        protected override void OnGrabbed()
        {
            base.OnGrabbed();

            _originalPos = transform.position;
        }

        protected override void OnReleased()
        {
            base.OnReleased();

            var closest = FindObjectsOfType<Corner>();

            foreach (var corner in closest)
            {
                if (corner == Corner || Helper.SwizzleXz(corner.transform.position - transform.position).sqrMagnitude > 1f/256f) continue;
                if (!corner.TryMergeFrom(Corner))
                {
                    transform.position = _originalPos;
                    return;
                }

                Destroy(gameObject);

                return;
            }
        }
    }
}
