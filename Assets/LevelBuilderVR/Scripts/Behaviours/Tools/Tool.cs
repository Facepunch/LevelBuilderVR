using Unity.Entities;
using UnityEngine;
using Valve.VR.InteractionSystem;

namespace LevelBuilderVR.Behaviours.Tools
{
    public class Tool : MonoBehaviour
    {
        public string Label;
        public Sprite Icon;
        public bool IsSelected;

        private bool _wasSelected;

        protected EntityManager EntityManager => World.DefaultGameObjectInjectionWorld.EntityManager;

        protected Player Player => Player.instance;

        protected Entity Level { get; private set; }

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

            OnUpdate();
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
