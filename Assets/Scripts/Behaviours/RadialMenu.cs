using System;
using Boo.Lang;
using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace LevelBuilderVR.Behaviours
{
    public class RadialMenu : MonoBehaviour
    {
        public RadialMenuButton ButtonPrototype;

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

        public void AddButton(string label, Sprite icon, Action onClick, bool isSelected = false)
        {
            var button = Instantiate(ButtonPrototype, ButtonPrototype.transform.parent, false);
            _buttons.Add(button);

            button.RadialMenu = this;
            button.LabelText = label;
            button.Icon.sprite = icon;
            button.OnClicked.AddListener(() => onClick());
            button.gameObject.SetActive(true);

            _buttonsInvalid = true;

            if (isSelected)
            {
                SelectedButton = button;
            }
        }

        public void Show(Hand hand, SteamVR_Action_Boolean openAction)
        {
            ActiveHand = hand;
            OpenAction = openAction;

            var forward = hand.transform.forward;

            forward.y = 0f;
            forward.Normalize();
            forward.y = -0.3f;

            transform.position = hand.transform.position + forward * 0.2f;

            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

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
                var x = Mathf.Sin(angle) * radius + 0.5f;
                var y = -Mathf.Cos(angle) * radius + 0.5f;

                var rt = (RectTransform) button.transform;

                rt.anchorMin = rt.anchorMax = new Vector2(x, y);

                angle += deltaAngle;
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

            Vector3 handPos;
            try
            {
                handPos = ActiveHand.mainRenderModel.GetControllerPosition(ActiveHand.controllerHoverComponent);
            }
            catch
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

            SelectedButton = closest;
        }
    }
}
