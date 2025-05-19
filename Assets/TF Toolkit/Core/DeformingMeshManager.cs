using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using System.Linq;

namespace TF_Toolkit
{
    public class DeformingMeshManager : MonoBehaviour
    {
        [Tooltip("If it isn't causing your model to look messed up, you can keep this disabled for better performance.")]
        public bool updateNormals = true;

        [Tooltip("Can be disabled for better performance, but then the model will only appear while the camera is facing where the model was before deformations were applied.")]
        public bool updateBounds = true;

        [Tooltip("Should deformations be applied while the Unity editor is in Edit mode?")]
        public bool updateInEditMode = true;

        [Tooltip("The deformers acting on the object. They are applied from top to bottom.")]
        public Deformer[] deformers = new Deformer[0];

        [Tooltip("How many seconds should we wait between each time we check if the object has new renderers or if the existing ones have been enabled / disabled?\nLess checks = better performance.\nSet -1 to never check.")]
        public float renderersChangedCheckCooldown = 0;

        


        [HideInInspector] public bool initialized = false;  
        [HideInInspector] public bool wasRunningInEditMode = false;

        [HideInInspector] public Mesh[] ORIGINAL_MESHES = new Mesh[0];
        [HideInInspector] public int meshCount = 0;
        [HideInInspector] public Mesh[] meshes = new Mesh[0];
        [HideInInspector] public DeformingMesh[] deformingMeshes = new DeformingMesh[0];
        [HideInInspector] public List<SkinnedMeshRenderer> renderers = new List<SkinnedMeshRenderer>();
        [HideInInspector] public List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
        [HideInInspector] public List<MeshFilter> meshFilters = new List<MeshFilter>();

        [HideInInspector] public bool DMM_enabled = false;

        [HideInInspector] public NativeArray<JobHandle> jobHandles;
        [HideInInspector] public NativeArray<float3> minMax;
        [HideInInspector] public NativeArray<Bounds> bounds;
        [HideInInspector] public NativeArray<Matrix4x4> outMatrix;

        public void RecreateMeshes(List<SkinnedMeshRenderer> renderers, List<MeshRenderer> meshRenderers, List<MeshFilter> meshFilters)
        {
            //CodeTimer timer = new CodeTimer();

            DestroyMeshes();
   
            meshCount = renderers.Count + meshRenderers.Count;

            this.renderers = renderers;
            this.meshRenderers = meshRenderers;
            this.meshFilters = meshFilters;

            ORIGINAL_MESHES = new Mesh[meshCount];
            meshes = new Mesh[meshCount];
            deformingMeshes = new DeformingMesh[meshCount];

            int meshIndex = 0;
            for (int i = 0; i < renderers.Count; i++)
            {
                ORIGINAL_MESHES[meshIndex] = renderers[i].sharedMesh;
                meshIndex++;
            }
            for (int i = 0; i < meshRenderers.Count; i++)
            {
                ORIGINAL_MESHES[meshIndex] = meshFilters[i].sharedMesh;
                meshIndex++;
            }

            using (var readonlyMeshData = Mesh.AcquireReadOnlyMeshData(ORIGINAL_MESHES))
            {
                meshIndex = 0;
                for (int i = 0; i < renderers.Count; i++)
                {
                    DeformingMesh deformingMesh = ScriptableObject.CreateInstance<DeformingMesh>();
                    deformingMesh.Init(renderers[i], readonlyMeshData[meshIndex]);
                    deformingMeshes[meshIndex] = deformingMesh;
                    meshes[meshIndex] = deformingMesh.mesh;

                    meshIndex++;
                }
                for (int i = 0; i < meshRenderers.Count; i++)
                {
                    DeformingMesh deformingMesh = ScriptableObject.CreateInstance<DeformingMesh>();
                    deformingMesh.Init(meshFilters[i], readonlyMeshData[meshIndex]);
                    deformingMeshes[meshIndex] = deformingMesh;
                    meshes[meshIndex] = deformingMesh.mesh;

                    meshIndex++;
                }
            }

            DisposeArrays();
            CreateArrays();

            //timer.addTime("Recreate meshes");
        }

