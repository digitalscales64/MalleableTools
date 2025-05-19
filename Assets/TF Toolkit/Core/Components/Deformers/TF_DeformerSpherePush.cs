using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerSpherePush : Deformer
    {
        public float radius = 0.1f;
        public float height = 1.0f;
        public float fallOffDistance = 0.05f;

        protected override bool HasWork()
        {
            if (radius <= 0)
            {
                return false;
            }

            return true;
        }

        protected override JobHandle UpdateVertices(DeformingMesh dm, JobHandle jobHandle = default)
        {
            float top = height / 2;
            float outerRadius = radius + fallOffDistance;
            float outerRadiusSquared = outerRadius * outerRadius;
            float radiusSquared = radius * radius;
            float topHeight = top + fallOffDistance;

            if (fallOffDistance <= 0)
            {
                return new MeshJob_ZeroFallOff
                {
                    vertices = dm.vertices,
                    top = top,
                    radiusSquared = radiusSquared,
                }
                .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
            }

            return new MeshJob
            {
                vertices = dm.vertices,
                outerRadius = outerRadius,
                outerRadiusSquared = outerRadiusSquared,
                top = top,
                radiusSquared = radiusSquared,
                topHeight = topHeight,
                fallOffDistance = fallOffDistance,
            }
            .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float outerRadius;
            public float outerRadiusSquared;
            public float top;
            public float radiusSquared;
            public float topHeight;
            public float fallOffDistance;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                bool4 error = false;

                float4 xxzz = x * x + z * z;

                error |= xxzz >= outerRadiusSquared;
                error |= y <= 0;

                bool4 condition = xxzz >= radiusSquared;
                float4 minY = math.select(
                    top * (outerRadius - math.sqrt(xxzz)) / fallOffDistance,
                    top + math.sqrt(radiusSquared - xxzz), 
                    !condition
                );
                float4 maxY = math.select(
                    topHeight,
                    minY + fallOffDistance,
                    !condition
                );

                error |= y > maxY;

                y += minY * (1 - y / maxY);

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_ZeroFallOff : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float top;
            public float radiusSquared;
  
            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                bool4 error = false;

                float4 xxzz = x * x + z * z;
                float4 newY = top + math.sqrt(radiusSquared - xxzz);

                error |= xxzz >= radiusSquared;
                error |= y <= 0;
                error |= y > newY;

                y = newY;

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }


        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            void DrawCapsule(Material material, float _radius, float _height)
            {
                DrawGizmo(
                    helpers.HalfSphereWithoutCap,
                    material,
                    objectMatrix * Matrix4x4.TRS(Vector3.up * _height / 2, Quaternion.identity, Vector3.one * _radius)
                );
                DrawGizmo(
                    helpers.CylinderWithoutCaps,
                    material,
                    objectMatrix * Matrix4x4.TRS(Vector3.up * _height / 4, Quaternion.identity, new Vector3(_radius, _height / 4, _radius))
                );
            }


            #region radius
            DrawCapsule(helpers.TransparentBlue, radius, height);
            #endregion

            #region fallOffDistance

            DrawCapsule(helpers.TransparentRed, radius + fallOffDistance, height);
     
            #endregion
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();
            radius = Mathf.Max(radius, 0);
            height = Mathf.Max(height, 0);
            fallOffDistance = Mathf.Max(fallOffDistance, 0);
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Sphere Push";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerSpherePush>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}