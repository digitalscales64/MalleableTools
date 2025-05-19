using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerStretch : Deformer
    {
        public float radius = 0.2f;
        public float top = 0.5f;
        public float topAfter = 0.5f;
        public float bottom = 0;
        public float bottomAfter = 0;

        [SerializeField]
        [HideInInspector]
        private float prev_top = 0.5f;
        [SerializeField]
        [HideInInspector]
        private float prev_bottom = -0.5f;

        [SerializeField]
        [HideInInspector]
        private float prev_topAfter = 0.5f;
        [SerializeField]
        [HideInInspector]
        private float prev_bottomAfter = -0.5f;

        protected override bool HasWork()
        {
            if (radius <= 0)
            {
                return false;
            }
            if (top == bottom)
            {
                return false;
            }
            if (top == topAfter && bottom == bottomAfter)
            {
                return false;
            }

            return true;
        }

        protected override JobHandle UpdateVertices(DeformingMesh dm, JobHandle jobHandle = default)
        {
            return new MeshJob
            {
                vertices = dm.vertices,
                oneOverTopMinusBottom = 1f / (top - bottom),
                bottom = bottom,
                radiusSquared = radius * radius,
                topMovement = topAfter - top,
                bottomMovement = bottomAfter - bottom,
            }
            .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        public struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float oneOverTopMinusBottom;
            public float bottom;
            public float radiusSquared;
            public float topMovement;
            public float bottomMovement;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                bool4 error = x * x + z * z > radiusSquared;
          
                float4 val = math.clamp((y - bottom) * oneOverTopMinusBottom, 0, 1);
 
                y += math.lerp(bottomMovement, topMovement, val);

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }

        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            DrawGizmo(
                helpers.Cylinder,
                helpers.TransparentBlue,
                objectMatrix * Matrix4x4.TRS(Vector3.up * (bottom + top) / 2, Quaternion.identity, new Vector3(radius, (top - bottom) / 2, radius))
            );

            DrawGizmo(
                helpers.Circle,
                helpers.TransparentRed,
                objectMatrix * Matrix4x4.TRS(Vector3.up * topAfter, Quaternion.Euler(90, 0, 0), Vector3.one * radius)
            );

            DrawGizmo(
                helpers.Circle,
                helpers.TransparentRed,
                objectMatrix * Matrix4x4.TRS(Vector3.up * bottomAfter, Quaternion.Euler(90, 0, 0), Vector3.one * radius)
            );
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();
            radius = Mathf.Max(radius, 0);

            if (top != prev_top)
            {
                top = Mathf.Max(top, bottom);
            }
            else if (bottom != prev_bottom)
            {
                bottom = Mathf.Min(bottom, top);
            }
            prev_top = top;
            prev_bottom = bottom;

            if (topAfter != prev_topAfter)
            {
                topAfter = Mathf.Max(topAfter, bottomAfter);
            }
            else if (bottomAfter != prev_bottomAfter)
            {
                bottomAfter = Mathf.Min(bottomAfter, topAfter);
            }
            prev_topAfter = topAfter;
            prev_bottomAfter = bottomAfter;
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Stretch";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerStretch>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}