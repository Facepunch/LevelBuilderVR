using JetBrains.Annotations;
using UnityEngine;

namespace LevelBuilder.Interface
{
    [RequireComponent(typeof(SteamVR_TrackedObject))]
    public class SteamVRControllerGrabber : Grabber
    {
        private SteamVR_TrackedObject _trackedObject;

        [UsedImplicitly]
        private void Awake()
        {
            _trackedObject = GetComponent<SteamVR_TrackedObject>();
        }

        [UsedImplicitly]
        private void Update()
        {
            var device = SteamVR_Controller.Input((int) _trackedObject.index);

            if (device.GetHairTriggerUp()) Release();
            if (device.GetHairTriggerDown()) Grab();
        }
    }
}