        public void Deform(Deformer[] deformers)
        {
            //before first deform this frame
            UpdateRootBones();

            for (int i = 0; i < deformingMeshes.Length; i++)
            {
                DeformingMesh deformingMesh = deformingMeshes[i];
                
                jobHandles[i] = deformingMesh.BakeMesh();
                outMatrix[i] = deformingMesh.GetOutMatrix();
            }

            

            if (updateNormals)
            {
                foreach (var deformingMesh in deformingMeshes)
                {
                    deformingMesh.StartZeroNormalsJob();
                }
            }
         
            Enable();

            JobHandle.ScheduleBatchedJobs();

            foreach (Deformer deformer in deformers)
            {
                if (deformer == null || !deformer.isActiveAndEnabled)
                {
                    continue;
                }

                Matrix4x4 deformerWorldToLocalMatrix = deformer.transform.worldToLocalMatrix;
                Matrix4x4 deformerLocalToWorldMatrix = deformer.transform.localToWorldMatrix;

                for (int i = 0; i < deformingMeshes.Length; i++)
                {
                    DeformingMesh deformingMesh = deformingMeshes[i];
                    Matrix4x4 inMatrix = deformerWorldToLocalMatrix * deformingMesh.localToWorldMatrix;

                    DeformByMatrix(i, inMatrix * outMatrix[i]);

                    outMatrix[i] = deformingMesh.worldToLocalMatrix * deformerLocalToWorldMatrix;
                }

                deformer.UpdateVertices(this);
            }

            for (int i = 0; i < deformingMeshes.Length; i++)
            {
                DeformByMatrix(i, outMatrix[i]);
            }

            JobHandle.ScheduleBatchedJobs();

            if (updateNormals || updateBounds)
            {
                for (int i = 0; i < deformingMeshes.Length; i++)
                {
                    JobHandle normalJob = updateNormals ? deformingMeshes[i].UpdateNormals(jobHandles[i]) : default;
                    JobHandle boundsJob = updateBounds ? deformingMeshes[i].UpdateBounds(jobHandles[i], i, bounds) : default;

                    jobHandles[i] = JobHandle.CombineDependencies(normalJob, boundsJob);
                }
            }

            JobHandle.CompleteAll(jobHandles);

            for (int i = 0; i < deformingMeshes.Length; i++)
            {
                deformingMeshes[i].OnDeformDoneThisFrame_SetVertices();
                if (updateNormals)
                {
                    deformingMeshes[i].OnDeformDoneThisFrame_SetNormals();
                }
            }

            if (updateBounds)
            {
                UpdateBoundsFromBoundsArray();
            }
        }

        void UpdateBoundsFromBoundsArray()
        {
            for (int i = 0; i < deformingMeshes.Length; i++)
            {
                meshes[i].bounds = bounds[i];
            }

            for (int i = 0; i < renderers.Count; i++)
            {
                SkinnedMeshRenderer renderer = renderers[i];

                renderer.localBounds = bounds[i];
                renderer.bounds = ResetSkinnedMeshRendererBounds(renderer, bounds[i]);

//#if UNITY_2021_2_OR_NEWER
//                renderers[i].ResetBounds();

//#endif
            }
        }

        public void Enable()
        {
            if (DMM_enabled)
            {
                return;
            }
            DMM_enabled = true;

            int meshIndex = 0;
            for (int i = 0; i < renderers.Count; i++)
            {
                renderers[i].sharedMesh = meshes[meshIndex];
                meshIndex++;
            }
            for (int i = 0; i < meshFilters.Count; i++)
            {
                meshFilters[i].sharedMesh = meshes[meshIndex];
                meshIndex++;
            }
        }

        

        
        public void Disable(bool fixBounds = true)
        {
            DMM_enabled = false;

            int meshIndex = 0;
            for (int i = 0; i < renderers.Count; i++)
            {
                SkinnedMeshRenderer renderer = renderers[i];

                if (renderer != null)
                {
                    renderer.sharedMesh = ORIGINAL_MESHES[meshIndex];
                    if (fixBounds)
                    {
                        renderer.localBounds = ORIGINAL_MESHES[meshIndex].bounds;
                        renderers[i].bounds = ResetSkinnedMeshRendererBounds(renderer, ORIGINAL_MESHES[meshIndex].bounds);
                    }
                }
                meshIndex++;
            }
            for (int i = 0; i < meshFilters.Count; i++)
            {
                MeshFilter meshFilter = meshFilters[i];

                if (meshFilter != null)
                {
                    meshFilter.sharedMesh = ORIGINAL_MESHES[meshIndex];
                }
                meshIndex++;
            }
        }

