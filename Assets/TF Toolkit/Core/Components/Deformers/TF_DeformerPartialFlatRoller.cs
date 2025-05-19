using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerPartialFlatRoller : Deformer
    {
        const float MINIMUM_FALLOFF = 0.005f;
        public float fallOffDistance = 0.1f;

        public float lengthBehindRoller = 3;
        public float width = 1;
        public float rollerRadius = 0.25f;
        public bool stuckOnRoller = false;
        public float flattenedHeight = MINIMUM_FALLOFF;

        public override void UpdateVertices(DeformingMeshManager manager)
        {
            manager.GetMinMax();

            for (int i = 0; i < manager.deformingMeshes.Length; i++)
            {
                var dm = manager.deformingMeshes[i];
                JobHandle previousJob = manager.jobHandles[i];

                manager.jobHandles[i] = new MeshJob
                {
                    vertices = dm.vertices,
                    fallOffDistance = fallOffDistance,
                    lengthBehindRoller = lengthBehindRoller,
                    width = width,
                    rollerRadius = rollerRadius,
                    stuckOnRoller = stuckOnRoller,
                    flattenedHeight = flattenedHeight,
                    minMax = manager.minMax,
                }
                .Schedule(dm.vertices.Length, 2048, previousJob);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float fallOffDistance;
            public float lengthBehindRoller;
            public float width;
            public float rollerRadius;
            public bool stuckOnRoller;
            public float flattenedHeight;

            [ReadOnly]
            public NativeArray<float3> minMax;

            public void Execute(int i)
            {
                Vector3 vert = vertices[i];
                float x = vert.x;
                float y = vert.y;
                float z = vert.z;

                float max = minMax[1].y;

                if (flattenedHeight >= max)
                {
                    return;
                }

                if (z <= -lengthBehindRoller)
                {
                    return; //Dont bother with making this transition nice
                }
                if (z > rollerRadius)
                {
                    return;
                }
                if (Mathf.Abs(x) > width / 2 + fallOffDistance)
                {
                    return;
                }

                float height = max;

                float zCompression = 1;
                if (z <= 0)
                {
                    zCompression = flattenedHeight / height;
                }
                else if (z < rollerRadius)
                {
                    float heightToRoller = rollerRadius - Mathf.Sqrt(rollerRadius * rollerRadius - z * z);

                    if (heightToRoller > height)
                    {
                        return;
                    }

                    zCompression = Mathf.Max(heightToRoller, flattenedHeight) / height;
                }

                float xDist = Mathf.Abs(x) - width / 2;

                float compression;
                if (fallOffDistance == 0)
                {
                    compression = zCompression;
                }
                else
                {
                    compression = Mathf.Lerp(1, zCompression, Mathf.InverseLerp(fallOffDistance, 0, xDist));
                }

                y *= compression;

                if (stuckOnRoller && z < 0)
                {
                    float circumference = rollerRadius * 2 * Mathf.PI;

                    float halfAngle = Mathf.PI * z / circumference;

                    Quaternion pointRotation = new Quaternion(Mathf.Sin(halfAngle), 0, 0, Mathf.Cos(halfAngle));

                    Vector3 point = pointRotation * new Vector3(0, y - rollerRadius, 0);

                    z = -point.z;
                    y = point.y + rollerRadius;
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
                helpers.Plane,
                helpers.TransparentBlue,
                objectMatrix * Matrix4x4.TRS(Vector3.back * lengthBehindRoller / 2, Quaternion.Euler(90, 0, 0), new Vector3(width, lengthBehindRoller, 1))
            );


            DrawGizmo(
                helpers.Cylinder,
                helpers.TransparentRed,
                objectMatrix * Matrix4x4.TRS(Vector3.up * rollerRadius, Quaternion.Euler(0, 0, 90), new Vector3(rollerRadius, width / 2, rollerRadius))
            );
            #endregion
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();
            rollerRadius = Mathf.Max(rollerRadius, 0);
            width = Mathf.Max(width, 0);
            lengthBehindRoller = Mathf.Max(lengthBehindRoller, 0);
            fallOffDistance = Mathf.Max(fallOffDistance, 0);
            flattenedHeight = Mathf.Max(flattenedHeight, MINIMUM_FALLOFF);
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Partial Flat Roller";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerPartialFlatRoller>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}