using JetBrains.Annotations;
using LevelBuilderVR.Behaviours;
using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace LevelBuilderVR
{
    public class PlayerSetup : MonoBehaviour
    {
        public bool ShowController = true;
        public GameObject CrosshairPrefab;

        private bool _leftSetup;
        private bool _rightSetup;

        [UsedImplicitly]
        private void Update()
        {
            foreach (var hand in Player.instance.hands)
            {
                if (hand == null) continue;
                if (hand.mainRenderModel == null) continue;

                switch (hand.handType)
                {
                    case SteamVR_Input_Sources.LeftHand:
                        if (_leftSetup) continue;
                        _leftSetup = true;
                        break;
                    case SteamVR_Input_Sources.RightHand:
                        if (_rightSetup) continue;
                        _rightSetup = true;
                        break;
                    default:
                        continue;
                }

                hand.SetSkeletonRangeOfMotion(ShowController ? EVRSkeletalMotionRange.WithController : EVRSkeletalMotionRange.WithoutController);
                hand.ShowController(ShowController);

                if (CrosshairPrefab != null)
                {
                    var crosshair = Instantiate(CrosshairPrefab, hand.transform).GetComponent<Crosshair>();

                    crosshair.Hand = hand;
                }
            }

            if (_leftSetup && _rightSetup)
            {
                enabled = false;
            }
        }
    }
}
