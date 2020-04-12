using LevelBuilderVR.Entities;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Valve.VR.InteractionSystem;

namespace LevelBuilderVR.Behaviours.Tools
{
    public class VertexEditTool : Tool
    {
        private Entity _leftHovered;
        private Entity _rightHovered;

        public float InteractRadius = 0.01f;

        public ushort HapticPulseDurationMicros = 500;

        private void UpdateHover(Hand hand, ref Entity hovered)
        {
            if (!hand.TryGetPointerPosition(out var handPos))
            {
                if (hovered != Entity.Null)
                {
                    EntityManager.SetHovered(hovered, false);
                    hovered = Entity.Null;
                }

                return;
            }

            var localToWorld = EntityManager.GetComponentData<LocalToWorld>(Level).Value;
            var worldToLocal = EntityManager.GetComponentData<WorldToLocal>(Level).Value;
            var localHandPos = math.transform(worldToLocal, handPos);

            if (!EntityManager.FindClosestVertex(Level, localHandPos, out var newHovered, out var hoverPos) || newHovered != hovered)
            {
                EntityManager.SetHovered(hovered, false);
                hovered = Entity.Null;

                if (newHovered == Entity.Null)
                {
                    return;
                }
            }

            var hoverWorldPos = math.transform(localToWorld, hoverPos);
            var dist2 = math.distancesq(hoverWorldPos, handPos);

            if (dist2 > InteractRadius)
            {
                return;
            }

            if (hovered == newHovered)
            {
                return;
            }

            EntityManager.SetHovered(newHovered, true);
            hovered = newHovered;

            hand.TriggerHapticPulse(HapticPulseDurationMicros);
        }

        protected override void OnUpdate()
        {
            UpdateHover(Player.leftHand, ref _leftHovered);
            UpdateHover(Player.rightHand, ref _rightHovered);
        }

        protected override void OnDeselected()
        {
            if (_leftHovered != Entity.Null)
            {
                EntityManager.SetHovered(_leftHovered, false);
                _leftHovered = Entity.Null;
            }

            if (_rightHovered != Entity.Null)
            {
                EntityManager.SetHovered(_rightHovered, false);
                _rightHovered = Entity.Null;
            }
        }
    }
}
