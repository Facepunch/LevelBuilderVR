using System;
using System.Collections.Generic;
using Facepunch.UI;
using TMPro;
using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace LevelBuilderVR.Behaviours
{
    public class RadialMenu : MonoBehaviour
    {
        public RadialMenuButton ButtonPrototype;
        public TMP_Text Label;
        public StyledRect LabelBack;

        [HideInInspector]
        public Hand ActiveHand;

        [HideInInspector]
        public RadialMenuButton SelectedButton;

        [HideInInspector]
        public SteamVR_Action_Boolean OpenAction;

        private readonly List<RadialMenuButton> _buttons = new List<RadialMenuButton>();

        private bool _buttonsInvalid;

        public bool IsOpen => gameObject.activeSelf;

        private void Start()
        {
            ButtonPrototype.gameObject.SetActive(false);
        }

        public void ClearButtons()
        {
            foreach (var button in _buttons)
            {
                Destroy(button.gameObject);
            }

            _buttons.Clear();

            SelectedButton = null;
        }

        public void AddButton(string label, Sprite icon, Action onClick, bool isCenter = false)
        {
            var button = Instantiate(ButtonPrototype, ButtonPrototype.transform.parent, false);
            _buttons.Add(button);

            button.RadialMenu = this;
            button.LabelText = label;
            button.IsCenter = isCenter;
            button.Icon.sprite = icon;
            button.OnClicked.AddListener(() => onClick());
            button.gameObject.SetActive(true);

            _buttonsInvalid = true;
        }

        public void Show(Hand hand, SteamVR_Action_Boolean openAction)
        {
            ActiveHand = hand;
            OpenAction = openAction;

            var forward = hand.transform.position - Player.instance.hmdTransform.position;

            forward.Normalize();

            transform.position = hand.transform.position + forward * 0.2f;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            SelectedButton = null;
            Label.text = "";
            LabelBack.enabled = false;

            UpdateButtonPositions();
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);

            ActiveHand = null;
            SelectedButton = null;
        }

        private void UpdateButtonPositions()
        {
            _buttonsInvalid = false;

            if (_buttons.Count == 0)
            {
                return;
            }

            var angle = 0f;
            var deltaAngle = Mathf.PI * 2f / _buttons.Count;
            var radius = ((RectTransform) ButtonPrototype.transform).anchorMin.y - 0.5f;

            foreach (var button in _buttons)
            {
                var x = button.IsCenter ? 0.5f : Mathf.Sin(angle) * radius + 0.5f;
                var y = button.IsCenter ? 0.5f : Mathf.Cos(angle) * radius + 0.5f;

                var rt = (RectTransform) button.transform;

                rt.anchorMin = rt.anchorMax = new Vector2(x, y);

                if (!button.IsCenter)
                {
                    angle += deltaAngle;
                }
            }
        }

        private void Update()
        {
            if (_buttonsInvalid)
            {
                UpdateButtonPositions();
            }

            if (ActiveHand == null || OpenAction == null || !ActiveHand.isActive || ActiveHand.mainRenderModel == null)
            {
                Hide();
                return;
            }

            if (OpenAction.GetStateUp(ActiveHand.handType))
            {
                if (SelectedButton != null)
                {
                    SelectedButton.OnClicked.Invoke();
                }

                Hide();
                return;
            }

            if (!ActiveHand.TryGetPointerPosition(out var handPos))
            {
                Hide();
                return;
            }

            var closest = SelectedButton;
            var closestDist = float.MaxValue;

            foreach (var button in _buttons)
            {
                var dist = (button.transform.position - handPos).magnitude;

                if (dist < closestDist)
                {
                    closest = button;
                    closestDist = dist;
                }
            }

            if (closest != SelectedButton)
            {
                SelectedButton = closest;
                Label.text = closest.LabelText;

                var width = Label.preferredWidth + 32f;

                var min = LabelBack.transform.offsetMin;
                var max = LabelBack.transform.offsetMax;

                min.x = width * -0.5f;
                max.x = width * 0.5f;

                LabelBack.transform.offsetMin = min;
                LabelBack.transform.offsetMax = max;
                LabelBack.enabled = true;
            }
        }
    }
}
