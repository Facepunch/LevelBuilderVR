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
                    worldPos = hand.mainRenderModel.GetControllerPosition(hand.controllerHoverComponent);
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
    }
}
