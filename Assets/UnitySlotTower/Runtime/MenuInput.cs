using System;
using UnityEngine.InputSystem;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// タイトルとリザルトのキー操作（決定・戻る・数字）。<c>Game</c> マップの専用アクションだけを有効にする（他の入力を巻き込まない）。
    /// コールバックは先に作ってから登録する（Reload Domain 無効でも null が残らないように）。
    /// </summary>
    public sealed class MenuInput
    {
        private const string ConfirmName = "Game/MenuConfirm";
        private const string BackName = "Game/MenuBack";
        private const string DigitName = "Game/MenuDigit";

        private readonly Action<InputAction.CallbackContext> confirmCallback;
        private readonly Action<InputAction.CallbackContext> backCallback;
        private readonly Action<InputAction.CallbackContext> digitCallback;
        private InputAction confirm;
        private InputAction back;
        private InputAction digit;

        public event Action Confirmed;
        public event Action Back;
        /// <summary>数字キー（1〜9。トップ行・テンキー両方）。</summary>
        public event Action<int> DigitPressed;

        public MenuInput()
        {
            confirmCallback = _ => Confirmed?.Invoke();
            backCallback = _ => Back?.Invoke();
            digitCallback = context =>
            {
                var name = context.control.name;
                var value = name[name.Length - 1] - '0';
                if (value >= 0 && value <= 9) DigitPressed?.Invoke(value);
            };
        }

        public void Enable(InputActionAsset asset)
        {
            Disable();   // 二重に呼ばれても、コールバックを重ねて登録しない
            confirm = asset.FindAction(ConfirmName, true);
            back = asset.FindAction(BackName, true);
            digit = asset.FindAction(DigitName, true);
            confirm.performed += confirmCallback;
            back.performed += backCallback;
            digit.performed += digitCallback;
            confirm.Enable();
            back.Enable();
            digit.Enable();
        }

        public void Disable()
        {
            if (confirm == null) return;
            confirm.performed -= confirmCallback;
            back.performed -= backCallback;
            digit.performed -= digitCallback;
            confirm.Disable();
            back.Disable();
            digit.Disable();
            confirm = back = digit = null;
        }
    }
}
