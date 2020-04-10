using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace LevelBuilderVR.Behaviours
{
    public class GrabZoom : MonoBehaviour
    {
        public HybridLevel TargetLevel;

        public SteamVR_Action_Boolean GrabZoomAction = SteamVR_Input.GetBooleanAction("GrabZoom");

        private Vector3 _leftLocalAnchor;
        private Vector3 _rightLocalAnchor;

        private void OnEnable()
        {

        }

        private static bool IsHandValid(Hand hand)
        {
            return hand.currentAttachedObject == null;
        }

        private void Update()
        {
            if (TargetLevel == null) return;

            var player = Player.instance;

            var leftValid = IsHandValid(player.leftHand);
            var rightValid = IsHandValid(player.rightHand);

            var leftGrabZoomPressed = leftValid && GrabZoomAction.GetStateDown(SteamVR_Input_Sources.LeftHand);
            var rightGrabZoomPressed = rightValid && GrabZoomAction.GetStateDown(SteamVR_Input_Sources.RightHand);

            var leftGrabZoomReleased = leftValid && GrabZoomAction.GetStateUp(SteamVR_Input_Sources.LeftHand);
            var rightGrabZoomReleased = rightValid && GrabZoomAction.GetStateUp(SteamVR_Input_Sources.RightHand);

            var leftGrabZoomHeld = leftValid && GrabZoomAction.GetState(SteamVR_Input_Sources.LeftHand);
            var rightGrabZoomHeld = rightValid && GrabZoomAction.GetState(SteamVR_Input_Sources.RightHand);

            var leftLocalPos = TargetLevel.transform.InverseTransformPoint(player.leftHand.objectAttachmentPoint.position);
            var rightLocalPos = TargetLevel.transform.InverseTransformPoint(player.rightHand.objectAttachmentPoint.position);

            if (leftGrabZoomPressed || rightGrabZoomPressed || leftGrabZoomReleased || rightGrabZoomReleased)
            {
                _leftLocalAnchor = leftLocalPos;
                _rightLocalAnchor = rightLocalPos;
            }

            if (leftGrabZoomHeld && rightGrabZoomHeld)
            {
                var anchor = (_leftLocalAnchor + _rightLocalAnchor) * 0.5f;
                var pos = (leftLocalPos + rightLocalPos) * 0.5f;
                var localDiff = pos - anchor;

                var worldDiff = TargetLevel.transform.TransformVector(localDiff);

                TargetLevel.transform.Translate(worldDiff, Space.World);

                var betweenAnchorDiff = _rightLocalAnchor - _leftLocalAnchor;
                var betweenLocalDiff = rightLocalPos - leftLocalPos;

                var sizeRatio = betweenLocalDiff.magnitude / betweenAnchorDiff.magnitude;

                TargetLevel.transform.localScale *= sizeRatio;

                var betweenAnchorAngle = Mathf.Atan2(betweenAnchorDiff.z, betweenAnchorDiff.x) * Mathf.Rad2Deg;
                var betweenLocalAngle = Mathf.Atan2(betweenLocalDiff.z, betweenLocalDiff.x) * Mathf.Rad2Deg;
                var angleDiff = Mathf.DeltaAngle(betweenAnchorAngle, betweenLocalAngle);

                var worldPivot = TargetLevel.transform.TransformPoint(pos);

                TargetLevel.transform.RotateAround(worldPivot, Vector3.up, -angleDiff);
            }
            else if (leftGrabZoomHeld || rightGrabZoomHeld)
            {
                var anchor = leftGrabZoomHeld ? _leftLocalAnchor : _rightLocalAnchor;
                var pos = leftGrabZoomHeld ? leftLocalPos : rightLocalPos;
                var localDiff = pos - anchor;
                var worldDiff = TargetLevel.transform.TransformVector(localDiff);

                TargetLevel.transform.Translate(worldDiff, Space.World);
            }
        }
    }
}
