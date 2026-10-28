using UnityEngine;

namespace UnitySlotTower.Runtime
{
    /// <summary>味方または敵1体ぶんの表示（体・向きの印・HP ゲージ）。プールで使い回し、割り当てのたびに体の形と材質だけ差し替える。</summary>
    public sealed class ActorView
    {
        private const string BodyName = "Body";
        private const string FacingName = "Facing";
        private const string GaugeName = "Gauge";
        private const string GaugeBackName = "GaugeBack";
        private const string GaugeFillName = "GaugeFill";
        private const float Half = 0.5f;
        private const float FacingForward = 0.4f;
        private const float FacingLength = 0.3f;
        private const float FacingThickness = 0.14f;
        private const float GaugeWidth = 0.8f;
        private const float GaugeHeight = 0.09f;
        private const float GaugeBackDepth = 0.02f;
        private const float GaugeFillDepth = 0.04f;
        private const float GaugeGap = 0.2f;

        private readonly Transform root;
        private readonly Transform body;
        private readonly MeshFilter bodyMesh;
        private readonly MeshRenderer bodyRenderer;
        private readonly Transform facing;
        private readonly Transform gauge;
        private readonly Transform gaugeFill;
        private float shownRatio = -1f;

        private ActorView(Transform root, Transform body, Transform facing, Transform gauge, Transform gaugeFill)
        {
            this.root = root;
            this.body = body;
            bodyMesh = body.GetComponent<MeshFilter>();
            bodyRenderer = body.GetComponent<MeshRenderer>();
            this.facing = facing;
            this.gauge = gauge;
            this.gaugeFill = gaugeFill;
        }

        /// <summary>プール用に作る（無効の状態で返す）。parent の下に置く。facingMaterial が null なら向きの印は作らない（敵用）。</summary>
        public static ActorView Create(string name, Transform parent, Material facingMaterial, Material gaugeBackMaterial, Material gaugeFillMaterial)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var body = CreatePart(PrimitiveType.Cube, BodyName, root, null);
            Transform facing = null;
            if (facingMaterial != null)
            {
                facing = CreatePart(PrimitiveType.Cube, FacingName, root, facingMaterial);
                facing.localScale = new Vector3(FacingThickness, FacingThickness, FacingLength);
            }
            var gauge = new GameObject(GaugeName).transform;
            gauge.SetParent(root, false);
            var back = CreatePart(PrimitiveType.Cube, GaugeBackName, gauge, gaugeBackMaterial);
            back.localScale = new Vector3(GaugeWidth, GaugeHeight, GaugeBackDepth);
            var fill = CreatePart(PrimitiveType.Cube, GaugeFillName, gauge, gaugeFillMaterial);
            fill.localScale = new Vector3(GaugeWidth, GaugeHeight, GaugeFillDepth);
            var view = new ActorView(root, body, facing, gauge, fill);
            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>割り当て時に体の形・材質と、足元の位置（ローカル座標）を決める。</summary>
        public void Bind(BodyStyle style, Mesh mesh, Material material, Vector3 footPosition, float yawDegrees)
        {
            bodyMesh.sharedMesh = mesh;
            bodyRenderer.sharedMaterial = material;
            var height = style.Height;
            body.localScale = style.Scale;
            body.localRotation = Quaternion.Euler(style.Euler);
            body.localPosition = new Vector3(0f, height * Half, 0f);
            if (facing != null) facing.localPosition = new Vector3(0f, height * Half, FacingForward);
            gauge.localPosition = new Vector3(0f, height + GaugeGap, 0f);
            shownRatio = -1f;
            root.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            root.localPosition = footPosition;
            root.gameObject.SetActive(true);
        }

        public void Release() => root.gameObject.SetActive(false);

        public void SetFootPosition(Vector3 position) => root.localPosition = position;

        /// <summary>ゲージを ratio（0〜1）に合わせ、カメラの向きに揃える。ratio が変わらなければゲージは触らない。</summary>
        public void Refresh(float ratio, Quaternion cameraRotation)
        {
            gauge.rotation = cameraRotation;
            if (Mathf.Approximately(ratio, shownRatio)) return;
            shownRatio = ratio;
            HpGauge.Layout(ratio, GaugeWidth, out var fillWidth, out var offset);
            gaugeFill.localScale = new Vector3(fillWidth, GaugeHeight, GaugeFillDepth);
            gaugeFill.localPosition = new Vector3(offset, 0f, 0f);
        }

        private static Transform CreatePart(PrimitiveType shape, string partName, Transform parent, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = partName;
            Object.Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            if (material != null) part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part.transform;
        }
    }
}
