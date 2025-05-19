using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerBulge : Deformer
    {
        public float amount;
        public float top = 0.5f;
        public float bottom = -0.5f;
        public bool smooth = true;

        [SerializeField]
        [HideInInspector]
        private float prev_top = 0.5f;
        [SerializeField]
        [HideInInspector]
        private float prev_bottom = -0.5f;

        protected override bool HasWork()
        {
            if (amount == 0)
            {
                return false;
            }
            return true;
        }

        protected override JobHandle UpdateVertices(DeformingMesh dm, JobHandle jobHandle)
        {
            return new MeshJob
            {
                vertices = dm.vertices,
                amount = -amount * 4,
                top = top,
                bottom = bottom,
                oneDivHeight = 1 / (top - bottom),
                smooth = smooth,
            }
            .Schedule(dm.vertices.Length, 2048, jobHandle);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        public struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float amount;
            public float top;
            public float bottom;
            public float oneDivHeight;
            public bool smooth;

            public void Execute(int i)
            {
                float3 vert = vertices[i];
                float y = vert.y;

                if (y <= bottom || y >= top)
                {
                    return;
                } 

                float normalizedDistanceBetweenBounds = (y - bottom) * oneDivHeight;
                if (smooth)
                {
                    //Manual smoothstep Mathf.SmoothStep(0f, 1f, normalizedDistanceBetweenBounds);
                    normalizedDistanceBetweenBounds = normalizedDistanceBetweenBounds * normalizedDistanceBetweenBounds * (3.0f - 2.0f * normalizedDistanceBetweenBounds);
                }

                float multiplier = amount * (normalizedDistanceBetweenBounds * normalizedDistanceBetweenBounds - normalizedDistanceBetweenBounds) + 1f;
                float x = vert.x * multiplier;
                float z = vert.z * multiplier;

                vert.x = x;
                vert.y = y;
                vert.z = z;
                vertices[i] = vert;
            }
        }

        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            DrawGizmo(
                helpers.Plane,
                helpers.TransparentBlue,
                objectMatrix * Matrix4x4.TRS(Vector3.up * top, Quaternion.Euler(90, 0, 0), Vector3.one)
            );

            DrawGizmo(
                helpers.Plane,
                helpers.TransparentBlue,
                objectMatrix * Matrix4x4.TRS(Vector3.up * bottom, Quaternion.Euler(90, 0, 0), Vector3.one)
            );
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();

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
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Bulge";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerBulge>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}