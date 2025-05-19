using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace TF_Toolkit
{
    public static class Tools
    {
        public static float Lerp(float a, float b, float value)
        {
            return a * (1 - value) + b * value;
        }

        public static float InverseLerp(float a, float b, float value)
        {
            if (a == b)
            {
                return 0;
            }
            return Mathf.Clamp01((value - a) / (b - a));
        }

        public static Vector3 MultiplyPoint3x4(ref Matrix4x4 matrix, ref Vector3 v)
        {
            return new Vector3(
                matrix.m00 * v.x + matrix.m01 * v.y + matrix.m02 * v.z + matrix.m03,
                matrix.m10 * v.x + matrix.m11 * v.y + matrix.m12 * v.z + matrix.m13,
                matrix.m20 * v.x + matrix.m21 * v.y + matrix.m22 * v.z + matrix.m23
            );
        }

        public static Transform GetFirstCommonParent(Transform t1, Transform t2)
        {
            if (t1.IsChildOf(t2))
            {
                return t2;
            }
            while (t1 != null)
            {
                if (t2.IsChildOf(t1))
                {
                    return t1;
                }
                t1 = t1.parent;
            }
            return null;
        }


        public static void BakeMesh(SkinnedMeshRenderer renderer, NativeArray<Vector3> inVertices)
        {

        }

        [BurstCompile]
        public struct LinearBlendSkinningPositionJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Vector3> inVertices;
            [ReadOnly] public NativeArray<BoneWeight> inBoneWeights;
            [ReadOnly] public NativeArray<Matrix4x4> inSkinMatrices;

            [WriteOnly] public NativeArray<Vector3> outVertices;

            public void Execute(int i)
            {
                Vector3 inPos = inVertices[i];
                BoneWeight bw = inBoneWeights[i];

                Vector3 skinnedPos = inSkinMatrices[bw.boneIndex0].MultiplyPoint(inPos) * bw.weight0 +
                                        inSkinMatrices[bw.boneIndex1].MultiplyPoint(inPos) * bw.weight1 +
                                        inSkinMatrices[bw.boneIndex2].MultiplyPoint(inPos) * bw.weight2 +
                                        inSkinMatrices[bw.boneIndex3].MultiplyPoint(inPos) * bw.weight3;

                outVertices[i] = skinnedPos;
            }
        }






        static Vector3 skinTransform(Vector3 p, BoneWeight weight, Matrix4x4[] matrices)
        {
            var result = Vector3.zero;

            result += matrices[weight.boneIndex0].MultiplyPoint(p) * weight.weight0;
            result += matrices[weight.boneIndex1].MultiplyPoint(p) * weight.weight1;
            result += matrices[weight.boneIndex2].MultiplyPoint(p) * weight.weight2;
            result += matrices[weight.boneIndex3].MultiplyPoint(p) * weight.weight3;

            return result;
        }

        public static void drawSkeletonGizmo(SkinnedMeshRenderer[] skelRend)
        {
            if (skelRend == null) return;
            
            foreach (var rend in skelRend)
            {
                if (rend == null) continue;
                
                var mesh = rend.sharedMesh;

                var trigs = mesh.triangles;
                var verts = mesh.vertices;
                var weights = mesh.boneWeights;
                var numBones = mesh.bindposes.Length;
                var matrices = new Matrix4x4[mesh.bindposes.Length];
                for (int i = 0; i < numBones; i++)
                {
                    matrices[i] = rend.bones[i].localToWorldMatrix * mesh.bindposes[i];
                }
                for (int i = 0; i < trigs.Length; i += 3)
                {
                    var aIdx = trigs[i + 0];
                    var bIdx = trigs[i + 1];
                    var cIdx = trigs[i + 2];

                    var a = verts[aIdx];
                    var b = verts[bIdx];
                    var c = verts[cIdx];

                    var aWeights = weights[aIdx];
                    var bWeights = weights[bIdx];
                    var cWeights = weights[cIdx];

                    var a1 = skinTransform(a, aWeights, matrices);
                    var b1 = skinTransform(b, bWeights, matrices);
                    var c1 = skinTransform(c, cWeights, matrices);

                    Gizmos.DrawLine(a1, b1);
                    Gizmos.DrawLine(b1, c1);
                    Gizmos.DrawLine(a1, c1);
                }
            }
        }
    }
}