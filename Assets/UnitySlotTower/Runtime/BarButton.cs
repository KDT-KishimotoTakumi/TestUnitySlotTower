using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// 操作バーなどの小さなボタン（▶ ❚❚、強化の ▲、撤退の ×。T308）。左クリックされたことを持ち主に伝えるだけで、ロジックは持たない。
    /// 押下も受け取る（何もしない）。これがないと、カードの中の ▲ を押した瞬間が親のカード（召喚の選択）に届いてしまう。
    /// </summary>
    public sealed class BarButton : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
    {
        private Action pressed;

        public void Bind(Action onPressed) => pressed = onPressed;

        public void OnPointerDown(PointerEventData eventData)
        {
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) pressed?.Invoke();
        }
    }
}
