using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerSphere : Deformer
    {
        public enum Mode
        {
            Outside,
            Inside,
            Both
        }

        public Mode mode = Mode.Outside;

        public float radius = 0.5f;
        public float fallOffDistance = 0.05f;

        protected override bool HasWork()
        {
            if (mode == Mode.Outside && radius <= 0)
            {
                return false;
            }

            return true;
        }

        public override void UpdateVertices(DeformingMeshManager manager)
        {
            ValidateValues();

            if (!HasWork())
            {
                return;
            }

            if (mode == Mode.Outside)
            {
                float radiusPlusFallOff = radius + fallOffDistance;
                float val1 = 1 - radius / radiusPlusFallOff;

                for (int i = 0; i < manager.deformingMeshes.Length; i++)
                {
                    var dm = manager.deformingMeshes[i];
                    JobHandle jobHandle = manager.jobHandles[i];

                    manager.jobHandles[i] = new MeshJob_Outside
                    {
                        vertices = dm.vertices,
                        radiusPlusFallOff = radiusPlusFallOff,
                        radius = radius,
                        val1 = val1,
                    }
                    .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
                }
            }
            else if (mode == Mode.Inside)
            {
                if (radius <= 0)
                {
                    for (int i = 0; i < manager.deformingMeshes.Length; i++)
                    {
                        var dm = manager.deformingMeshes[i];
                        JobHandle jobHandle = manager.jobHandles[i];

                        manager.jobHandles[i] = new MeshJob_ZeroRadius
                        {
                            vertices = dm.vertices,
                     
                        }
                        .Schedule(dm.vertices.Length, 2048, jobHandle);
                    }
                }
                else
                {
                    var output = new NativeArray<float>(Math.Max(1, manager.deformingMeshes.Length), Allocator.TempJob);
                 
                    for (int i = 0; i < manager.deformingMeshes.Length; i++)
                    {
                        var dm = manager.deformingMeshes[i];
                        JobHandle jobHandle = manager.jobHandles[i];

                        manager.jobHandles[i] = new MeshJob_BiggestDistanceSquared
                        {
                            vertices = dm.vertices,
                            output = output,
                            meshIndex = i,
                        }
                        .Schedule(jobHandle);
                    }
      
                    JobHandle jobHandle_fixMax = new FindMaxJob
                    {
                        output = output
                    }
                    .Schedule(JobHandle.CombineDependencies(manager.jobHandles));
             
                    float minRadius = Math.Max(0, radius - fallOffDistance);

                    for (int i = 0; i < manager.deformingMeshes.Length; i++)
                    {
                        var dm = manager.deformingMeshes[i];

                        manager.jobHandles[i] = new MeshJob_Inside
                        {
                            vertices = dm.vertices,
                            output = output,
                            minRadius = minRadius,
                            radius = radius,
                        }
                        .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle_fixMax);
                    }

                    JobHandle allJobs = JobHandle.CombineDependencies(manager.jobHandles);
                    QueueNativeArrayDisposal(output, allJobs);
                }
            }
            else //if (mode == Mode.Both)
            {
                if (radius <= 0)
                {
                    Vector3 worldPosition = transform.position;
                    for (int i = 0; i < manager.deformingMeshes.Length; i++)
                    {
                        var dm = manager.deformingMeshes[i];
                        JobHandle jobHandle = manager.jobHandles[i];

                        manager.jobHandles[i] = new MeshJob_ZeroRadius
                        {
                            vertices = dm.vertices,
                        }
                        .Schedule(dm.vertices.Length, 2048, jobHandle);
                    }
                    return;
                }
                else
                {
                    var output = new NativeArray<float>(Math.Max(1, manager.deformingMeshes.Length), Allocator.TempJob);
                    for (int i = 0; i < manager.deformingMeshes.Length; i++)
                    {
                        var dm = manager.deformingMeshes[i];
                        JobHandle jobHandle = manager.jobHandles[i];

                        manager.jobHandles[i] = new MeshJob_BiggestDistanceSquared
                        {
                            vertices = dm.vertices,
                            output = output,
                            meshIndex = i,
                        }
                        .Schedule(jobHandle);
                    }

                    JobHandle jobHandle_fixMax = new FindMaxJob
                    {
                        output = output
                    }
                    .Schedule(JobHandle.CombineDependencies(manager.jobHandles));

                    float fallOff = Mathf.Min(radius, fallOffDistance * 0.5f);
                    float maxRadius = radius + fallOff;
                    float minRadius = radius - fallOff;
            
                    for (int i = 0; i < manager.deformingMeshes.Length; i++)
                    {
                        var dm = manager.deformingMeshes[i];

                        manager.jobHandles[i] = new MeshJob_Both
                        {
                            vertices = dm.vertices,
                            minRadius = minRadius,
                            maxRadiusMinusMinRadius = fallOff * 2,
                            output = output,
                        }
                        .Schedule(dm.vertices.Length, 2048, jobHandle_fixMax);
                        //.Schedule(dm.vertices.Length / 4 + 1, 512);
                    }

                    JobHandle allJobs = JobHandle.CombineDependencies(manager.jobHandles);
                    QueueNativeArrayDisposal(output, allJobs);
                }
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct FindMaxJob : IJob
        {
            public NativeArray<float> output;

            public void Execute()
            {
                float max = 0;
             
                for (int i = 0; i < output.Length; i++)
                {
                    max = math.max(max, output[i]);
                }
                output[0] = max;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_BiggestDistanceSquared : IJob
        {
            [ReadOnly]
            public NativeArray<Vector3> vertices;

            [WriteOnly]
            [NativeDisableContainerSafetyRestriction] //should be alright cause each job only writes to its own index
            public NativeArray<float> output;

            [return:AssumeRange(0, int.MaxValue)]
            public int meshIndex;

            public void Execute()
            {
                float4 max = 0;
                int imax = vertices.Length - 1;

                for (int i = 0; i < vertices.Length / 4 + 1; i++)
                {
                    int i4 = i * 4;
                    int i0 = math.min(i4 + 0, imax);
                    int i1 = math.min(i4 + 1, imax);
                    int i2 = math.min(i4 + 2, imax);
                    int i3 = math.min(i4 + 3, imax);
                    float3 vert0 = vertices[i0];
                    float3 vert1 = vertices[i1];
                    float3 vert2 = vertices[i2];
                    float3 vert3 = vertices[i3];
                    float4 x = new float4(vert0.x, vert1.x, vert2.x, vert3.x);
                    float4 y = new float4(vert0.y, vert1.y, vert2.y, vert3.y);
                    float4 z = new float4(vert0.z, vert1.z, vert2.z, vert3.z);

                    max = math.max(max, x * x + y * y + z * z);
                }
                output[meshIndex] = math.cmax(max);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_Outside : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float radiusPlusFallOff;
            public float radius;
            public float val1;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                float4 distance = math.sqrt(x * x + y * y + z * z);

                bool4 error =
                    distance >= radiusPlusFallOff |
                    distance == 0
               ;

                float4 multiplier = radius / distance + val1;
                x *= multiplier;
                y *= multiplier;
                z *= multiplier;

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_Inside : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float minRadius;
            public float radius;
       
            [ReadOnly]
            public NativeArray<float> output;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                float4 distance = math.sqrt(x * x + y * y + z * z);

                bool4 error = distance <= minRadius;

                float biggestDistance = output[0];
       
                float4 value = (distance - biggestDistance) / (minRadius - biggestDistance);
                float4 goalDistance = math.min(radius, biggestDistance) * (1 - value) + minRadius * value;

                float4 multiplier = goalDistance / distance;
                x *= multiplier;
                y *= multiplier;
                z *= multiplier;

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_Both : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float minRadius;
            public float maxRadiusMinusMinRadius;

            [ReadOnly]
            public NativeArray<float> output;

            //public void Execute(int i)
            //{
            //    PrepareXYZ(vertices, i, inMatrix, out float4 x, out float4 y, out float4 z);

            //    float4 distance = math.sqrt(x * x + y * y + z * z);

            //    bool4 error = distance == 0;

            //    float4 multiplier = minRadius / distance + val1;

            //    x *= multiplier;
            //    y *= multiplier;
            //    z *= multiplier;

            //    ApplyXYZ(vertices, i, outMatrix, x, y, z, false);
            //}

            //seems SIMD is a downgrade in this case, maybe because this math is just that simple?
            //maybe its automatically being turned into SIMD instructions better than what I'm able to pull off
            public void Execute(int i)
            {
                float3 vert = vertices[i];
                float x = vert.x;
                float y = vert.y;
                float z = vert.z;

                float biggestDistance = output[0];
                float val1 = maxRadiusMinusMinRadius / biggestDistance;

                float distance = math.sqrt(x * x + y * y + z * z);

                float multiplier = minRadius / distance + val1;

                x *= multiplier;
                y *= multiplier;
                z *= multiplier;

                vert.x = x;
                vert.y = y;
                vert.z = z;
                vertices[i] = vert;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_ZeroRadius : IJobParallelFor
        {
            [WriteOnly] public NativeArray<Vector3> vertices;
   
            public void Execute(int i)
            {
                vertices[i] = new Vector3(0, 0, 0);
            }
        }

        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            #region radius
            DrawGizmo(
                helpers.Sphere,
                helpers.TransparentBlue,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * radius * 2)
            );
            #endregion

            #region fallOffDistance
            if (mode == Mode.Outside)
            {
                DrawGizmo(
                    helpers.Sphere,
                    helpers.TransparentRed,
                    objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * (radius + fallOffDistance) * 2)
                );
            }
            else if (mode == Mode.Inside)
            {
                DrawGizmo(
                    helpers.Sphere,
                    helpers.TransparentRed,
                    objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * Mathf.Max(radius - fallOffDistance, 0) * 2)
                );
            }
            else //if (mode == Mode.Both)
            {
                DrawGizmo(
                    helpers.Sphere,
                    helpers.TransparentRed,
                    objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * (radius + fallOffDistance / 2) * 2)
                );
                DrawGizmo(
                    helpers.Sphere,
                    helpers.TransparentRed,
                    objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * Mathf.Max(radius - fallOffDistance / 2, 0) * 2)
                );
            }
            #endregion
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();
            radius = Mathf.Max(radius, 0);
            fallOffDistance = Mathf.Max(fallOffDistance, 0);
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Sphere";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerSphere>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }









































    //    public class TF_DeformerSphere : Deformer
    //    {
    //        public enum Mode
    //        {
    //            Outside,
    //            Inside,
    //            Both
    //        }

    //        public Mode mode = Mode.Outside;

    //        public float radius = 0.5f;
    //        public float fallOffDistance = 0.05f;

    //        protected override bool HasWork()
    //        {
    //            if (mode == Mode.Outside && radius <= 0)
    //            {
    //                return false;
    //            }

    //            return true;
    //        }

    //        public override void UpdateVertices(DeformingMeshManager manager)
    //        {
    //            ValidateValues();

    //            if (!HasWork())
    //            {
    //                return;
    //            }

    //            CodeTimer timer = new CodeTimer();

    //            if (mode == Mode.Outside)
    //            {
    //                float radiusPlusFallOff = radius + fallOffDistance;
    //                float val1 = 1 - radius / radiusPlusFallOff;

    //                var jobHandles = new NativeArray<JobHandle>(manager.deformingMeshes.Length, Allocator.Temp);
    //                for (int i = 0; i < manager.deformingMeshes.Length; i++)
    //                {
    //                    var dm = manager.deformingMeshes[i];

    //                    jobHandles[i] = new MeshJob_Outside
    //                    {
    //                        vertices = dm.vertices,
    //                        inMatrix = this.worldToLocalMatrix * dm.localToWorldMatrix,
    //                        outMatrix = dm.worldToLocalMatrix * this.localToWorldMatrix,
    //                        radiusPlusFallOff = radiusPlusFallOff,
    //                        radius = radius,
    //                        val1 = val1,
    //                    }
    //                    .Schedule(dm.vertices.Length / 4 + 1, 512);
    //                }
    //                JobHandle.CompleteAll(jobHandles);
    //                jobHandles.Dispose();
    //            }
    //            else if (mode == Mode.Inside)
    //            {
    //                if (radius <= 0)
    //                {
    //                    var jobHandles = new NativeArray<JobHandle>(manager.deformingMeshes.Length, Allocator.Temp);
    //                    for (int i = 0; i < manager.deformingMeshes.Length; i++)
    //                    {
    //                        var dm = manager.deformingMeshes[i];

    //                        jobHandles[i] = new MeshJob_ZeroRadius
    //                        {
    //                            vertices = dm.vertices,
    //                            point = dm.worldToLocalMatrix.MultiplyPoint3x4(this.worldPosition),
    //                        }
    //                        .Schedule(dm.vertices.Length, 2048);
    //                    }
    //                    JobHandle.CompleteAll(jobHandles);
    //                    jobHandles.Dispose();
    //                }
    //                else
    //                {
    //                    float biggestDistanceSquared = GetBiggestDistanceSquared(manager);

    //                    //var output = new NativeArray<float>(manager.deformingMeshes.Length, Allocator.TempJob);

    //                    //var jobHandles_minMax = new NativeArray<JobHandle>(manager.deformingMeshes.Length, Allocator.Temp);
    //                    //for (int i = 0; i < manager.deformingMeshes.Length; i++)
    //                    //{
    //                    //    var dm = manager.deformingMeshes[i];

    //                    //    jobHandles_minMax[i] = new MeshJob_BiggestDistanceSquared
    //                    //    {
    //                    //        vertices = dm.vertices,
    //                    //        output = output,
    //                    //        inMatrix = this.worldToLocalMatrix * dm.localToWorldMatrix,
    //                    //        meshIndex = i,
    //                    //    }
    //                    //    .Schedule();
    //                    //}
    //                    ////JobHandle.CompleteAll(jobHandles);

    //                    //JobHandle minMaxJob = JobHandle.CombineDependencies(jobHandles_minMax);

    //                    //jobHandles_minMax.Dispose();

    //                    //JobHandle jobHandle_fixMax = new FindMaxJob
    //                    //{
    //                    //    output = output,
    //                    //}
    //                    //.Schedule();














    //                    float minRadius = Math.Max(0, radius - fallOffDistance);
    //                    float minRadiusSquared = minRadius * minRadius;

    //                    if (biggestDistanceSquared <= minRadiusSquared)
    //                    {
    //                        return;
    //                    }

    //                    float biggestDistance = Mathf.Sqrt(biggestDistanceSquared);

    //                    float smallestOutOfRadiusAndBiggestDistance = Mathf.Min(radius, biggestDistance);

    //                    float oneOverMinRadiusMinusBiggestDistance = 1 / (minRadius - biggestDistance);

    //                    var jobHandles = new NativeArray<JobHandle>(manager.deformingMeshes.Length, Allocator.Temp);
    //                    for (int i = 0; i < manager.deformingMeshes.Length; i++)
    //                    {
    //                        var dm = manager.deformingMeshes[i];

    //                        jobHandles[i] = new MeshJob_Inside
    //                        {
    //                            vertices = dm.vertices,
    //                            inMatrix = this.worldToLocalMatrix * dm.localToWorldMatrix,
    //                            outMatrix = dm.worldToLocalMatrix * this.localToWorldMatrix,
    //                            biggestDistance = biggestDistance,
    //                            oneOverMinRadiusMinusBiggestDistance = oneOverMinRadiusMinusBiggestDistance,
    //                            smallestOutOfRadiusAndBiggestDistance = smallestOutOfRadiusAndBiggestDistance,
    //                            minRadius = minRadius,
    //                        }
    //                        .Schedule(dm.vertices.Length / 4 + 1, 512);
    //                    }
    //                    JobHandle.CompleteAll(jobHandles);
    //                    jobHandles.Dispose();
    //                }
    //            }
    //            else //if (mode == Mode.Both)
    //            {
    //                if (radius <= 0)
    //                {
    //                    var jobHandles = new NativeArray<JobHandle>(manager.deformingMeshes.Length, Allocator.Temp);
    //                    for (int i = 0; i < manager.deformingMeshes.Length; i++)
    //                    {
    //                        var dm = manager.deformingMeshes[i];

    //                        jobHandles[i] = new MeshJob_ZeroRadius
    //                        {
    //                            vertices = dm.vertices,
    //                            point = dm.worldToLocalMatrix.MultiplyPoint3x4(this.worldPosition),
    //                        }
    //                        .Schedule(dm.vertices.Length, 2048);
    //                    }
    //                    JobHandle.CompleteAll(jobHandles);
    //                    jobHandles.Dispose();
    //                    return;
    //                }
    //                else
    //                {
    //                    float biggestDistance = Mathf.Sqrt(GetBiggestDistanceSquared(manager));

    //                    if (biggestDistance == 0)
    //                    {
    //                        return;
    //                    }

    //                    float fallOff = Mathf.Min(radius, fallOffDistance * 0.5f);
    //                    float maxRadius = radius + fallOff;
    //                    float minRadius = radius - fallOff;
    //                    float val1 = maxRadius / biggestDistance - minRadius / biggestDistance;

    //                    var jobHandles = new NativeArray<JobHandle>(manager.deformingMeshes.Length, Allocator.Temp);
    //                    for (int i = 0; i < manager.deformingMeshes.Length; i++)
    //                    {
    //                        var dm = manager.deformingMeshes[i];

    //                        jobHandles[i] = new MeshJob_Both
    //                        {
    //                            vertices = dm.vertices,
    //                            inMatrix = this.worldToLocalMatrix * dm.localToWorldMatrix,
    //                            outMatrix = dm.worldToLocalMatrix * this.localToWorldMatrix,
    //                            minRadius = minRadius,
    //                            val1 = val1,
    //                        }
    //                        .Schedule(dm.vertices.Length, 2048);
    //                        //.Schedule(dm.vertices.Length / 4 + 1, 512);
    //                    }
    //                    JobHandle.CompleteAll(jobHandles);
    //                    jobHandles.Dispose();
    //                }
    //            }

    //            timer.addTime("UpdateVertices");
    //        }

    //        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
    //        struct FindMaxJob : IJob
    //        {
    //            public NativeArray<float> output;

    //            public void Execute()
    //            {
    //                float max = 0;
    //                for (int i = 0; i < output.Length; i++)
    //                {
    //                    max = math.max(max, output[i]);
    //                }
    //                output[0] = max;
    //            }
    //        }

    //        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
    //        struct MeshJob_BiggestDistanceSquared : IJob
    //        {
    //            [ReadOnly]
    //            public NativeArray<Vector3> vertices;

    //            [WriteOnly]
    //            public NativeArray<float> output;

    //            public Matrix4x4 inMatrix;

    //            //public int meshIndex;

    //            public void Execute()
    //            {
    //                float4 max = 0;
    //                int imax = vertices.Length - 1;

    //                for (int i = 0; i < vertices.Length / 4 + 1; i++)
    //                {
    //                    int i4 = i * 4;
    //                    int i0 = math.min(i4 + 0, imax);
    //                    int i1 = math.min(i4 + 1, imax);
    //                    int i2 = math.min(i4 + 2, imax);
    //                    int i3 = math.min(i4 + 3, imax);
    //                    float3 vert0 = math.transform(inMatrix, vertices[i0]);
    //                    float3 vert1 = math.transform(inMatrix, vertices[i1]);
    //                    float3 vert2 = math.transform(inMatrix, vertices[i2]);
    //                    float3 vert3 = math.transform(inMatrix, vertices[i3]);
    //                    float4 x = new float4(vert0.x, vert1.x, vert2.x, vert3.x);
    //                    float4 y = new float4(vert0.y, vert1.y, vert2.y, vert3.y);
    //                    float4 z = new float4(vert0.z, vert1.z, vert2.z, vert3.z);

    //                    max = math.max(max, x * x + y * y + z * z);
    //                }
    //                output[0] = math.cmax(max);
    //                //output[meshIndex] = math.cmax(max);
    //            }
    //        }

    //        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
    //        struct MeshJob_Outside : IJobParallelFor
    //        {
    //            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
    //            public Matrix4x4 inMatrix;
    //            public Matrix4x4 outMatrix;

    //            public float radiusPlusFallOff;
    //            public float radius;
    //            public float val1;

    //            public void Execute(int i)
    //            {
    //                PrepareXYZ(vertices, i, inMatrix, out float4 x, out float4 y, out float4 z);

    //                float4 distance = math.sqrt(x * x + y * y + z * z);

    //                bool4 error =
    //                    distance >= radiusPlusFallOff |
    //                    distance == 0
    //               ;

    //                float4 multiplier = radius / distance + val1;
    //                x *= multiplier;
    //                y *= multiplier;
    //                z *= multiplier;

    //                ApplyXYZ(vertices, i, outMatrix, x, y, z, error);
    //            }
    //        }

    //        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
    //        struct MeshJob_Inside : IJobParallelFor
    //        {
    //            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
    //            public Matrix4x4 inMatrix;
    //            public Matrix4x4 outMatrix;
    //            public float biggestDistance;
    //            public float oneOverMinRadiusMinusBiggestDistance;
    //            public float smallestOutOfRadiusAndBiggestDistance;
    //            public float minRadius;

    //            public void Execute(int i)
    //            {
    //                PrepareXYZ(vertices, i, inMatrix, out float4 x, out float4 y, out float4 z);

    //                float4 distance = math.sqrt(x * x + y * y + z * z);

    //                bool4 error = distance <= minRadius;

    //                float4 value = (distance - biggestDistance) * oneOverMinRadiusMinusBiggestDistance;
    //                float4 goalDistance = smallestOutOfRadiusAndBiggestDistance * (1 - value) + minRadius * value;

    //                float4 multiplier = goalDistance / distance;
    //                x *= multiplier;
    //                y *= multiplier;
    //                z *= multiplier;

    //                ApplyXYZ(vertices, i, outMatrix, x, y, z, error);
    //            }
    //        }

    //        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
    //        struct MeshJob_Both : IJobParallelFor
    //        {
    //            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
    //            public Matrix4x4 inMatrix;
    //            public Matrix4x4 outMatrix;

    //            public float minRadius;
    //            public float val1;

    //            //public void Execute(int i)
    //            //{
    //            //    PrepareXYZ(vertices, i, inMatrix, out float4 x, out float4 y, out float4 z);

    //            //    float4 distance = math.sqrt(x * x + y * y + z * z);

    //            //    bool4 error = distance == 0;

    //            //    float4 multiplier = minRadius / distance + val1;

    //            //    x *= multiplier;
    //            //    y *= multiplier;
    //            //    z *= multiplier;

    //            //    ApplyXYZ(vertices, i, outMatrix, x, y, z, false);
    //            //}

    //            //seems SIMD is a downgrade in this case, maybe because this math is just that simple?
    //            //maybe its automatically being turned into SIMD instructions better than what I'm able to pull off
    //            public void Execute(int i)
    //            {
    //                float3 vert = vertices[i];
    //                float x = inMatrix.m00 * vert.x + inMatrix.m01 * vert.y + inMatrix.m02 * vert.z + inMatrix.m03;
    //                float y = inMatrix.m10 * vert.x + inMatrix.m11 * vert.y + inMatrix.m12 * vert.z + inMatrix.m13;
    //                float z = inMatrix.m20 * vert.x + inMatrix.m21 * vert.y + inMatrix.m22 * vert.z + inMatrix.m23;

    //                float distance = math.sqrt(x * x + y * y + z * z);

    //                float multiplier = minRadius / distance + val1;

    //                x *= multiplier;
    //                y *= multiplier;
    //                z *= multiplier;

    //                vert.x = outMatrix.m00 * x + outMatrix.m01 * y + outMatrix.m02 * z + outMatrix.m03;
    //                vert.y = outMatrix.m10 * x + outMatrix.m11 * y + outMatrix.m12 * z + outMatrix.m13;
    //                vert.z = outMatrix.m20 * x + outMatrix.m21 * y + outMatrix.m22 * z + outMatrix.m23;
    //                vertices[i] = vert;
    //            }
    //        }

    //        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
    //        struct MeshJob_ZeroRadius : IJobParallelFor
    //        {
    //            [WriteOnly] public NativeArray<Vector3> vertices;
    //            public Vector3 point;

    //            public void Execute(int i)
    //            {
    //                vertices[i] = point;
    //            }
    //        }

    //        float GetBiggestDistanceSquared(DeformingMeshManager manager)
    //        {
    //            CodeTimer timer= new CodeTimer();
    //            var output = new NativeArray<float>[manager.deformingMeshes.Length];
    //            for (int i = 0; i < manager.deformingMeshes.Length; i++)
    //            {
    //                output[i] = new NativeArray<float>(1, Allocator.TempJob);
    //            }
    //            var jobHandles = new NativeArray<JobHandle>(manager.deformingMeshes.Length, Allocator.Temp);
    //            for (int i = 0; i < manager.deformingMeshes.Length; i++)
    //            {
    //                var dm = manager.deformingMeshes[i];

    //                jobHandles[i] = new MeshJob_BiggestDistanceSquared
    //                {
    //                    vertices = dm.vertices,
    //                    output = output[i],
    //                    inMatrix = this.worldToLocalMatrix * dm.localToWorldMatrix,
    //                }
    //                .Schedule();
    //            }
    //            JobHandle.CompleteAll(jobHandles);

    //            //JobHandle.CombineDependencies(jobHandles);

    //            jobHandles.Dispose();

    //            float biggestDistanceSquared = 0;
    //            for (int i = 0; i < manager.deformingMeshes.Length; i++) {
    //                biggestDistanceSquared = Mathf.Max(biggestDistanceSquared, output[i][0]);
    //                output[i].Dispose();
    //            }
    //            timer.addTime("biggestDistance");
    //            return biggestDistanceSquared;
    //        }

    //        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
    //        {
    //            #region radius
    //            DrawGizmo(
    //                helpers.Sphere,
    //                helpers.TransparentBlue,
    //                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * radius * 2)
    //            );
    //            #endregion

    //            #region fallOffDistance
    //            if (mode == Mode.Outside)
    //            {
    //                DrawGizmo(
    //                    helpers.Sphere,
    //                    helpers.TransparentRed,
    //                    objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * (radius + fallOffDistance) * 2)
    //                );
    //            }
    //            else if (mode == Mode.Inside)
    //            {
    //                DrawGizmo(
    //                    helpers.Sphere,
    //                    helpers.TransparentRed,
    //                    objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * Mathf.Max(radius - fallOffDistance, 0) * 2)
    //                );
    //            }
    //            else //if (mode == Mode.Both)
    //            {
    //                DrawGizmo(
    //                    helpers.Sphere,
    //                    helpers.TransparentRed,
    //                    objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * (radius + fallOffDistance / 2) * 2)
    //                );
    //                DrawGizmo(
    //                    helpers.Sphere,
    //                    helpers.TransparentRed,
    //                    objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * Mathf.Max(radius - fallOffDistance / 2, 0) * 2)
    //                );
    //            }
    //            #endregion
    //        }

    //        protected override void ValidateValues()
    //        {
    //            base.ValidateValues();
    //            radius = Mathf.Max(radius, 0);
    //            fallOffDistance = Mathf.Max(fallOffDistance, 0);
    //        }

    //#if UNITY_EDITOR
    //        const string DEFORMER_NAME = "Sphere";
    //        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
    //        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
    //        {
    //            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
    //            var createdDeformer = gameObject.AddComponent<TF_DeformerSphere>();
    //            AddNewDeformerToDeformable(createdDeformer);
    //        }
    //#endif
    //    }
}