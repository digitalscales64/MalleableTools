using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace TF_Toolkit
{
    public class DeformingMesh : ScriptableObject
    {
        public Mesh mesh;
        public Mesh ORIGINAL_MESH;
        public int vertexCount;
        public Transform root;
        public Matrix4x4 worldToLocalMatrix;
        public Matrix4x4 localToWorldMatrix;
        public NativeArray<Vector3> originalMeshVertices;
        public NativeArray<Vector3> blendShapedVertices;
        public NativeArray<Vector3> vertices;
        public NativeArray<float3> normals;
        public NativeArray<int> indices;
        public NativeArray<float> helpers;

        public List<Vector3[]> blendShapes = new List<Vector3[]>();
        public float[] blendShapeWeights = new float[0];

        public NativeArray<BoneWeight> boneWeights;
        public NativeArray<Matrix4x4> bindposes;
        public NativeArray<Matrix4x4> bonelocalToWorldMatrix;
        public NativeArray<float4x3> boneMatrices;

        public Transform[] bones;

        public SkinnedMeshRenderer skinnedMeshRenderer;
        public MeshFilter meshFilter;

        public bool hasBakedOnce = false;

        JobHandle zeroNormalsJobHandle;

        public void UpdateBlendShapes()
        {
            if (skinnedMeshRenderer == null)
            {
                return;
            }

            int blendShapeCount = ORIGINAL_MESH.blendShapeCount;

            if (mesh.blendShapeCount != blendShapeCount)
            {
                blendShapedVertices.CopyFrom(originalMeshVertices);

                if (mesh.blendShapeCount != blendShapeCount)
                {
                    mesh.ClearBlendShapes();
                }

                if (blendShapeCount > 0)
                {
                    Vector3[] empty = new Vector3[vertexCount];

                    for (int shapeIndex = 0; shapeIndex < blendShapeCount; shapeIndex++)
                    {
                        string shapeName = ORIGINAL_MESH.GetBlendShapeName(shapeIndex);
                        //mesh.AddBlendShapeFrame(shapeName, 0, empty, empty, empty);
                        mesh.AddBlendShapeFrame(shapeName, 100, empty, empty, empty);
                    }

                    blendShapes.Clear();



                    for (int shapeIndex = 0; shapeIndex < blendShapeCount; shapeIndex++)
                    {
                        int frameCount = ORIGINAL_MESH.GetBlendShapeFrameCount(shapeIndex);

                        if (frameCount != 1)
                        {
                            Debug.Log("Wierd blendshape detected! My assumption failed");
                        }

                        float weightInverse = 1f / ORIGINAL_MESH.GetBlendShapeFrameWeight(shapeIndex, 0);

                        var deltaVertices = new Vector3[vertexCount];

                        ORIGINAL_MESH.GetBlendShapeFrameVertices(shapeIndex, 0, deltaVertices, null, null);
                        for (int i = 0; i < deltaVertices.Length; i++)
                        {
                            deltaVertices[i] *= weightInverse;
                        }

                        blendShapes.Add(deltaVertices);
                    }

                    blendShapeWeights = new float[blendShapeCount];
                }
            }
        }

        public void UpdateBlendShapeVertices()
        {
            if (skinnedMeshRenderer == null)
            {
                return;
            }

            bool needsUpdate = false;
            for (int i = 0; i < blendShapeWeights.Length; i++)
            {
                float currentWeight = skinnedMeshRenderer.GetBlendShapeWeight(i);
                if (blendShapeWeights[i] != currentWeight)
                {
                    needsUpdate = true;
                    blendShapeWeights[i] = currentWeight;
                }
            }
            if (!needsUpdate)
            {
                return;
            }

            blendShapedVertices.CopyFrom(originalMeshVertices);

            for (int blendShapeIndex = 0; blendShapeIndex < blendShapes.Count; blendShapeIndex++)
            {
                float weight = blendShapeWeights[blendShapeIndex];

                if (weight == 0)
                {
                    continue;
                }

                Vector3[] blendShape = blendShapes[blendShapeIndex];

                for (int i = 0; i < vertexCount; i++)
                {
                    blendShapedVertices[i] += blendShape[i] * weight;
                }
            }
        }

        public Matrix4x4 GetOutMatrix()
        {
            if (skinnedMeshRenderer != null)
            {
                Transform root = skinnedMeshRenderer.rootBone;
                if (root != null)
                {
                    return root.worldToLocalMatrix;
                }
            }
            return Matrix4x4.identity;
        }

        public JobHandle BakeMesh()
        {
            if (skinnedMeshRenderer == null)
            {
                vertices.CopyFrom(originalMeshVertices);
                return default;
            }

            if (!hasBakedOnce)
            {
                skinnedMeshRenderer.BakeMesh(mesh, true);
                hasBakedOnce = true;

                UpdateBlendShapes();
            }

            UpdateBlendShapeVertices();

            for (int i = 0; i < boneMatrices.Length; i++)
            {
                bonelocalToWorldMatrix[i] = bones[i].localToWorldMatrix;
            }

            JobHandle createBoneMatricesJob = new CreateBoneMatricesJob
            {
                bonelocalToWorldMatrix = bonelocalToWorldMatrix,
                bindposes = bindposes,
                boneMatrices = boneMatrices,
            }
            .Schedule(boneMatrices.Length, 2048);

            return new BakeMeshJob
            {
                originalMeshVertices = blendShapedVertices,//originalMeshVertices,
                vertices = vertices,
                boneWeights = boneWeights,
                boneMatrices = boneMatrices,
            }
            .Schedule(vertexCount, 2048, createBoneMatricesJob);
        }
        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct CreateBoneMatricesJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<Matrix4x4> bonelocalToWorldMatrix;
            [ReadOnly]
            public NativeArray<Matrix4x4> bindposes;
            [WriteOnly]
            public NativeArray<float4x3> boneMatrices;
            public void Execute(int i)
            {
                float4x4 mat = math.transpose(math.mul(bonelocalToWorldMatrix[i], bindposes[i]));

                float4x3 newMat = new float4x3();
                newMat.c0 = mat.c0;
                newMat.c1 = mat.c1;
                newMat.c2 = mat.c2;

                boneMatrices[i] = newMat;
            }
        }
        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct BakeMeshJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<Vector3> originalMeshVertices;
            [WriteOnly]
            [NativeDisableParallelForRestriction]
            public NativeArray<Vector3> vertices;
            [ReadOnly]
            public NativeArray<BoneWeight> boneWeights;
            [ReadOnly]
            public NativeArray<float4x3> boneMatrices; //These are transposed cause it makes the math faster and allows us to use float4x3 instead of float4x4
            public void Execute(int i)
            {
                BoneWeight weight = boneWeights[i];

                float4x3 bm0 = boneMatrices[weight.boneIndex0];
                float4x3 bm1 = boneMatrices[weight.boneIndex1];
                float4x3 bm2 = boneMatrices[weight.boneIndex2];
                float4x3 bm3 = boneMatrices[weight.boneIndex3];

                float4x3 mat = bm0 * weight.weight0 + bm1 * weight.weight1 + bm2 * weight.weight2 + bm3 * weight.weight3;

                float3 originalVert = originalMeshVertices[i];

                //Manual math.transform because we are using float4x3
                vertices[i] = new float3(
                    math.dot(originalVert, mat.c0.xyz) + mat.c0.w,
                    math.dot(originalVert, mat.c1.xyz) + mat.c1.w,
                    math.dot(originalVert, mat.c2.xyz) + mat.c2.w
                );
            }
        }

        //public DeformingMesh(DeformingMeshManager manager, SkinnedMeshRenderer skinnedMeshRenderer)
        public void Init(SkinnedMeshRenderer skinnedMeshRenderer, Mesh.MeshData meshData)
        {
            ORIGINAL_MESH = skinnedMeshRenderer.sharedMesh;
            vertexCount = ORIGINAL_MESH.vertexCount;

            mesh = new Mesh();
            mesh.MarkDynamic();
            mesh.name = ORIGINAL_MESH.name.Replace(".baked", "") + "(Deformable)";

            using (NativeArray<Vector3> verts = new NativeArray<Vector3>(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
                meshData.GetVertices(verts);
                originalMeshVertices = new NativeArray<Vector3>(vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                originalMeshVertices.CopyFrom(verts);
            }

            blendShapedVertices = new NativeArray<Vector3>(vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            blendShapedVertices.CopyFrom(originalMeshVertices);
            vertices = new NativeArray<Vector3>(vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            normals = new NativeArray<float3>(vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            var tris = ORIGINAL_MESH.triangles;
            indices = new NativeArray<int>(tris.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            indices.CopyFrom(tris);
            helpers = new NativeArray<float>(vertexCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); //clear because this needs to be 0s

            for (int i = 0; i < indices.Length; i++)
            {
                helpers[indices[i]] += 1;
            }
            for (int i = 0; i < helpers.Length; i++)
            {
                helpers[i] = 1f / helpers[i];
            }



            var originalBoneWeights = ORIGINAL_MESH.boneWeights;

            boneWeights = new NativeArray<BoneWeight>(vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); //Clear because it is possible for this array to not get filled if a mesh without weights is using a SkinnedMeshRenderer
            boneWeights.CopyFrom(originalBoneWeights);

            bones = skinnedMeshRenderer.bones;
            var originalBindposes = ORIGINAL_MESH.bindposes;

            int bindPoseCount = Mathf.Min(originalBindposes.Length, bones.Length);
            bindposes = new NativeArray<Matrix4x4>(bindPoseCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            NativeArray<Matrix4x4>.Copy(originalBindposes, 0, bindposes, 0, bindPoseCount);

            bonelocalToWorldMatrix = new NativeArray<Matrix4x4>(bindPoseCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            boneMatrices = new NativeArray<float4x3>(bindPoseCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            this.skinnedMeshRenderer = skinnedMeshRenderer;
        }
        //public DeformingMesh(DeformingMeshManager manager, MeshFilter meshFilter)
        public void Init(MeshFilter meshFilter, Mesh.MeshData meshData)
        {
            this.meshFilter = meshFilter;
            ORIGINAL_MESH = meshFilter.sharedMesh;

            mesh = MonoBehaviour.Instantiate(meshFilter.sharedMesh);
            mesh.MarkDynamic();
            mesh.name = ORIGINAL_MESH.name + "(Deformable)";

            vertexCount = ORIGINAL_MESH.vertexCount;

            using (NativeArray<Vector3> verts = new NativeArray<Vector3>(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
                meshData.GetVertices(verts);
                originalMeshVertices = new NativeArray<Vector3>(vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                originalMeshVertices.CopyFrom(verts);
            }

            vertices = new NativeArray<Vector3>(vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            normals = new NativeArray<float3>(vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            var tris = ORIGINAL_MESH.triangles;
            indices = new NativeArray<int>(tris.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            indices.CopyFrom(tris);
            helpers = new NativeArray<float>(vertexCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); //clear because this needs to be 0s

            for (int i = 0; i < indices.Length; i++)
            {
                helpers[indices[i]] += 1;
            }
            for (int i = 0; i < helpers.Length; i++)
            {
                helpers[i] = 1f / helpers[i];
            }
        }

        public void UpdateRoot(Transform root)
        {
            this.root = root;
            worldToLocalMatrix = root.worldToLocalMatrix;
            localToWorldMatrix = root.localToWorldMatrix;
        }

        public void OnDeformDoneThisFrame_SetVertices()
        {
            const MeshUpdateFlags FLAGS = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontRecalculateBounds;
            //CodeTimer timer = new CodeTimer();
            mesh.SetVertices(vertices, 0, vertices.Length, FLAGS);
            //mesh.SetVertexBufferData<MyVertexStruct>(myVertexStructs, 0, 0, vertexCount); //can probably boost performance with something like this later
            //timer.addTime("SET vertices");
        }
        public void OnDeformDoneThisFrame_SetNormals()
        {
            const MeshUpdateFlags FLAGS = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontRecalculateBounds;
            mesh.SetNormals(normals, 0, normals.Length, FLAGS);
        }

        private void OnDestroy()
        {
            if (vertices.IsCreated) vertices.Dispose();
            if (normals.IsCreated) normals.Dispose();
            if (indices.IsCreated) indices.Dispose();
            if (helpers.IsCreated) helpers.Dispose();
            if (boneWeights.IsCreated) boneWeights.Dispose();
            if (bindposes.IsCreated) bindposes.Dispose();
            if (originalMeshVertices.IsCreated) originalMeshVertices.Dispose();
            if (boneMatrices.IsCreated) boneMatrices.Dispose();
            if (bonelocalToWorldMatrix.IsCreated) bonelocalToWorldMatrix.Dispose();
            if (blendShapedVertices.IsCreated) blendShapedVertices.Dispose();

            if (mesh != null)
            {
                MonoBehaviour.DestroyImmediate(mesh);
            }
        }

        public void StartZeroNormalsJob()
        {
            zeroNormalsJobHandle = new NormalJob_1
            {
                normals = normals,
            }
            .Schedule();
        }

        public JobHandle UpdateNormals(JobHandle jobHandle = default)
        {
            jobHandle = JobHandle.CombineDependencies(zeroNormalsJobHandle, jobHandle);

            return new NormalJob_2
            {
                vertices = vertices,
                normals = normals,
                indices = indices,
                helpers = helpers,
            }
            .Schedule(indices.Length / 3, 2048, jobHandle);//part1);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, DisableSafetyChecks = true)]
        struct NormalJob_1 : IJob
        {
            [WriteOnly]
            public NativeArray<float3> normals;

            public void Execute()
            {
                for (int i = 0; i < normals.Length; i++)
                {
                    normals[i] = 0;
                }
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct NormalJob_2 : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<Vector3> vertices;
            [NativeDisableParallelForRestriction]
            public NativeArray<float3> normals;
            [ReadOnly]
            public NativeArray<float> helpers;
            [ReadOnly]
            public NativeArray<int> indices;

            public void Execute(int i)
            {
                int i0 = indices[i * 3 + 0];
                int i1 = indices[i * 3 + 1];
                int i2 = indices[i * 3 + 2];

                float3 v0 = vertices[i0];
                float3 v1 = vertices[i1];
                float3 v2 = vertices[i2];

                // Calculate the normal of the triangle
                float3 edge1 = v1 - v0;
                float3 edge2 = v2 - v0;
                float3 normal = math.cross(edge1, edge2);
                normal = math.normalize(normal);
                // Add the normal to each vertex normal of the triangle

                normals[i0] += normal * helpers[i0];
                normals[i1] += normal * helpers[i1];
                normals[i2] += normal * helpers[i2];
            }
        }


        public JobHandle UpdateBounds(JobHandle jobHandle, int meshIndex, NativeArray<Bounds> bounds)
        {
            return new BoundsJob
            {
                vertices = vertices,
                bounds = bounds,
                meshIndex = meshIndex,
            }
            .Schedule(jobHandle);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct BoundsJob : IJob
        {
            [ReadOnly]
            public NativeArray<Vector3> vertices;
            [WriteOnly]
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<Bounds> bounds;

            public int meshIndex;

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

                float3 center = (min + max) * 0.5f;
                float3 extents = max - min;

                bounds[meshIndex] = new Bounds(center, extents);
            }
        }
    }
}