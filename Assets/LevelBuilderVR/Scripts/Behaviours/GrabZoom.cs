using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements.Experimental;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace LevelBuilderVR.Behaviours
{
    public class GrabZoom : MonoBehaviour
    {
        public HybridLevel TargetLevel;

        public float SnapAngle = 15f;

        public float MinScaleLog10 = -2;
        public float MaxScaleLog10 = 0;
        public float ScaleIncrementLog10 = 1f / 4f;

        public SteamVR_Action_Boolean GrabZoomAction = SteamVR_Input.GetBooleanAction("GrabZoom");
        public SteamVR_Action_Boolean UseToolAction = SteamVR_Input.GetBooleanAction("UseTool");

        public GameObject TextPrefab;

        private Vector3 _leftLocalAnchor;
        private Vector3 _rightLocalAnchor;

        public Texture2D CrosshairGrabTexture;
        public Texture2D CrosshairRotateTexture;
        public Texture2D CrosshairScaleTexture;

        [HideInInspector]
        public TMP_Text Text;

        private bool _isScaling;
        private bool _leftHeld;
        private bool _rightHeld;

        private float _prevScale;
        private float _targetScale;
        private float _scaleTimer;

        private float[] _scales;

        private float _prevAngle;
        private float _targetAngle;
        private float _angleTimer;
        private Vector3 _worldPivotPos;

        public float EasingTime = 0.2f;

        private void Start()
        {
            var scaleIncrements = (int) math.round((MaxScaleLog10 - MinScaleLog10) / ScaleIncrementLog10);

            _scales = new float[scaleIncrements + 1];

            for (var i = 0; i <= scaleIncrements; ++i)
            {
                _scales[i] = math.pow(10f, MinScaleLog10 + ScaleIncrementLog10 * i);
            }

            Text = Instantiate(TextPrefab).GetComponent<TMP_Text>();
            Text.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (TargetLevel == null)
            {
                return;
            }

            _targetAngle = TargetLevel.transform.localEulerAngles.y;
            _targetScale = TargetLevel.transform.localScale.x;
        }

        private void UpdateCrosshairTextures()
        {
            var player = Player.instance;

            if (_leftHeld && _rightHeld)
            {
                if (_isScaling)
                {
                    player.leftHand.SetCrosshairTexture(CrosshairScaleTexture);
                    player.rightHand.SetCrosshairTexture(CrosshairScaleTexture);

                    UpdateScaleText();
                }
                else
                {
                    player.leftHand.SetCrosshairTexture(CrosshairRotateTexture);
                    player.rightHand.SetCrosshairTexture(CrosshairRotateTexture);

                    UpdateAngleText();
                }
            }
            else if (_leftHeld)
            {
                player.leftHand.SetCrosshairTexture(CrosshairGrabTexture);
                player.rightHand.ResetCrosshairTexture();
            }
            else if (_rightHeld)
            {
                player.leftHand.ResetCrosshairTexture();
                player.rightHand.SetCrosshairTexture(CrosshairGrabTexture);
            }
            else
            {
                player.leftHand.ResetCrosshairTexture();
                player.rightHand.ResetCrosshairTexture();
            }

            Text.gameObject.SetActive(_leftHeld && _rightHeld);
        }

        private void UpdateScaleText()
        {
            var scaleStr = _targetScale < 1f ? $"{_targetScale * 100f:F0}cm" : $"{_targetScale:F0}m";

            Text.text = $"1m:{scaleStr}";
        }

        private void UpdateAngleText()
        {
            Text.text = $"{_targetAngle:F0}°";
        }

        private void Update()
        {
            if (TargetLevel == null) return;

            var player = Player.instance;

            var leftValid = player.leftHand.TryGetPointerPosition(out var leftWorldPos);
            var rightValid = player.rightHand.TryGetPointerPosition(out var rightWorldPos);

            var leftPressed = leftValid && UseToolAction.GetStateDown(SteamVR_Input_Sources.LeftHand) && GrabZoomAction.GetState(SteamVR_Input_Sources.LeftHand);
            var rightPressed = rightValid && UseToolAction.GetStateDown(SteamVR_Input_Sources.RightHand) && GrabZoomAction.GetState(SteamVR_Input_Sources.LeftHand);

            var leftReleased = leftValid && UseToolAction.GetStateUp(SteamVR_Input_Sources.LeftHand);
            var rightReleased = rightValid && UseToolAction.GetStateUp(SteamVR_Input_Sources.RightHand);

            _leftHeld &= leftValid && UseToolAction.GetState(SteamVR_Input_Sources.LeftHand);
            _rightHeld &= rightValid && UseToolAction.GetState(SteamVR_Input_Sources.RightHand);

            var leftLocalPos = TargetLevel.transform.InverseTransformPoint(leftWorldPos);
            var rightLocalPos = TargetLevel.transform.InverseTransformPoint(rightWorldPos);

            var crosshairsInvalid = false;

            // New anchor points if anything pressed or released

            if (leftPressed || rightPressed || leftReleased || rightReleased)
            {
                _leftLocalAnchor = leftLocalPos;
                _rightLocalAnchor = rightLocalPos;

                _leftHeld |= leftPressed;
                _rightHeld |= rightPressed;

                crosshairsInvalid = true;
            }

            // Check for new scale / rotation target

            if (_leftHeld && _rightHeld)
            {
                _worldPivotPos = (leftWorldPos + rightWorldPos) * 0.5f;

                var betweenAnchorDiff = _rightLocalAnchor - _leftLocalAnchor;
                var betweenLocalDiff = rightLocalPos - leftLocalPos;

                var isScaling = Mathf.Abs(betweenAnchorDiff.normalized.y) >= Mathf.Sqrt(0.5f);

                if (isScaling)
                {
                    var sizeRatio = betweenLocalDiff.magnitude / betweenAnchorDiff.magnitude;
                    var newScale = TargetLevel.transform.localScale.x * sizeRatio;
                    var logNewScale = math.log10(newScale);

                    var bestScale = 1f;
                    var bestLogScaleDiff = float.PositiveInfinity;

                    foreach (var scale in _scales)
                    {
                        var logScale = math.log10(scale);
                        var logScaleDiff = math.abs(logScale - logNewScale);

                        if (logScaleDiff < bestLogScaleDiff)
                        {
                            bestLogScaleDiff = logScaleDiff;
                            bestScale = scale;
                        }
                    }

                    if (bestScale != _targetScale)
                    {
                        _prevScale = _targetScale;
                        _targetScale = bestScale;
                        _scaleTimer = 1f;

                        player.leftHand.TriggerHapticPulse(500);
                        player.rightHand.TriggerHapticPulse(500);

                        UpdateScaleText();
                    }
                }
                else
                {
                    var betweenAnchorAngle = Mathf.Atan2(betweenAnchorDiff.z, betweenAnchorDiff.x) * Mathf.Rad2Deg;
                    var betweenLocalAngle = Mathf.Atan2(betweenLocalDiff.z, betweenLocalDiff.x) * Mathf.Rad2Deg;

                    var angleDiff = Mathf.DeltaAngle(betweenLocalAngle, betweenAnchorAngle);
                    var targetAngle = TargetLevel.transform.localEulerAngles.y + angleDiff;

                    targetAngle = math.round(targetAngle / SnapAngle) * SnapAngle;
                    targetAngle -= math.floor(targetAngle / 360f) * 360f;

                    if (Mathf.DeltaAngle(_targetAngle, targetAngle) != 0f)
                    {
                        _prevAngle = _targetAngle;
                        _targetAngle = targetAngle;
                        _angleTimer = 1f;

                        player.leftHand.TriggerHapticPulse(500);
                        player.rightHand.TriggerHapticPulse(500);

                        UpdateAngleText();
                    }
                }

                if (isScaling != _isScaling)
                {
                    _isScaling = isScaling;
                    crosshairsInvalid = true;
                }
            }

            // Handle rotation / scale animation

            var rotatedOrScaled = false;

            if (_scaleTimer > 0f)
            {
                _scaleTimer -= Time.deltaTime / EasingTime;
                var t = Mathf.Clamp01(1f - _scaleTimer);

                var scale = _prevScale + Easing.InOutCubic(t) * (_targetScale - _prevScale);
                TargetLevel.transform.localScale = Vector3.one * scale;

                rotatedOrScaled = true;
            }

            if (_angleTimer > 0f)
            {
                _angleTimer -= Time.deltaTime / EasingTime;
                var t = Mathf.Clamp01(1f - _angleTimer);

                var angle = _prevAngle + Easing.InOutCubic(t) * Mathf.DeltaAngle(_prevAngle, _targetAngle);
                var curAngle = TargetLevel.transform.localEulerAngles.y;

                TargetLevel.transform.RotateAround(_worldPivotPos, Vector3.up, angle - curAngle);

                rotatedOrScaled = true;
            }

            // Local positions will have changed if scale or rotation changed

            if (rotatedOrScaled)
            {
                leftLocalPos = TargetLevel.transform.InverseTransformPoint(leftWorldPos);
                rightLocalPos = TargetLevel.transform.InverseTransformPoint(rightWorldPos);
            }

            // Grab translation

            if (_leftHeld || _rightHeld)
            {
                Vector3 anchor, pos;

                if (_leftHeld && _rightHeld)
                {
                    anchor = (_leftLocalAnchor + _rightLocalAnchor) * 0.5f;
                    pos = (leftLocalPos + rightLocalPos) * 0.5f;
                }
                else
                {
                    anchor = _leftHeld ? _leftLocalAnchor : _rightLocalAnchor;
                    pos = _leftHeld ? leftLocalPos : rightLocalPos;
                }

                var localDiff = pos - anchor;
                var worldDiff = TargetLevel.transform.TransformVector(localDiff);

                TargetLevel.transform.Translate(worldDiff, Space.World);
            }

            // Update widgets

            if (crosshairsInvalid)
            {
                UpdateCrosshairTextures();
            }

            if (Text.gameObject.activeSelf)
            {
                Text.transform.position = _worldPivotPos;
                Text.transform.rotation = Quaternion.LookRotation(_worldPivotPos - Player.instance.hmdTransform.position);
            }
        }
    }
}
