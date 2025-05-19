using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerCube : Deformer
    {
        public enum Mode
        {
            Outside,
            Inside,
            Both
        }

        public Mode mode = Mode.Outside;

        public Vector3 size = Vector3.one;
        public float fallOffDistance = 0.05f;

        public override void UpdateVertices(DeformingMeshManager manager)
        {
            if (mode == Mode.Outside)
            {
                if (size.x == 0 && size.y == 0 && size.z == 0)
                {
                    return;
                }

                Vector3 innerSize = Vector3.Max(size - Vector3.one * fallOffDistance * 2, Vector3.zero);

                for (int i = 0; i < manager.deformingMeshes.Length; i++)
                {
                    var dm = manager.deformingMeshes[i];
                    JobHandle previousJob = manager.jobHandles[i];

                    manager.jobHandles[i] = new MeshJob_Outside
                    {
                        vertices = dm.vertices,
                        innerSize = innerSize / 2f,
                        size = size / 2f,
                        fallOffDistance = fallOffDistance,
                    }
                    .Schedule(dm.vertices.Length, 2048, previousJob);
                }
            }
            else if (mode == Mode.Inside)
            {
                Vector3 innerSize = Vector3.Max(size - Vector3.one * fallOffDistance * 2, Vector3.zero);

                manager.GetMinMax();

                for (int i = 0; i < manager.deformingMeshes.Length; i++)
                {
                    var dm = manager.deformingMeshes[i];
                    JobHandle previousJob = manager.jobHandles[i];

                    manager.jobHandles[i] = new MeshJob_Inside
                    {
                        vertices = dm.vertices,
                        innerSize = innerSize / 2f,
                        size = size / 2f,
                        minMax = manager.minMax,
                    }
                    .Schedule(dm.vertices.Length, 2048, previousJob);
                }
            }
            else //if (mode == Mode.Both)
            {
                Vector3 innerSize = Vector3.Max(size - Vector3.one * fallOffDistance, Vector3.zero);
                Vector3 outerSize = size + Vector3.one * fallOffDistance;

                manager.GetMinMax();

                for (int i = 0; i < manager.deformingMeshes.Length; i++)
                {
                    var dm = manager.deformingMeshes[i];
                    JobHandle previousJob = manager.jobHandles[i];

                    manager.jobHandles[i] = new MeshJob_Both
                    {
                        vertices = dm.vertices,
                        outerSize = outerSize / 2f,
                        innerSize = innerSize / 2f,
                        minMax = manager.minMax,
                        size = size / 2f,
                        fallOffDistance = fallOffDistance,
                    }
                    .Schedule(dm.vertices.Length, 2048, previousJob);
                }
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_Inside : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float3 innerSize;
            public float3 size;

            [ReadOnly]
            public NativeArray<float3> minMax;

            public void Execute(int i)
            {
                float3 vert = vertices[i];
                float x = vert.x;
                float y = vert.y;
                float z = vert.z;

                float3 min = minMax[0];
                float3 max = minMax[1];

                if (x > innerSize.x)
                {
                    x = math.lerp(innerSize.x, math.min(size.x, max.x), Mathf.InverseLerp(innerSize.x, max.x, x));
                }
                else if (x < -innerSize.x)
                {
                    x = math.lerp(-innerSize.x, math.max(-size.x, min.x), Mathf.InverseLerp(-innerSize.x, min.x, x));
                }

                if (y > innerSize.y)
                {
                    y = math.lerp(innerSize.y, math.min(size.y, max.y), Mathf.InverseLerp(innerSize.y, max.y, y));
                }
                else if (y < -innerSize.y)
                {
                    y = math.lerp(-innerSize.y, math.max(-size.y, min.y), Mathf.InverseLerp(-innerSize.y, min.y, y));
                }

                if (z > innerSize.z)
                {
                    z = math.lerp(innerSize.z, math.min(size.z, max.z), Mathf.InverseLerp(innerSize.z, max.z, z));
                }
                else if (z < -innerSize.z)
                {
                    z = math.lerp(-innerSize.z, math.max(-size.z, min.z), Mathf.InverseLerp(-innerSize.z, min.z, z));
                }

                vert.x = x;
                vert.y = y;
                vert.z = z;
                vertices[i] = vert;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_Outside : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float3 size;
            public float3 innerSize;
            public float fallOffDistance;
 
            public void Execute(int i)
            {
                float3 vert = vertices[i];
       
                float3 abs = math.abs(vert);
                float3 dist = size + fallOffDistance - abs;

                float smallestDist = math.cmin(dist);

                if (smallestDist < 0)
                {
                    return;
                }

                if (smallestDist == dist.x)
                {
                    vert.x = math.sign(vert.x) * (size.x + fallOffDistance * Mathf.InverseLerp(0, size.x + fallOffDistance, abs.x));
                }
                else if (smallestDist == dist.y)
                {
                    vert.y = math.sign(vert.y) * (size.y + fallOffDistance * Mathf.InverseLerp(0, size.y + fallOffDistance, abs.y));
                }
                else
                {
                    vert.z = math.sign(vert.z) * (size.z + fallOffDistance * Mathf.InverseLerp(0, size.z + fallOffDistance, abs.z));
                }

                vertices[i] = vert;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_Both : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float3 outerSize;
            public float3 size;
            public float3 innerSize;
            public float fallOffDistance;

            [ReadOnly]
            public NativeArray<float3> minMax;

            public void Execute(int i)
            {
                float3 vert = vertices[i];

                float3 abs = math.abs(vert);
                float3 dist = size - abs;

           
                float smallestDist = math.cmin(dist);

                //its outside
                float3 min = minMax[0];
                float3 max = minMax[1];

                if (vert.x > size.x)
                {
                    vert.x = math.lerp(size.x, math.min(outerSize.x, max.x), Mathf.InverseLerp(size.x, max.x, vert.x));
                }
                else if (vert.x < -size.x)
                {
                    vert.x = math.lerp(-size.x, math.max(-outerSize.x, min.x), Mathf.InverseLerp(-size.x, min.x, vert.x));
                }

                if (vert.y > size.y)
                {
                    vert.y = math.lerp(size.y, math.min(outerSize.y, max.y), Mathf.InverseLerp(size.y, max.y, vert.y));
                }
                else if (vert.y < -size.y)
                {
                    vert.y = math.lerp(-size.y, math.max(-outerSize.y, min.y), Mathf.InverseLerp(-size.y, min.y, vert.y));
                }

                if (vert.z > size.z)
                {
                    vert.z = math.lerp(size.z, math.min(outerSize.z, max.z), Mathf.InverseLerp(size.z, max.z, vert.z));
                }
                else if (vert.z < -size.z)
                {
                    vert.z = math.lerp(-size.z, math.max(-outerSize.z, min.z), Mathf.InverseLerp(-size.z, min.z, vert.z));
                }

                if (smallestDist > 0)
                {
                    //its inside
                    if (smallestDist == dist.x)
                    {
                        vert.x = math.sign(vert.x) * math.lerp(innerSize.x, size.x, Mathf.InverseLerp(0, size.x, abs.x));
                    }
                    else if (smallestDist == dist.y)
                    {
                        vert.y = math.sign(vert.y) * math.lerp(innerSize.y, size.y, Mathf.InverseLerp(0, size.y, abs.y));
                    }
                    else
                    {
                        vert.z = math.sign(vert.z) * math.lerp(innerSize.z, size.z, Mathf.InverseLerp(0, size.z, abs.z));
                    }
                }

                vertices[i] = vert;
            }
        }

        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            #region radius
            DrawGizmo(
                helpers.Cube,
                helpers.TransparentBlue,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, size)
            );
            #endregion

            #region fallOffDistance
            if (mode == Mode.Outside)
            {
                DrawGizmo(
                    helpers.Cube,
                    helpers.TransparentRed,
                    objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, size + Vector3.one * fallOffDistance * 2)
                );
            }
            else if (mode == Mode.Inside)
            {
                DrawGizmo(
                    helpers.Cube,
                    helpers.TransparentRed,
                    objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.Max(size - Vector3.one * fallOffDistance * 2, Vector3.zero))
                );
            }
            else //if (mode == Mode.Both)
            {
                DrawGizmo(
                    helpers.Cube,
                    helpers.TransparentRed,
                    objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, size + Vector3.one * fallOffDistance)
                );
                DrawGizmo(
                    helpers.Cube,
                    helpers.TransparentRed,
                    objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.Max(size - Vector3.one * fallOffDistance, Vector3.zero))
                );
            }
            #endregion
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();
            size = Vector3.Max(size, Vector3.zero);
            fallOffDistance = Mathf.Max(fallOffDistance, 0);
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Cube";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerCube>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}