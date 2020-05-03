using Unity.Entities;
using Unity.Mathematics;
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

        public virtual bool ShowGrid { get; }

        public virtual Vector3 GridOrigin { get; }

        public float GridSnapTargetResolution = 0.01f;

        private static readonly float[] _sGridSnaps =
        {
            1f,
            1f / 2f,
            1f / 4f,
            1f / 8f,
            1f / 16f,
            1f / 32f,
            1f / 64f
        };

        public float GridSnap { get; private set; }

        public float InteractRadius = 0.05f;

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

        private void UpdateGridSnap()
        {
            var levelScale = HybridLevel.transform.localScale.x;
            var targetSnap = GridSnapTargetResolution / levelScale;
            var targetSnapLog = math.log10(targetSnap);

            GridSnap = 1f;
            var bestDist = float.MaxValue;

            for (var i = 0; i < _sGridSnaps.Length; ++i)
            {
                var snap = _sGridSnaps[i];
                var snapLog = math.log10(snap);

                var dist = math.abs(snapLog - targetSnapLog);
                if (dist < bestDist)
                {
                    GridSnap = snap;
                    bestDist = dist;
                }
            }
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

            if (IsSelected)
            {
                UpdateGridSnap();
            }

            if (IsSelected && !_wasSelected)
            {
                OnSelected();
            }

            if (!IsSelected && _wasSelected)
            {
                HybridLevel.GridGuide.enabled = false;
                OnDeselected();
            }

            _wasSelected = IsSelected;

            if (IsSelected)
            {
                OnUpdate();

                var showGrid = ShowGrid;
                HybridLevel.GridGuide.enabled = showGrid;

                if (showGrid)
                {
                    HybridLevel.GridGuide.Origin = GridOrigin;
                    HybridLevel.GridGuide.MinorDivisionSize = GridSnap;
                }
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
