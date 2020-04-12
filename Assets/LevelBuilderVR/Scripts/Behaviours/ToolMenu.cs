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

        [HideInInspector]
        public RadialMenu RadialMenu;

        [HideInInspector]
        public Tool SelectedTool;

        private void Show(Hand hand)
        {
            if (RadialMenu == null)
            {
                RadialMenu = Instantiate(RadialMenuPrefab).GetComponent<RadialMenu>();
            }

            RadialMenu.ClearButtons();

            foreach (var tool in FindObjectsOfType<Tool>())
            {
                RadialMenu.AddButton(tool.Label, tool.Icon, () =>
                {
                    if (SelectedTool != null) SelectedTool.IsSelected = false;
                    SelectedTool = tool;
                    tool.IsSelected = true;
                }, SelectedTool == tool);
            }

            RadialMenu.Show(hand, OpenAction);
        }

        private void OnEnable()
        {
            Tool firstSelected = null;

            foreach (var tool in FindObjectsOfType<Tool>())
            {
                if (!tool.IsSelected)
                {
                    continue;
                }

                if (firstSelected == null)
                {
                    firstSelected = tool;
                }
                else
                {
                    tool.IsSelected = false;
                }
            }
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
