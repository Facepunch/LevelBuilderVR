using Unity.Entities;
using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace LevelBuilderVR.Behaviours.Tools
{
    public abstract class Tool : MonoBehaviour
    {
        public SteamVR_Action_Boolean UseToolAction = SteamVR_Input.GetBooleanAction("UseTool");
        public SteamVR_Action_Boolean MultiSelectAction = SteamVR_Input.GetBooleanAction("MultiSelect");
        public SteamVR_Action_Boolean AxisAlignAction = SteamVR_Input.GetBooleanAction("AxisAlign");

        public string Label;
        public Sprite Icon;

        private bool _wasSelected;

        public bool LeftHandActive;
        public bool RightHandActive;

        public bool IsSelected => LeftHandActive || RightHandActive;

        protected EntityManager EntityManager => World.DefaultGameObjectInjectionWorld.EntityManager;

        protected Player Player => Player.instance;

        protected HybridLevel HybridLevel { get; private set; }

        protected Entity Level { get; private set; }

        public abstract bool AllowTwoHanded { get; }

        private void Start()
        {
            HybridLevel = FindObjectOfType<HybridLevel>();

            OnStart();
        }

        protected virtual void OnStart()
        {

        }

        protected virtual void OnSelectLevel(Entity level)
        {

        }

        private void Update()
        {
            if (Level == Entity.Null)
            {
                Level = HybridLevel.Level;

                if (Level != Entity.Null)
                {
                    OnSelectLevel(Level);
                }
            }

            if (IsSelected && !_wasSelected)
            {
                OnSelected();
            }

            if (!IsSelected && _wasSelected)
            {
                OnDeselected();
            }

            _wasSelected = IsSelected;

            if (IsSelected)
            {
                OnUpdate();
            }
        }

        protected virtual void OnSelected()
        {

        }

        protected virtual void OnDeselected()
        {

        }

        protected virtual void OnUpdate()
        {

        }
    }
}
