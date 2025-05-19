using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace TF_Toolkit
{
    public class TF_DeformerTube : Deformer
    {
        public float radius = 0.1f;
        public float effectRadius = 0.4f;
        public float height = 0.3f;
        public float fallOffDistance = 0.05f;
        public float fallOffHeight = 0.1f;

        protected override bool HasWork()
        {
            if (effectRadius <= 0)
            {
                return false;
            }
            if (radius == effectRadius)
            {
                return false;
            }
            if (height == 0 && fallOffHeight == 0)
            {
                return false;
            }

            return true;
        }

        protected override JobHandle UpdateVertices(DeformingMesh dm, JobHandle jobHandle = default)
        {
            float top = height / 2;
            float innerRadius = Mathf.Max(radius - fallOffDistance, 0);
            float topPlusFallOff = top + fallOffHeight;
            float effectRadiusSquared = effectRadius * effectRadius;
            float innerRadiusSquared = innerRadius * innerRadius;

            if (fallOffHeight <= 0)
            {
                return new MeshJob_ZeroFallOffHeight
                {
                    vertices = dm.vertices,
                    innerRadius = innerRadius,
                    topPlusFallOff = topPlusFallOff,
                    effectRadiusSquared = effectRadiusSquared,
                    innerRadiusSquared = innerRadiusSquared,
                    radius = radius,
                    effectRadius = effectRadius,
                }
                .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
            }

            return new MeshJob
            {
                vertices = dm.vertices,
                top = top,
                innerRadius = innerRadius,
                topPlusFallOff = topPlusFallOff,
                effectRadiusSquared = effectRadiusSquared,
                innerRadiusSquared = innerRadiusSquared,
                radius = radius,
                effectRadius = effectRadius,
                fallOffHeight = fallOffHeight,
            }
            //.Schedule(dm.vertices.Length, 2048, jobHandle);
            .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float top;
            public float innerRadius;
            public float topPlusFallOff;
            public float effectRadiusSquared;
            public float innerRadiusSquared;

            public float radius;
            public float effectRadius;
            public float fallOffHeight;

            //public void Execute(int i)
            //{
            //    float3 vert = vertices[i];//PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

            //    float absY = math.abs(vert.y);
            //    float xxzz = vert.x * vert.x + vert.z * vert.z;
            //    float distance = math.sqrt(xxzz);

            //    float val1 = (distance - effectRadius) / (innerRadius - effectRadius);
            //    float goalDistance = math.lerp(radius, innerRadius, val1);

            //    float val2 = 1 - math.max(absY - top, 0) / fallOffHeight;
            //    goalDistance = math.lerp(distance, goalDistance, val2);

            //    vert.x *= goalDistance / distance;
            //    vert.z *= goalDistance / distance;

            //    bool error =
            //        absY >= topPlusFallOff |
            //        xxzz >= effectRadiusSquared |
            //        xxzz <= innerRadiusSquared
            //    ;



            //    vertices[i] = error ? vertices[i] : vert;

            //    //ApplyXYZ(vertices, i, x, y, z, error);
            //}

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                float4 absY = math.abs(y);
                float4 xxzz = x * x + z * z;
                float4 distance = math.sqrt(xxzz);

                float4 val1 = (distance - effectRadius) / (innerRadius - effectRadius);
                float4 goalDistance = math.lerp(radius, innerRadius, val1);

                float4 val2 = 1 - math.max(absY - top, 0) / fallOffHeight;
                goalDistance = math.lerp(distance, goalDistance, val2);

                x *= goalDistance / distance;
                z *= goalDistance / distance;

                bool4 error =
                    absY >= topPlusFallOff |
                    xxzz >= effectRadiusSquared |
                    xxzz <= innerRadiusSquared
                ;

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_ZeroFallOffHeight : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float innerRadius;
            public float topPlusFallOff;
            public float effectRadiusSquared;
            public float innerRadiusSquared;

            public float radius;
            public float effectRadius;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                float4 absY = math.abs(y);
                float4 xxzz = x * x + z * z;
                float4 distance = math.sqrt(xxzz);

                float4 val1 = (distance - effectRadius) / (innerRadius - effectRadius);
                float4 goalDistance = math.lerp(radius, innerRadius, val1);
                float4 multiplier = goalDistance / distance;

                x *= multiplier;
                z *= multiplier;

                bool4 error =
                    absY >= topPlusFallOff |
                    xxzz >= effectRadiusSquared |
                    xxzz <= innerRadiusSquared
                ;

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }


        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            //EffectRadius
            DrawGizmo(
                helpers.Cylinder,
                helpers.TransparentRed,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(effectRadius, height / 2 + fallOffHeight, effectRadius))
            );

            //radius
            DrawGizmo(
                helpers.Cylinder,
                helpers.TransparentBlue,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(radius, height / 2, radius))
            );

            float minRadius = Mathf.Max(radius - fallOffDistance, 0);
            DrawGizmo(
                helpers.Cylinder,
                helpers.TransparentRed,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(minRadius, height / 2, minRadius))
            );
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();
            radius = Mathf.Max(radius, 0);
            effectRadius = Mathf.Max(effectRadius, radius);
            height = Mathf.Max(height, 0);
            fallOffDistance = Mathf.Max(fallOffDistance, 0);
            fallOffHeight = Mathf.Max(fallOffHeight, 0);
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Tube";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerTube>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}