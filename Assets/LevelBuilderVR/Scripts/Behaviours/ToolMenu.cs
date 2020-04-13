using LevelBuilderVR.Behaviours.Tools;
using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace LevelBuilderVR.Behaviours
{
    public class ToolMenu : MonoBehaviour
    {
        public SteamVR_Action_Boolean OpenAction = SteamVR_Input.GetBooleanAction("ToolMenu");
        public GameObject RadialMenuPrefab;

        public Tool DefaultOffhandTool;

        [HideInInspector]
        public RadialMenu RadialMenu;

        [HideInInspector]
        public Tool LeftSelectedTool;

        [HideInInspector]
        public Tool RightSelectedTool;

        private Tool GetSelectedTool(Hand hand)
        {
            var player = Player.instance;

            if (hand == player.leftHand)
            {
                return LeftSelectedTool;
            }
            
            if (hand == player.rightHand)
            {
                return RightSelectedTool;
            }

            return null;
        }

        private void SetSelectedTool(Hand hand, Tool tool)
        {
            var player = Player.instance;
            var oldTool = GetSelectedTool(hand);

            if (oldTool == tool)
            {
                return;
            }

            if (oldTool != null)
            {
                if (hand == player.leftHand)
                {
                    oldTool.LeftHandActive = false;
                }

                if (hand == player.rightHand)
                {
                    oldTool.RightHandActive = false;
                }
            }

            if (hand == player.leftHand)
            {
                LeftSelectedTool = tool;

                if (tool != null)
                {
                    tool.LeftHandActive = true;
                }
            }

            if (hand == player.rightHand)
            {
                RightSelectedTool = tool;

                if (tool != null)
                {
                    tool.RightHandActive = true;
                }
            }

            if (tool != null && tool != DefaultOffhandTool && !tool.AllowTwoHanded)
            {
                SetSelectedTool(hand.otherHand, DefaultOffhandTool);
            }
        }

        private void Show(Hand hand)
        {
            if (RadialMenu == null)
            {
                RadialMenu = Instantiate(RadialMenuPrefab).GetComponent<RadialMenu>();
            }

            RadialMenu.ClearButtons();

            foreach (var tool in FindObjectsOfType<Tool>())
            {
                RadialMenu.AddButton(tool.Label, tool.Icon, () => SetSelectedTool(hand, tool), 
                    isCenter: tool == DefaultOffhandTool);
            }

            RadialMenu.Show(hand, OpenAction);
        }

        private void OnEnable()
        {
            foreach (var tool in FindObjectsOfType<Tool>())
            {
                tool.LeftHandActive = false;
                tool.RightHandActive = false;
            }

            var player = Player.instance;

            SetSelectedTool(player.leftHand, DefaultOffhandTool);
            SetSelectedTool(player.rightHand, DefaultOffhandTool);
        }

        private void Update()
        {
            var player = Player.instance;

            if (RadialMenu != null && RadialMenu.IsOpen) return;

            if (OpenAction.GetStateDown(player.leftHand.handType))
            {
                Show(player.leftHand);
                return;
            }

            if (OpenAction.GetStateDown(player.rightHand.handType))
            {
                Show(player.rightHand);
                return;
            }
        }
    }
}