        public void UpdateRootBones()
        {
            int meshIndex = 0;
            for (int i = 0; i < renderers.Count; i++)
            {
                Transform root = renderers[i].rootBone;
                if (root == null)
                {
                    root = renderers[i].transform;
                }
                deformingMeshes[meshIndex].UpdateRoot(root);

                meshIndex++;
            }
            for (int i = 0; i < meshRenderers.Count; i++)
            {
                deformingMeshes[meshIndex].UpdateRoot(meshRenderers[i].transform);
                meshIndex++;
            }
        }


        void CreateArrays()
        {
            if (!jobHandles.IsCreated) jobHandles = new NativeArray<JobHandle>(meshCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); //clear cause this one seems a bit sus to leave undefined
            if (!bounds.IsCreated) bounds = new NativeArray<Bounds>(meshCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            if (!minMax.IsCreated) minMax = new NativeArray<float3>(meshCount * 2, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            if (!outMatrix.IsCreated) outMatrix = new NativeArray<Matrix4x4>(meshCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }
        public void DisposeArrays()
        {
            if (jobHandles.IsCreated) jobHandles.Dispose();
            if (bounds.IsCreated) bounds.Dispose();
            if (minMax.IsCreated) minMax.Dispose();
            if (outMatrix.IsCreated) outMatrix.Dispose();
        }



    
        public void DestroyMeshes()
        {
            if (meshCount == 0) return;

            Disable();

            foreach (var deformingMesh in deformingMeshes)
            {
                ScriptableObject.DestroyImmediate(deformingMesh);
            }
        }

        
        public void DeformByMatrix(int meshIndex, Matrix4x4 transformation)
        {
            bool hasRotation = !IsMatrixAlmostZeroRotation(ref transformation);
            bool hasPosition = !IsMatrixAlmostZeroPosition(ref transformation);

            if (!hasRotation && !hasPosition)
            {
                //There is no transformation, skip this job
                return;
            }

            DeformingMesh deformingMesh = deformingMeshes[meshIndex];
            JobHandle previousJob = jobHandles[meshIndex];

            if (!hasRotation)
            {
                //Use a simpler job that only adds the offset
                jobHandles[meshIndex] = new DeformByPositionOffsetJob
                {
                    vertices = deformingMesh.vertices,
                    offset = new Vector3(transformation.m03, transformation.m13, transformation.m23),
                }
                .Schedule(deformingMesh.vertices.Length, 2048, previousJob);
                return;
            }
    
            //Apply full matrix
            jobHandles[meshIndex] = new DeformByMatrixJob
            {
                vertices = deformingMesh.vertices,
                matrix = transformation,
                //matrix = deformingMesh.worldToLocalMatrix * transformation * deformingMesh.localToWorldMatrix,
            }
            .Schedule(deformingMesh.vertices.Length, 2048, previousJob);
        }
        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct DeformByMatrixJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction]
            public NativeArray<Vector3> vertices;
            public Matrix4x4 matrix;

            public void Execute(int i)
            {
                vertices[i] = math.transform(matrix, vertices[i]);
            }
        }
        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct DeformByPositionOffsetJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction]
            public NativeArray<Vector3> vertices;
            public Vector3 offset;

            public void Execute(int i)
            {
                vertices[i] += offset;
            }
        }

