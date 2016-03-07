using System.Linq;
using UnityEngine;

namespace LevelBuilder.Interface
{
    public class Grabber : MonoBehaviour
    {
        public float GrabRange = 0.1f;

        public Grabbable CurrentlyGrabbed { get; private set; }

        public void Grab(Grabbable target)
        {
            Release();
            
            CurrentlyGrabbed = target;
            CurrentlyGrabbed.Grab(this);
        }

        public void Grab()
        {
            var worldScaleRange2 = transform.lossyScale.x*GrabRange;
            worldScaleRange2 *= worldScaleRange2;

            var toGrab = FindObjectsOfType<Grabbable>()
                .Where(x => x != CurrentlyGrabbed && (x.transform.position - transform.position).sqrMagnitude <= worldScaleRange2)
                .OrderBy(x => (x.transform.position - transform.position).sqrMagnitude)
                .Where(CanGrab)
                .FirstOrDefault();
            
            if (toGrab == null) return;

            Grab(toGrab);
        }

        public void Release()
        {
            if (CurrentlyGrabbed == null) return;

            CurrentlyGrabbed.Release();
            CurrentlyGrabbed = null;
        }

        public virtual bool CanGrab(Grabbable grabbable)
        {
            return true;
        }
    }
}
