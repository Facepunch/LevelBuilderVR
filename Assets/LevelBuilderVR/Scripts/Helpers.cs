using LevelBuilderVR.Behaviours;
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
    }
}
