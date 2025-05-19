using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerCapsule : Deformer
    {
        public enum Mode
        {
            Outside,
            Inside,
            Both
        }

        public Mode mode = Mode.Outside;

        public float radius = 0.2f;
        public float height = 1.0f;
        public float fallOffDistance = 0.05f;

        public override void UpdateVertices(DeformingMeshManager manager)
        {
            float top = height / 2;
      
            if (mode == Mode.Outside)
            {
                if (radius <= 0)
                {
                    return;
                }

                float outerRadius = radius + fallOffDistance;
                float val1 = 1 - radius / outerRadius;

                for (int i = 0; i < manager.deformingMeshes.Length; i++)
                {
                    var dm = manager.deformingMeshes[i];
                    JobHandle previousJob = manager.jobHandles[i];

                    manager.jobHandles[i] = new MeshJob_Outside
                    {
                        vertices = dm.vertices,
                        top = top,
                        radius = radius,
                        val1 = val1,
                    }
                    .Schedule(dm.vertices.Length / 4 + 1, 512, previousJob);
                }
            }
            else if (mode == Mode.Inside)
            {
                float innerRadius = Mathf.Max(radius - fallOffDistance, 0);

                JobHandle maxJob = GetBiggestDistance(manager, out NativeArray<float> output);

                float innerRadiusSquared = innerRadius * innerRadius;

                for (int i = 0; i < manager.deformingMeshes.Length; i++)
                {
                    var dm = manager.deformingMeshes[i];
                
                    manager.jobHandles[i] = new MeshJob_Inside
                    {
                        vertices = dm.vertices,
                        top = top,
                        innerRadiusSquared = innerRadiusSquared,
                        output = output,
                        innerRadius = innerRadius,
                        radiusMinusInnerRadius = radius - innerRadius,
                    }
                    .Schedule(dm.vertices.Length, 2048, maxJob);
                }

                JobHandle allJobs = JobHandle.CombineDependencies(manager.jobHandles);
                QueueNativeArrayDisposal(output, allJobs);
            }
            else //if (mode == Mode.Both)
            {
                JobHandle maxJob = GetBiggestDistance(manager, out NativeArray<float> output);

                float innerDistance = Mathf.Max(radius - fallOffDistance / 2, 0);
                float outerDistance = radius + fallOffDistance / 2;
              
                for (int i = 0; i < manager.deformingMeshes.Length; i++)
                {
                    var dm = manager.deformingMeshes[i];
              
                    manager.jobHandles[i] = new MeshJob_Both
                    {
                        vertices = dm.vertices,
                        top = top,
                        innerDistance = innerDistance,
                        output = output,
                        outerMinusInnerDistance = outerDistance - innerDistance,
                    }
                    .Schedule(dm.vertices.Length, 2048, maxJob);
                }

                JobHandle allJobs = JobHandle.CombineDependencies(manager.jobHandles);
                QueueNativeArrayDisposal(output, allJobs);
            }
            
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_Outside : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float radius;
            public float top;
            public float val1;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                float4 xxzz = x * x + z * z;

                float4 signY = math.sign(y);
                float4 absY = y * signY;

                float4 yMinusTop = math.max(absY - top, 0);
                float4 lengthSquared = xxzz + yMinusTop * yMinusTop;

                float4 multiplier = radius / math.sqrt(lengthSquared) + val1;

                multiplier = math.max(multiplier, 1);

                y = math.select(
                    y,
                    (yMinusTop * multiplier + top) * signY,
                    yMinusTop > 0
                );

                x *= multiplier;
                z *= multiplier;

                ApplyXYZ(vertices, i, x, y, z, false);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_Inside : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float top;
            public float innerRadiusSquared;
            public float val1;
            public float val2;
            public float innerRadius;
            public float radiusMinusInnerRadius;

            [ReadOnly]
            public NativeArray<float> output;

            public void Execute(int i)
            {
                float biggestDistance = output[0];
                if (biggestDistance <= innerRadius)
                {
                    return;
                }

                float3 vert = vertices[i];
                float x = vert.x;
                float y = vert.y;
                float z = vert.z;

                
                float val1 = radiusMinusInnerRadius / (biggestDistance - innerRadius);
                float val2 = innerRadius * (1 - val1);

                float signY = y > 0 ? 1 : -1;
                float absY = y * signY;

                float multiplier;
                if (absY > top)
                {
                    float yMinusTop = absY - top;
                    float distSquared = x * x + z * z + yMinusTop * yMinusTop;
                    if (distSquared < innerRadiusSquared)
                    {
                        return;
                    }

                    multiplier = val1 + val2 / math.sqrt(distSquared);
                    y = (yMinusTop * multiplier + top) * signY;
                }
                else
                {
                    //between
                    float xxzz = x * x + z * z;
                    if (xxzz < innerRadiusSquared)
                    {
                        return;
                    }

                    multiplier = val1 + val2 / math.sqrt(xxzz);
                }

                x *= multiplier;
                z *= multiplier;

                vert.x = x;
                vert.y = y;
                vert.z = z;
                vertices[i] = vert;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_Both : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float top;
            public float innerDistance;
            public float outerMinusInnerDistance;

            [ReadOnly]
            public NativeArray<float> output;


            public void Execute(int i)
            {
                float biggestDistance = output[0];
                if (biggestDistance == 0)
                {
                    return; //everything is on the center line, cant define a direction to push things
                }

                Vector3 vert = vertices[i];
                float x = vert.x;
                float y = vert.y;
                float z = vert.z;

                float val_1 = outerMinusInnerDistance / biggestDistance;

                float signY = y > 0 ? 1 : -1;
                float absY = y * signY;
                
                float multiplier;
                if (absY > top)
                {
                    float yMinusTop = absY - top;
                    multiplier = innerDistance / math.sqrt(x * x + z * z + yMinusTop * yMinusTop) + val_1;
                    y = (yMinusTop * multiplier + top) * signY;
                }
                else
                {
                    //betweeen
                    multiplier = innerDistance / math.sqrt(x * x + z * z) + val_1;
                }
                x *= multiplier;
                z *= multiplier;

                vert.x = x;
                vert.y = y;
                vert.z = z;
                vertices[i] = vert;
            }
        }

        JobHandle GetBiggestDistance(DeformingMeshManager manager, out NativeArray<float> output)
        {
            float top = height / 2f;
        
            output = new NativeArray<float>(Mathf.Max(1, manager.deformingMeshes.Length), Allocator.TempJob);
            for (int i = 0; i < manager.deformingMeshes.Length; i++)
            {
                var dm = manager.deformingMeshes[i];
                JobHandle previousJob = manager.jobHandles[i];

                manager.jobHandles[i] = new MeshJob_GetBiggestDistance_1
                {
                    vertices = dm.vertices,
                    output = output,
                    top = top,
                    jobIndex = i,
                }
                .Schedule(previousJob);
            }

            return new MeshJob_GetBiggestDistance_2
            {
                output = output,
            }
            .Schedule(JobHandle.CombineDependencies(manager.jobHandles));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_GetBiggestDistance_1 : IJob
        {
            [ReadOnly]
            public NativeArray<Vector3> vertices;

            [WriteOnly]
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<float> output;

            public float top;

            public int jobIndex;

            public void Execute()
            {
                float max = 0;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 vert = vertices[i];
                    float x = vert.x;
                    float y = vert.y;
                    float z = vert.z;

                    float absY = math.abs(y);

                    float yMinusTop = absY > top ? absY - top : 0; //math.max(absY - top, 0);

                    float lengthSquared = x * x + z * z + yMinusTop * yMinusTop;

                    max = max > lengthSquared ? max : lengthSquared;//math.max(max, x * x + z * z + yMinusTop * yMinusTop);
                }
                output[jobIndex] = max;
            }
        }
        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_GetBiggestDistance_2 : IJob
        {
            public NativeArray<float> output;

            public void Execute()
            {
                float max = 0;
                for (int i = 0; i < output.Length; i++)
                {
                    float val = output[i];

                    max = val > max ? val : max;//math.max(max, output[i]);
                }
                output[0] = math.sqrt(max);
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


            #region radius
            DrawCapsule(helpers.TransparentBlue, radius, height);
            #endregion

            #region fallOffDistance
            if (mode == Mode.Outside)
            {
                DrawCapsule(helpers.TransparentRed, radius + fallOffDistance, height);
            }
            else if (mode == Mode.Inside)
            {
                DrawCapsule(helpers.TransparentRed, Mathf.Max(radius - fallOffDistance, 0), height);
            }
            else //if (mode == Mode.Both)
            {
                DrawCapsule(helpers.TransparentRed, radius + fallOffDistance / 2, height);
                DrawCapsule(helpers.TransparentRed, Mathf.Max(radius - fallOffDistance / 2, 0), height);
            }
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
        const string DEFORMER_NAME = "Capsule";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerCapsule>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}