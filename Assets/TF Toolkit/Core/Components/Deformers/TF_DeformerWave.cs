using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerWave : Deformer
    {
        public float heightX = 0.1f;
        public float distanceX = 0.4f;
        public float offsetX = 0;

        [Space(10)] // 10 pixels of spacing here.

        public float heightZ = 0;
        public float distanceZ = 0.4f;
        public float offsetZ = 0;

        protected override bool HasWork()
        {
            if (heightX == 0 && heightZ == 0)
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
                heightX = heightX,
                heightZ = heightZ,
                offsetX = offsetX,
                offsetZ = offsetZ,
                pi2OverDistanceX = Mathf.PI * 2 / distanceX,
                pi2OverDistanceZ = Mathf.PI * 2 / distanceZ,
            }
            .Schedule(dm.vertices.Length, 2048, jobHandle);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float heightX;
            public float heightZ;
            public float offsetX;
            public float offsetZ;
            public float pi2OverDistanceX;
            public float pi2OverDistanceZ;

            public void Execute(int i)
            {
                float3 vert = vertices[i];

                vert.y += math.cos((vert.x + offsetX) * pi2OverDistanceX) * heightX;
                vert.y += math.cos((vert.z + offsetZ) * pi2OverDistanceZ) * heightZ;

                vertices[i] = vert;
            }
        }

        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            DrawGizmo(
                helpers.Plane,
                helpers.TransparentBlue,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(90, 0, 0), Vector3.one)
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
            heightX = Mathf.Max(heightX, 0);
            heightZ = Mathf.Max(heightZ, 0);
            distanceX = Mathf.Max(distanceX, 0.01f);
            distanceZ = Mathf.Max(distanceZ, 0.01f);
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Wave";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerWave>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}