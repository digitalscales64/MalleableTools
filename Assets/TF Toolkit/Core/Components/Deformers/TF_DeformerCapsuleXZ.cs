using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerCapsuleXZ : Deformer
    {
        public float radius = 0.2f;
        public float height = 1.0f;
        public float fallOffDistance = 0.05f;
        public float push = 0;

        protected override bool HasWork()
        {
            if (radius <= 0)
            {
                return false;
            }
            return true;
        }

        protected override JobHandle UpdateVertices(DeformingMesh dm, JobHandle jobHandle)
        {
            float top = height / 2;
            float outerRadius = radius + fallOffDistance;
            float outerRadiusSquared = outerRadius * outerRadius;
            float val1 = 1 - radius / outerRadius;

            return new MeshJob
            {
                vertices = dm.vertices,
                radius = radius,
                fallOffDistance = fallOffDistance,
                push = push,
                top = top,
                outerRadius = outerRadius,
                outerRadiusSquared = outerRadiusSquared,
                val1 = val1,
            }
            .Schedule(dm.vertices.Length, 2048, jobHandle);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float radius;
            public float fallOffDistance;
            public float push;

            public float top;
            public float outerRadius;
            public float outerRadiusSquared;
            public float val1;

            public void Execute(int i)
            {
                float3 vert = vertices[i];
                float x = vert.x;
                float y = vert.y;
                float z = vert.z;

                float distanceSquared = x * x + z * z;

                if (distanceSquared >= outerRadiusSquared)
                {
                    return; //outside endless cylinder
                }

                float distance = math.sqrt(distanceSquared);


                float multiplier;

                float pushAmount;

                float absY = math.abs(y);

                if (absY >= top + radius)
                {
                    return;
                }

                if (absY > top)
                {
                    float h = absY - top;
                    float radiusAtY = math.sqrt(radius * radius - h * h);

                    if (distance >= radiusAtY + fallOffDistance)
                    {
                        return;
                    }

                    float targetDistance = radiusAtY + fallOffDistance * distance / (radiusAtY + fallOffDistance);

                    pushAmount = push * Mathf.InverseLerp(radiusAtY + fallOffDistance, radiusAtY, distance);
                    pushAmount *= (top + radius - absY) / radius;//Mathf.InverseLerp(top + radius, top, absY);


                    multiplier = targetDistance / distance;

                }
                else
                {
                    multiplier = radius / distance + val1;
                    pushAmount = push * Mathf.InverseLerp(radius + fallOffDistance, radius, distance);
                }

                x *= multiplier;
                z *= multiplier;
                y += pushAmount;

                vert.x = x;
                vert.y = y;
                vert.z = z;
                vertices[i] = vert;
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
                    helpers.HalfSphereWithoutCap,
                    material,
                    objectMatrix * Matrix4x4.TRS(Vector3.down * _height / 2, Quaternion.Euler(180, 0, 0), Vector3.one * _radius)
                );
                DrawGizmo(
                    helpers.CylinderWithoutCaps,
                    material,
                    objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_radius, _height / 2, _radius))
                );
            }

            //radius
            DrawCapsule(helpers.TransparentBlue, radius, height);

            //fallOffDistance
            DrawCapsule(helpers.TransparentRed, radius + fallOffDistance, height);
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();
            radius = Mathf.Max(radius, 0);
            height = Mathf.Max(height, 0);
            fallOffDistance = Mathf.Max(fallOffDistance, 0);
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Capsule XZ";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerCapsuleXZ>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}