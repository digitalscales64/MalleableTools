using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerRectanglePush : Deformer
    {
        const string DEFORMER_NAME = "Square Push";

        public float fallOffDistance = 0.05f;
        public Vector2 planeSize = Vector2.one;
        public float pushDistance = 1;
        public float fallOffDistance_sides = 0.05f;

        protected override bool HasWork()
        {
            if (pushDistance <= 0)
            {
                return false;
            }
            if (planeSize.x <= 0 && planeSize.y <= 0 && fallOffDistance_sides <= 0)
            {
                return false;
            }
            return true;
        }

        protected override JobHandle UpdateVertices(DeformingMesh dm, JobHandle jobHandle = default)
        {
            if (fallOffDistance_sides <= 0)
            {
                return new MeshJob_ZeroFallOffSides
                {
                    vertices = dm.vertices,
                    pushDistance = pushDistance,
                    max = new float3(
                        planeSize.x * 0.5f + fallOffDistance_sides,
                        pushDistance + fallOffDistance,
                        planeSize.y * 0.5f + fallOffDistance_sides
                    ),
                    fallOffDistanceDivMaxY = fallOffDistance / (pushDistance + fallOffDistance),
                }
                .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);

            }
            else
            {
                return new MeshJob
                {
                    vertices = dm.vertices,
                    planeSize = planeSize * 0.5f,
                    pushDistance = pushDistance,
                    oneOverFallOffDistance_sides = 1f / fallOffDistance_sides,
                    max = new float3(
                        planeSize.x * 0.5f + fallOffDistance_sides,
                        pushDistance + fallOffDistance,
                        planeSize.y * 0.5f + fallOffDistance_sides
                    ),
                    fallOffDistanceDivMaxY = fallOffDistance / (pushDistance + fallOffDistance),
                }
                .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float2 planeSize;
            public float pushDistance;
            public float oneOverFallOffDistance_sides;
            public float3 max;
            public float fallOffDistanceDivMaxY;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                float4 absX = math.abs(x);
                float4 absZ = math.abs(z);

                bool4 error =
                    y <= 0 |
                    y >= max.y |
                    absX >= max.x |
                    absZ >= max.z
                ;

                float4 distToPlaneX = absX - planeSize.x;
                float4 distToPlaneZ = absZ - planeSize.y;
                float4 distToPlane = math.max(math.max(distToPlaneX, distToPlaneZ), 0);

                float4 newY = y * fallOffDistanceDivMaxY + pushDistance;

                y = math.lerp(newY, y, distToPlane * oneOverFallOffDistance_sides);

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_ZeroFallOffSides : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float pushDistance; 
            public float3 max;
            public float fallOffDistanceDivMaxY;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                float4 absX = math.abs(x);
                float4 absZ = math.abs(z);

                bool4 error =
                    y <= 0 |
                    y >= max.y |
                    absX >= max.x |
                    absZ >= max.z
                ;

                y = y * fallOffDistanceDivMaxY + pushDistance;

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }

        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            DrawGizmo(
                helpers.Cube,
                helpers.TransparentBlue,
                objectMatrix * Matrix4x4.TRS(Vector3.up * (pushDistance / 2), Quaternion.identity, new Vector3(planeSize.x, pushDistance, planeSize.y))
            );
            DrawGizmo(
                helpers.Cube,
                helpers.TransparentRed,
                objectMatrix * Matrix4x4.TRS(Vector3.up * ((pushDistance + fallOffDistance) / 2), Quaternion.identity, new Vector3(planeSize.x + fallOffDistance_sides, pushDistance + fallOffDistance, planeSize.y + fallOffDistance_sides))
            );
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();
            fallOffDistance = Mathf.Max(fallOffDistance, 0);
            planeSize.x = Mathf.Max(planeSize.x, 0);
            planeSize.y = Mathf.Max(planeSize.y, 0);
            pushDistance = Mathf.Max(pushDistance, 0);
            fallOffDistance_sides = Mathf.Max(fallOffDistance_sides, 0);
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerRectanglePush>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}
