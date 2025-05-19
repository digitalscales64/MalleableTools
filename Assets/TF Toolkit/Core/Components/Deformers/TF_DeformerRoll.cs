using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerRoll : Deformer
    {
        public float distanceBetweenLoops;

        protected override bool HasWork()
        {
            return true;
        }

        protected override JobHandle UpdateVertices(DeformingMesh dm, JobHandle jobHandle = default)
        {
            float PIoverDistance = Mathf.PI / distanceBetweenLoops;

            return new MeshJob
            {
                vertices = dm.vertices,
                PIoverDistance = PIoverDistance,
            }
            .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float PIoverDistance;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                float4 spiralLength = PIoverDistance * z * z;

                bool4 error = 
                    y > 0 | 
                    spiralLength == 0
                ;

                float4 newRadius = z * math.sqrt(1 + y / spiralLength);

                float4 halfAngle = (z - newRadius) * PIoverDistance;

                float4 qw = math.cos(halfAngle);

                bool4 condition = (-y >= spiralLength);
                y = math.select(0, -math.sin(halfAngle) * qw * newRadius * 2, !condition);
                z = math.select(0, (2 * qw * qw - 1) * newRadius, !condition);

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }


        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            //DrawGizmo(
            //    helpers.HalfSphereWithoutCap,
            //    helpers.OpaqueRed,
            //    objectMatrix * Matrix4x4.TRS(Vector3.right / 2, Quaternion.Euler(0, 0, -90), Vector3.one * 0.01f)
            //);
            //DrawGizmo(
            //    helpers.HalfSphereWithoutCap,
            //    helpers.OpaqueRed,
            //    objectMatrix * Matrix4x4.TRS(Vector3.left / 2, Quaternion.Euler(0, 0, 90), Vector3.one * 0.01f)
            //);
            DrawGizmo(
                helpers.Cylinder,
                helpers.OpaqueRed,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 90), new Vector3(0.01f, 0.5f, 0.01f))
            );

            DrawGizmo(
                helpers.Plane,
                helpers.TransparentBlue,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(90, 0, 0), Vector3.one)
            );
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();
            distanceBetweenLoops = Mathf.Max(distanceBetweenLoops, 0.01f);
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Roll";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerRoll>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}