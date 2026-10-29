using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnitySlotTower.Runtime
{
    /// <summary>操作バーのボタン（▶ と ❚❚、T308）。左クリックされたことを <see cref="OperationBarView"/> に伝えるだけで、ロジックは持たない。</summary>
    public sealed class BarButton : MonoBehaviour, IPointerClickHandler
    {
        private Action pressed;

        public void Bind(Action onPressed) => pressed = onPressed;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) pressed?.Invoke();
        }
    }
}
