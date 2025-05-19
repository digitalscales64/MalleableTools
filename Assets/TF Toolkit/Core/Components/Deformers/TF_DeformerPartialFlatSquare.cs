using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerPartialFlatSquare : Deformer
    {
        const float MINIMUM_FALLOFF = 0.005f;
        public float fallOffDistance = 0.1f;
        public float planeDistance = 0.2f;
        public float sizeX = 1;
        public float sizeZ = 1;

        public override void UpdateVertices(DeformingMeshManager manager)
        {
            if (fallOffDistance <= 0)
            {
                if (sizeX <= 0 || sizeZ <= 0)
                {
                    return;
                }
            }

            manager.GetMinMax();

            if (fallOffDistance == 0)
            {
                for (int i = 0; i < manager.deformingMeshes.Length; i++)
                {
                    DeformingMesh dm = manager.deformingMeshes[i];
                    JobHandle jobHandle = manager.jobHandles[i];

                    manager.jobHandles[i] = new MeshJob_ZeroFallOff
                    {
                        vertices = dm.vertices,
                        planeDistance = planeDistance,
                        halfSizeX = sizeX / 2f,
                        halfSizeZ = sizeZ / 2f,
                        minMax = manager.minMax,
                    }
                    .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
                }
            }
            else
            {
                for (int i = 0; i < manager.deformingMeshes.Length; i++)
                {
                    DeformingMesh dm = manager.deformingMeshes[i];
                    JobHandle jobHandle = manager.jobHandles[i];

                    manager.jobHandles[i] = new MeshJob
                    {
                        vertices = dm.vertices,
                        planeDistance = planeDistance,
                        fallOffDistance = fallOffDistance,
                        halfSizeX = sizeX / 2f,
                        halfSizeZ = sizeZ / 2f,
                        minMax = manager.minMax,
                    }
                    .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
                }
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float planeDistance;
            public float fallOffDistance;
            public float halfSizeX;
            public float halfSizeZ;

            [ReadOnly]
            public NativeArray<float3> minMax;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                float4 min = minMax[0].y;
                float4 max = minMax[1].y;
                float4 height = math.max(max, -min);
                float4 compression = math.min(planeDistance / height, 1);

                float4 distanceX = math.max(math.abs(x) - halfSizeX, 0);
                float4 distanceZ = math.max(math.abs(z) - halfSizeZ, 0);

                float4 distanceSquared = distanceX * distanceX + distanceZ * distanceZ;
                float4 distance = math.sqrt(distanceSquared);

                y *= math.lerp(compression, 1, math.clamp(distance / fallOffDistance, 0, 1));

                ApplyXYZ(vertices, i, x, y, z, false);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_ZeroFallOff : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float planeDistance;
            public float halfSizeX;
            public float halfSizeZ;

            [ReadOnly]
            public NativeArray<float3> minMax;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                float4 min = minMax[0].y;
                float4 max = minMax[1].y;
                float4 height = math.max(max, -min);
                float4 compression = planeDistance / height;

                bool4 error =
                    height <= planeDistance |
                    math.abs(x) > halfSizeX | 
                    math.abs(z) > halfSizeZ
                ;

                y *= compression;

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }


        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            #region size
            DrawGizmo(
                helpers.Plane,
                helpers.TransparentBlue,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(90, 0, 0), new Vector3(sizeX, sizeZ, 1))
            );
            #endregion

            #region fallOffDistance
            DrawGizmo(
                helpers.Plane,
                helpers.TransparentRed,
                objectMatrix * Matrix4x4.TRS(Vector3.up * planeDistance, Quaternion.Euler(90, 0, 0), new Vector3(sizeX, sizeZ, 1))
            );
            DrawGizmo(
                helpers.Plane,
                helpers.TransparentRed,
                objectMatrix * Matrix4x4.TRS(Vector3.down * planeDistance, Quaternion.Euler(90, 0, 0), new Vector3(sizeX, sizeZ, 1))
            );
            #endregion
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();
            sizeX = Mathf.Max(sizeX, 0);
            sizeZ = Mathf.Max(sizeZ, 0);
            planeDistance = Mathf.Max(planeDistance, MINIMUM_FALLOFF * 0.5f);
            fallOffDistance = Mathf.Max(fallOffDistance, 0);
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Partial Flat Square";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerPartialFlatSquare>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}