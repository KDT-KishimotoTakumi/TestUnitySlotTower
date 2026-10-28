using UnityEngine;

namespace UnitySlotTower.Runtime
{
    /// <summary>戦闘カメラの Viewport Rect を、UI 上の戦闘領域（RectTransform）に合わせる。</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class BattleViewport : MonoBehaviour
    {
        [SerializeField] private RectTransform area;

        private readonly Vector3[] corners = new Vector3[4];
        private Camera battleCamera;

        private void LateUpdate()
        {
            if (area == null) return;
            if (battleCamera == null) battleCamera = GetComponent<Camera>();

            var width = Screen.width;
            var height = Screen.height;
            if (width <= 0 || height <= 0) return;

            // Overlay キャンバスのワールド座標はピクセルに等しい。corners: 0=左下, 2=右上
            area.GetWorldCorners(corners);
            var rect = Rect.MinMaxRect(corners[0].x / width, corners[0].y / height, corners[2].x / width, corners[2].y / height);
            if (rect.width > 0f && rect.height > 0f && battleCamera.rect != rect) battleCamera.rect = rect;
        }
    }
}
