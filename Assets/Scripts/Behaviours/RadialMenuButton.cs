using Facepunch.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LevelBuilderVR.Behaviours
{
    public class RadialMenuButton : MonoBehaviour
    {
        public Image Icon;
        public StyledRect Background;

        public string LabelText;

        public RectStyle DefaultStyle;
        public RectStyle SelectedStyle;

        public RadialMenu RadialMenu;

        public UnityEvent OnClicked;

        public StyledRect.EasingType TransitionEase = StyledRect.EasingType.Linear;
        public float TransitionTime = 0.2f;

        private bool _styleInvalid;
        private bool _wasSelected;

        private void OnEnable()
        {
            _styleInvalid = true;
        }

        private void Update()
        {
            if (RadialMenu == null) return;

            var isSelected = RadialMenu.SelectedButton == this;
            if (isSelected == _wasSelected && !_styleInvalid) return;

            _styleInvalid = false;
            _wasSelected = isSelected;

            Background.CrossFadeStyle(isSelected ? SelectedStyle : DefaultStyle, TransitionTime, TransitionEase);
        }
    }
}
