using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerTwist : Deformer
    {
        public float rotations;
        public float fallOffDistance = 0.05f;
        public float middleWidthMultiplier = 1;

        protected override bool HasWork()
        {
            if (rotations == 0 && middleWidthMultiplier == 1)
            {
                return false;
            }

            return true;
        }

        protected override JobHandle UpdateVertices(DeformingMesh dm, JobHandle jobHandle = default)
        {
            if (fallOffDistance <= 0)
            {
                return new MeshJob_ZeroFallOff
                {
                    vertices = dm.vertices,
                    sin = Mathf.Sin(rotations * 2 * Mathf.PI),
                    cos = Mathf.Cos(rotations * 2 * Mathf.PI),
                }
                .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
            }

            if (middleWidthMultiplier == 1)
            {
                return new MeshJob_middleWidthMultiplierIsOne
                {
                    vertices = dm.vertices,
                    rotationsDivFallOffDistance = rotations * 2 * Mathf.PI / fallOffDistance,
                    fallOffDistance = fallOffDistance,
                }
                .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
            }

            return new MeshJob
            {
                vertices = dm.vertices,
                rotationsDivFallOffDistance = rotations * 2 * Mathf.PI / fallOffDistance,
                fallOffDistance = fallOffDistance,
                middleWidthMultiplier = middleWidthMultiplier,
            }
            .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float rotationsDivFallOffDistance;
            public float fallOffDistance;
            public float middleWidthMultiplier;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                bool4 error = y < 0;

                float4 degrees = rotationsDivFallOffDistance * math.min(y, fallOffDistance);

                float4 sin = math.sin(degrees);
                float4 cos = math.cos(degrees);

                float4 tx = x;
                float4 tz = z;
                x = (cos * tx) - (sin * tz);
                z = (sin * tx) + (cos * tz);

                float4 distLerpValue = 1 - 2 * y / fallOffDistance;

                float4 widthMultiplier = math.lerp(middleWidthMultiplier, 1, math.min(distLerpValue * distLerpValue, 1));

                x *= widthMultiplier;
                z *= widthMultiplier;

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_ZeroFallOff : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float sin;
            public float cos;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                bool4 error = y < 0;

                float4 tx = x;
                float4 tz = z;
                x = (cos * tx) - (sin * tz);
                z = (sin * tx) + (cos * tz);

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_middleWidthMultiplierIsOne : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float rotationsDivFallOffDistance;
            public float fallOffDistance;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                bool4 error = y < 0;

                float4 degrees = rotationsDivFallOffDistance * math.min(y, fallOffDistance);

                float4 sin = math.sin(degrees);
                float4 cos = math.cos(degrees);

                float4 tx = x;
                float4 tz = z;
                x = (cos * tx) - (sin * tz);
                z = (sin * tx) + (cos * tz);

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }

        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            DrawGizmo(
                helpers.Plane,
                helpers.TransparentBlue,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(90, 0 ,0), Vector3.one)
            );

            DrawGizmo(
                helpers.Plane,
                helpers.TransparentRed,
                objectMatrix * Matrix4x4.TRS(Vector3.up * fallOffDistance, Quaternion.Euler(90, -rotations * 360, 0), Vector3.one)
            );

            DrawGizmo(
                helpers.Cylinder,
                helpers.OpaqueRed,
                objectMatrix * Matrix4x4.TRS(Vector3.up * + fallOffDistance / 2, Quaternion.identity, new Vector3(0.01f, fallOffDistance / 2, 0.01f))
            );
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();

            fallOffDistance = Mathf.Max(fallOffDistance, 0);
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Twist";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerTwist>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}