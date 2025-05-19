using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerPartialFlatCircle : Deformer
    {
        const float MINIMUM_FALLOFF = 0.005f;
        public float fallOffDistance = 0.1f;
        public float planeDistance = 0.2f;
        public float radius = 0.5f;

        public override void UpdateVertices(DeformingMeshManager manager)
        {
            if (fallOffDistance <= 0 && radius <= 0)
            {
                return;
            }

            manager.GetMinMax();

            for (int i = 0; i < manager.deformingMeshes.Length; i++)
            {
                DeformingMesh dm = manager.deformingMeshes[i];
                JobHandle jobHandle = manager.jobHandles[i];

                manager.jobHandles[i] = new MeshJob
                {
                    vertices = dm.vertices,
                    fallOffDistance = fallOffDistance,
                    planeDistance = planeDistance,
                    radius = radius,
                    minMax = manager.minMax,
                }
                .Schedule(dm.vertices.Length, 2048, jobHandle);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float fallOffDistance;
            public float planeDistance;
            public float radius;

            [ReadOnly]
            public NativeArray<float3> minMax;

            public void Execute(int i)
            {
                Vector3 vert = vertices[i];
                float x = vert.x;
                float y = vert.y;
                float z = vert.z;

                float min = minMax[0].y;
                float max = minMax[1].y;
                float height = math.max(max, -min);
                float compression = planeDistance / height;

                if (max <= planeDistance && min >= -planeDistance)
                {
                    return;
                }

                float xxzz = x * x + z * z;

                if (xxzz > (fallOffDistance + radius) * (fallOffDistance + radius))
                {
                    return;
                }

                if (fallOffDistance == 0 || xxzz <= radius * radius)
                {
                    y *= compression;
                }
                else
                {
                    float distance = math.sqrt(xxzz) - radius;
                    y *= Mathf.Lerp(compression, 1, Mathf.InverseLerp(0, fallOffDistance, distance));
                }

                vert.x = x;
                vert.y = y;
                vert.z = z;
                vertices[i] = vert;
            }
        }

        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            #region size
            DrawGizmo(
                helpers.Circle,
                helpers.TransparentBlue,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(90, 0, 0), new Vector3(radius, radius, 1))
            );
            #endregion
            
            #region fallOffDistance
            DrawGizmo(
                helpers.Circle,
                helpers.TransparentRed,
                objectMatrix * Matrix4x4.TRS(Vector3.up * planeDistance, Quaternion.Euler(90, 0, 0), new Vector3(radius, radius, 1))
            );
            DrawGizmo(
                helpers.Circle,
                helpers.TransparentRed,
                objectMatrix * Matrix4x4.TRS(Vector3.down * planeDistance, Quaternion.Euler(90, 0, 0), new Vector3(radius, radius, 1))
            );
            #endregion
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();
            radius = Mathf.Max(radius, 0);
            planeDistance = Mathf.Max(planeDistance, MINIMUM_FALLOFF * 0.5f);
            fallOffDistance = Mathf.Max(fallOffDistance, 0);
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Partial Flat Circle";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerPartialFlatCircle>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}