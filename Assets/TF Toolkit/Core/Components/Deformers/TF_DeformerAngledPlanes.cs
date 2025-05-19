using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerAngledPlanes : Deformer
    {
        const float MINIMUM_FALLOFF = 0.005f;
        public float fallOffDistance = 0.1f;

        public float length = 1;
        public float width = 1;
        public float angle = 90;
        public float flattenedHeight = MINIMUM_FALLOFF;

        public override void UpdateVertices(DeformingMeshManager manager)
        {
            if (angle >= 90)
            {
                return;
            }
        
            float heightPerZ = Mathf.Sin(angle * Mathf.Deg2Rad) / Mathf.Sin(Mathf.PI / 2 - angle * Mathf.Deg2Rad);

            manager.GetMinMax();
            

            for (int i = 0; i < manager.deformingMeshes.Length; i++)
            {
                var dm = manager.deformingMeshes[i];
                JobHandle previousJob = manager.jobHandles[i];

                manager.jobHandles[i] = new MeshJob
                {
                    vertices = dm.vertices,
                    fallOffDistance = fallOffDistance,
                    length = length,
                    width = width,
                    flattenedHeight = flattenedHeight,
                    heightPerZ = heightPerZ,
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
            public float length;
            public float width;
            public float flattenedHeight;
            public float heightPerZ;

            [ReadOnly]
            public NativeArray<float3> minMax;

            public void Execute(int i)
            {
                Vector3 vert = vertices[i];
                float x = vert.x;
                float y = vert.y;
                float z = vert.z;

                float minY = minMax[0].y;
                float maxY = minMax[1].y;

                float height = maxY - minY;

                if (flattenedHeight >= height)
                {
                    return;
                }

                if (z <= -fallOffDistance)
                {
                    return;
                }
                if (z >= length + fallOffDistance)
                {
                    return;
                }
                if (math.abs(x) > width / 2 + fallOffDistance)
                {
                    return;
                }

                float zCompression;

                if (z < 0)
                {
                    zCompression = flattenedHeight / height;

                    zCompression = Mathf.Lerp(zCompression, 1, Mathf.InverseLerp(0, -fallOffDistance, z));
                }
                else if (z > length)
                {
                    float heightAtPoint = heightPerZ * length;

                    if (heightAtPoint > height)
                    {
                        return;
                    }

                    zCompression = Mathf.Max(heightAtPoint, flattenedHeight) / height;

                    zCompression = Mathf.Lerp(zCompression, 1, Mathf.InverseLerp(length, length + fallOffDistance, z));
                }
                else
                {
                    float heightAtPoint = heightPerZ * z;

                    if (heightAtPoint > height)
                    {
                        return;
                    }

                    zCompression = Mathf.Max(heightAtPoint, flattenedHeight) / height;
                }

                float xDist = math.abs(x) - width / 2;

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


            
                vert.x = x;
                vert.y = y;
                vert.z = z;
                vertices[i] = vert;
            }
        }

        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            DrawGizmo(
                helpers.Plane,
                helpers.TransparentBlue,
                objectMatrix * Matrix4x4.TRS(Vector3.forward * length / 2, Quaternion.Euler(-90, 0, 0), new Vector3(width, length, 1))
            );

            DrawGizmo(
                helpers.Plane,
                helpers.TransparentRed,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(-angle, 0, 0), Vector3.one) * Matrix4x4.TRS(Vector3.forward * length / 2, Quaternion.Euler(-90, 0, 0), new Vector3(width, length, 1))
            );
            DrawGizmo(
                helpers.Plane,
                helpers.TransparentRed,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(angle, 0, 0), Vector3.one) * Matrix4x4.TRS(Vector3.forward * length / 2, Quaternion.Euler(-90, 0, 0), new Vector3(width, length, 1))
            );
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();
            angle = Mathf.Clamp(angle, 0, 90);
            width = Mathf.Max(width, 0);
            length = Mathf.Max(length, 0);
            fallOffDistance = Mathf.Max(fallOffDistance, 0);
            flattenedHeight = Mathf.Max(flattenedHeight, MINIMUM_FALLOFF);
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Angled Planes";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerAngledPlanes>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}