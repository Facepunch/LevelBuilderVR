﻿using Unity.Entities;
using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace LevelBuilderVR.Behaviours.Tools
{
    public class Tool : MonoBehaviour
    {
        public SteamVR_Action_Boolean UseToolAction = SteamVR_Input.GetBooleanAction("UseTool");
        public SteamVR_Action_Boolean MultiSelectAction = SteamVR_Input.GetBooleanAction("MultiSelect");
        public SteamVR_Action_Boolean AxisAlignAction = SteamVR_Input.GetBooleanAction("AxisAlign");

        public string Label;
        public Sprite Icon;
        public bool IsSelected;

        private bool _wasSelected;

        protected EntityManager EntityManager => World.DefaultGameObjectInjectionWorld.EntityManager;

        protected Player Player => Player.instance;

        protected GrabZoom GrabZoom { get; private set; }

        protected Entity Level { get; private set; }

        private void Start()
        {
            GrabZoom = FindObjectOfType<GrabZoom>();

            OnStart();
        }

        protected virtual void OnStart()
        {

        }

        private void Update()
        {
            if (Level == Entity.Null)
            {
                Level = FindObjectOfType<HybridLevel>().Level;
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
