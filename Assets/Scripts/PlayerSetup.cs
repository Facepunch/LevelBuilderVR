using JetBrains.Annotations;
using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace LevelBuilderVR
{
    public class PlayerSetup : MonoBehaviour
    {
        public bool ShowController = true;

        [UsedImplicitly]
        private void Update()
        {
            var finishedSetup = false;

            foreach (var hand in Player.instance.hands)
            {
                if (hand == null) continue;
                if (hand.mainRenderModel == null) continue;

                hand.SetSkeletonRangeOfMotion(ShowController ? EVRSkeletalMotionRange.WithController : EVRSkeletalMotionRange.WithoutController);
                hand.ShowController(ShowController);

                finishedSetup = true;
            }

            if (finishedSetup)
            {
                enabled = false;
            }
        }
    }
}
