using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace TF_Toolkit
{
    public class BakemeshTester : MonoBehaviour
    {
        public bool VISUALIZE = true;

        Mesh[] bakedMeshes;
        SkinnedMeshRenderer[] skinnedMeshRenderers;
        List<int[]> triangles = new List<int[]>();
        List<NativeArray<Vector3>> vertices = new List<NativeArray<Vector3>>();
        List<NativeArray<BoneWeight>> boneWeights = new List<NativeArray<BoneWeight>>();
        List<NativeArray<Vector3>> outVertices = new List<NativeArray<Vector3>>();
        List<NativeArray<Matrix4x4>> allMatrices = new List<NativeArray<Matrix4x4>>();
        List<Matrix4x4[]> allBindposes = new List<Matrix4x4[]>();
        List<Transform[]> allBones = new List<Transform[]>();

       

        void OnDrawGizmos()
        {
            init();
            CodeTimer timer = new CodeTimer();
            testGetVertices();
            //drawSkeletonGizmo(skinnedMeshRenderers);
            timer.read();
        }


        private void init()
        {
            if (skinnedMeshRenderers == null)
            {
                skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            }
            triangles.Clear();
            vertices.Clear();
            boneWeights.Clear();
            outVertices.Clear();
            allMatrices.Clear();
            allBindposes.Clear();
            allBones.Clear();

            if (bakedMeshes != null)
            {
                foreach (var bakedMesh in bakedMeshes)
                {
                    if (bakedMesh != null)
                    {
                        DestroyImmediate(bakedMesh);
                    }
                }
            }

            bakedMeshes = new Mesh[skinnedMeshRenderers.Length];

            for (int i = 0; i < skinnedMeshRenderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = skinnedMeshRenderers[i];
                Mesh mesh = renderer.sharedMesh;

                bakedMeshes[i] = new Mesh();
                bakedMeshes[i].MarkDynamic();

                triangles.Add(mesh.triangles);
                {
                    var arr = mesh.vertices;
                    var nativeArr = new NativeArray<Vector3>(arr.Length, Allocator.Persistent);
                    nativeArr.CopyFrom(arr);
                    vertices.Add(nativeArr);

                    outVertices.Add(new NativeArray<Vector3>(arr.Length, Allocator.Persistent));
                }
                {
                    var arr = mesh.boneWeights;
                    var nativeArr = new NativeArray<BoneWeight>(arr.Length, Allocator.Persistent);
                    nativeArr.CopyFrom(arr);
                    boneWeights.Add(nativeArr);
                }

                var thisMeshBindPoses = mesh.bindposes;

                allMatrices.Add(new NativeArray<Matrix4x4>(thisMeshBindPoses.Length, Allocator.Persistent));
            
                allBindposes.Add(thisMeshBindPoses);
                allBones.Add(renderer.bones);
            }      
        }



        Vector3 skinTransform(Vector3 p, BoneWeight weight, NativeArray<Matrix4x4> matrices)
        {
            return
                  matrices[weight.boneIndex0].MultiplyPoint(p) * weight.weight0
                + matrices[weight.boneIndex1].MultiplyPoint(p) * weight.weight1
                + matrices[weight.boneIndex2].MultiplyPoint(p) * weight.weight2
                + matrices[weight.boneIndex3].MultiplyPoint(p) * weight.weight3
            ;
        }

        [BurstCompile]
        public struct MeshJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Vector3> vertices;
            [ReadOnly] public NativeArray<BoneWeight> boneWeights;
            [ReadOnly] public NativeArray<Matrix4x4> matrices;

            [WriteOnly] public NativeArray<Vector3> outVertices;

            public void Execute(int i)
            {
                Vector3 p = vertices[i];
                BoneWeight weight = boneWeights[i];

                //outVertices[i] = matrices[weight.boneIndex0].MultiplyPoint(p) * weight.weight0
                //+ matrices[weight.boneIndex1].MultiplyPoint(p) * weight.weight1
                //+ matrices[weight.boneIndex2].MultiplyPoint(p) * weight.weight2
                //+ matrices[weight.boneIndex3].MultiplyPoint(p) * weight.weight3;

                Vector3 pos = Vector3.zero;
                if (weight.weight0 != 0) pos += matrices[weight.boneIndex0].MultiplyPoint3x4(p) * weight.weight0;
                if (weight.weight1 != 0) pos += matrices[weight.boneIndex1].MultiplyPoint3x4(p) * weight.weight1;
                if (weight.weight2 != 0) pos += matrices[weight.boneIndex2].MultiplyPoint3x4(p) * weight.weight2;
                if (weight.weight3 != 0) pos += matrices[weight.boneIndex3].MultiplyPoint3x4(p) * weight.weight3;
                outVertices[i] = pos;
                //outVertices[i] = matrices[weight.boneIndex0].MultiplyPoint3x4(p) * weight.weight0
                //+ matrices[weight.boneIndex1].MultiplyPoint3x4(p) * weight.weight1
                //+ matrices[weight.boneIndex2].MultiplyPoint3x4(p) * weight.weight2
                //+ matrices[weight.boneIndex3].MultiplyPoint3x4(p) * weight.weight3;
            }
        }

        void testGetVertices()
        {
            CodeTimer timer1 = new CodeTimer();
            for (int meshIndex = 0; meshIndex < skinnedMeshRenderers.Length; meshIndex++)
            {
                var verts = vertices[meshIndex];
                var outVerts = outVertices[meshIndex];

                var weights = boneWeights[meshIndex];
                var matrices = allMatrices[meshIndex];

                var bindposes = allBindposes[meshIndex];

                var bones = allBones[meshIndex];

                for (int i = 0; i < bones.Length; i++)
                {
                    matrices[i] = bones[i].localToWorldMatrix * bindposes[i];
                }

                var job = new MeshJob
                {
                    vertices = verts,
                    boneWeights = weights,
                    matrices = matrices,
                    outVertices = outVerts,
                };
                job.Schedule(verts.Length, 2048).Complete();

                //for (int i = 0; i < outVerts.Length; i++)
                //{
                //    outVerts[i] = skinTransform(verts[i], weights[i], matrices);
                //}
            }
            timer1.read("NewMethod");

            CodeTimer timer2 = new CodeTimer();
            for (int meshIndex = 0; meshIndex < skinnedMeshRenderers.Length; meshIndex++)
            {
                skinnedMeshRenderers[meshIndex].BakeMesh(bakedMeshes[meshIndex]);
            }
            var readOnlyDataArray = Mesh.AcquireReadOnlyMeshData(bakedMeshes);
            for (int meshIndex = 0; meshIndex < skinnedMeshRenderers.Length; meshIndex++)
            {
                NativeArray<Vector3> verts = new NativeArray<Vector3>(vertices[meshIndex].Length, Allocator.Temp);
                readOnlyDataArray[meshIndex].GetVertices(verts);
                verts.CopyTo(outVertices[meshIndex]);
                //deformingMesh.SetNewVertices(verts);
                verts.Dispose();
            }
            readOnlyDataArray.Dispose();
            timer2.read("BakeMesh");
        }

        void drawSkeletonGizmo(SkinnedMeshRenderer[] skelRend)
        {
            if (skinnedMeshRenderers == null) return;

            for (int meshIndex = 0; meshIndex < skinnedMeshRenderers.Length; meshIndex++)
            {
                var verts = vertices[meshIndex];
                var outVerts = outVertices[meshIndex];

                var weights = boneWeights[meshIndex];
                var matrices = allMatrices[meshIndex];

                var bindposes = allBindposes[meshIndex];

                var bones = allBones[meshIndex];

                for (int i = 0; i < bones.Length; i++)
                {
                    matrices[i] = bones[i].localToWorldMatrix * bindposes[i];
                }

                for (int i = 0; i < outVerts.Length; i++)
                {
                    outVerts[i] = skinTransform(verts[i], weights[i], matrices);
                }

                if (!VISUALIZE)
                {
                    return;
                }

                var trigs = triangles[meshIndex];

                for (int i = 0; i < trigs.Length; i += 3)
                {
                    var aIdx = trigs[i + 0];
                    var bIdx = trigs[i + 1];
                    var cIdx = trigs[i + 2];

                    var a1 = outVerts[aIdx];
                    var b1 = outVerts[bIdx];
                    var c1 = outVerts[cIdx];

                    Gizmos.DrawLine(a1, b1);
                    Gizmos.DrawLine(b1, c1);
                    Gizmos.DrawLine(a1, c1);
                }
            }
        }
    }
}