        #region MinMax
        public void GetMinMax()
        {
            for (int i = 0; i < deformingMeshes.Length; i++)
            {
                var dm = deformingMeshes[i];
                JobHandle jobHandle = jobHandles[i];

                jobHandles[i] = new MeshJob_MinMaxY_1
                {
                    vertices = dm.vertices,
                    minMax = minMax,
                    jobIndex = i,
                }
                .Schedule(jobHandle);
            }

            JobHandle combinedDependency = JobHandle.CombineDependencies(jobHandles);

            JobHandle part2 = new MeshJob_MinMaxY_2
            {
                minMax = minMax,
            }
            .Schedule(combinedDependency);

            for (int i = 0; i < jobHandles.Length; i++)
            {
                jobHandles[i] = part2;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_MinMaxY_1 : IJob
        {
            [ReadOnly]
            public NativeArray<Vector3> vertices;

            [WriteOnly]
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<float3> minMax;

            public int jobIndex;

            public void Execute()
            {
                float3 min = float.MaxValue;
                float3 max = float.MinValue;

                for (int i = 0; i < vertices.Length; i++)
                {
                    float3 vert = vertices[i];
                    min = math.min(min, vert);
                    max = math.max(max, vert);
                }
             
                minMax[jobIndex * 2 + 0] = min;
                minMax[jobIndex * 2 + 1] = max;
            }
        }
        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_MinMaxY_2 : IJob
        {
            public NativeArray<float3> minMax;

            public void Execute()
            {
                float3 min = float.MaxValue;
                float3 max = float.MinValue;

                for (int i = 0; i < minMax.Length; i++)
                {
                    float3 value = minMax[i];
                    min = math.min(min, value);
                    max = math.max(max, value);
                }

                minMax[0] = min;
                minMax[1] = max;
            }
        }
        #endregion












        public bool HasActiveDeformer()
        {
            for (int i = 0; i < deformers.Length; i++)
            {
                if (deformers[i] != null && deformers[i].isActiveAndEnabled)
                {
                    return true;
                }
            }
            return false;
        }
        public void AddDeformer(Deformer deformer)
        {
            if (deformer == null)
            {
                return;
            }
            Deformer[] newDeformers = new Deformer[deformers.Length + 1];
            for (int i = 0; i < deformers.Length; i++)
            {
                newDeformers[i] = deformers[i];
            }
            newDeformers[deformers.Length] = deformer;
            deformers = newDeformers;
        }
        public void AddDeformers(IEnumerable<Deformer> manyDeformers)
        {
            if (manyDeformers == null)
            {
                return;
            }
            int count = manyDeformers.Count();
            if (count == 0)
            {
                return;
            }

            Deformer[] newDeformers = new Deformer[deformers.Length + count];
            for (int i = 0; i < deformers.Length; i++)
            {
                newDeformers[i] = deformers[i];
            }

            int currentIndex = deformers.Length;

            foreach (var deformer in manyDeformers)
            {
                newDeformers[currentIndex++] = deformer;
            }
            deformers = newDeformers;
        }
        public void RemoveDeformer(Deformer deformer)
        {
            List<Deformer> currentDeformers = deformers.ToList();
            currentDeformers.Remove(deformer);
            deformers = currentDeformers.ToArray();
        }
        public void RemoveDeformers(IEnumerable<Deformer> manyDeformers)
        {
            List<Deformer> currentDeformers = deformers.ToList<Deformer>();
            foreach (var deformer in manyDeformers)
            {
                currentDeformers.Remove(deformer);
            }
            deformers = currentDeformers.ToArray();
        }
        public void RemoveAllDeformers()
        {
            deformers = new Deformer[0];
        }















        static Bounds ResetSkinnedMeshRendererBounds(SkinnedMeshRenderer renderer, Bounds localBounds)
        {
            Transform root = renderer.rootBone;
            Matrix4x4 rootMatrix = (root == null) ? renderer.transform.localToWorldMatrix : root.localToWorldMatrix;

            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;

            Vector3 worldMin = Vector3.positiveInfinity;
            Vector3 worldMax = Vector3.negativeInfinity;

            //Vector3[] points = new Vector3[8];
            //points[0] = new Vector3(min.x, min.y, min.z);
            //points[1] = new Vector3(max.x, min.y, min.z);
            //points[2] = new Vector3(min.x, max.y, min.z);
            //points[3] = new Vector3(max.x, max.y, min.z);
            //points[4] = new Vector3(min.x, min.y, max.z);
            //points[5] = new Vector3(max.x, min.y, max.z);
            //points[6] = new Vector3(min.x, max.y, max.z);
            //points[7] = new Vector3(max.x, max.y, max.z);

            //for (int i = 0; i < 8; i++) {
            //    Vector3 worldPoint = rootMatrix.MultiplyPoint3x4(points[i]);

            //    worldMin = Vector3.Min(worldMin, worldPoint);
            //    worldMax = Vector3.Max(worldMax, worldPoint);
            //}

            void checkPoint(float pX, float pY, float pZ, ref Matrix4x4 rootMatrix, ref Vector3 worldMin, ref Vector3 worldMax)
            {
                float x = rootMatrix.m00 * pX + rootMatrix.m01 * pY + rootMatrix.m02 * pZ;
                float y = rootMatrix.m10 * pX + rootMatrix.m11 * pY + rootMatrix.m12 * pZ;
                float z = rootMatrix.m20 * pX + rootMatrix.m21 * pY + rootMatrix.m22 * pZ;
                worldMin.x = worldMin.x > x ? x : worldMin.x;
                worldMax.x = worldMax.x < x ? x : worldMax.x;
                worldMin.y = worldMin.y > y ? y : worldMin.y;
                worldMax.y = worldMax.y < y ? y : worldMax.y;
                worldMin.z = worldMin.z > z ? z : worldMin.z;
                worldMax.z = worldMax.z < z ? z : worldMax.z;
            }

            checkPoint(min.x, min.y, min.z, ref rootMatrix, ref worldMin, ref worldMax);
            checkPoint(max.x, min.y, min.z, ref rootMatrix, ref worldMin, ref worldMax);
            checkPoint(min.x, max.y, min.z, ref rootMatrix, ref worldMin, ref worldMax);
            checkPoint(max.x, max.y, min.z, ref rootMatrix, ref worldMin, ref worldMax);
            checkPoint(min.x, min.y, max.z, ref rootMatrix, ref worldMin, ref worldMax);
            checkPoint(max.x, min.y, max.z, ref rootMatrix, ref worldMin, ref worldMax);
            checkPoint(min.x, max.y, max.z, ref rootMatrix, ref worldMin, ref worldMax);
            checkPoint(max.x, max.y, max.z, ref rootMatrix, ref worldMin, ref worldMax);

            Vector3 worldCenter = new Vector3(
              (worldMin.x + worldMax.x) / 2f + rootMatrix.m03,
              (worldMin.y + worldMax.y) / 2f + rootMatrix.m13,
              (worldMin.z + worldMax.z) / 2f + rootMatrix.m23
            );
            Vector3 worldSize = worldMax - worldMin;

            return new Bounds(worldCenter, worldSize);
        }
        static bool IsMatrixAlmostZeroRotation(ref Matrix4x4 matrix)
        {
            const float EPSILON = 0.0001f;

            return
                math.abs(matrix.m01) < EPSILON &&
                math.abs(matrix.m02) < EPSILON &&

                math.abs(matrix.m10) < EPSILON &&
                math.abs(matrix.m12) < EPSILON &&

                math.abs(matrix.m20) < EPSILON &&
                math.abs(matrix.m21) < EPSILON &&

                math.abs(matrix.m00 - 1) < EPSILON &&
                math.abs(matrix.m11 - 1) < EPSILON &&
                math.abs(matrix.m22 - 1) < EPSILON
            ;
        }
        static bool IsMatrixAlmostZeroPosition(ref Matrix4x4 matrix)
        {
            const float EPSILON = 0.0001f;

            return
                math.abs(matrix.m03) < EPSILON &&
                math.abs(matrix.m13) < EPSILON &&
                math.abs(matrix.m23) < EPSILON
            ;
        }
        static bool IsMatrixAlmostIdentity(ref Matrix4x4 matrix)
        {
            const float EPSILON = 0.0001f;

            return
                math.abs(matrix.m01) < EPSILON &&
                math.abs(matrix.m02) < EPSILON &&
                math.abs(matrix.m03) < EPSILON &&

                math.abs(matrix.m10) < EPSILON &&
                math.abs(matrix.m12) < EPSILON &&
                math.abs(matrix.m13) < EPSILON &&

                math.abs(matrix.m20) < EPSILON &&
                math.abs(matrix.m21) < EPSILON &&
                math.abs(matrix.m23) < EPSILON &&

                math.abs(matrix.m00 - 1) < EPSILON &&
                math.abs(matrix.m11 - 1) < EPSILON &&
                math.abs(matrix.m22 - 1) < EPSILON
            ;
        }

    }
}