using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    [ExecuteInEditMode]
    public abstract class Deformer : MonoBehaviour
    {
        [Tooltip("Shows helper geometries if enabled.")]
        public bool showHelpers = true;

        protected virtual bool HasWork()
        {
            return true;
        }

        public virtual void UpdateVertices(DeformingMeshManager manager)
        {
            ValidateValues();

            if (!HasWork())
            {
                return;
            }

            for (int i = 0; i < manager.deformingMeshes.Length; i++)
            {
                manager.jobHandles[i] = UpdateVertices(manager.deformingMeshes[i], manager.jobHandles[i]);
            }
        }

        public static void QueueNativeArrayDisposal<T>(NativeArray<T> nativeArray, JobHandle dependency) where T : struct
        {
            new DisposeJob<T>
            {
                dispose = nativeArray,
            }.Schedule(dependency);
        }

        struct DisposeJob<T> : IJob where T : struct
        {
            [DeallocateOnJobCompletion] public NativeArray<T> dispose;

            public void Execute()
            {
                //Do nothing
            }
        }

        protected virtual JobHandle UpdateVertices(DeformingMesh deformingMesh, JobHandle jobHandle = default)
        {
            throw new System.NotImplementedException("Child classes need to eighter override UpdateVertices(DeformingMeshManager) or UpdateVertices(UpdateVertices)");
        }
 
        protected virtual void Update()
        {
            if (showHelpers)
            {
                RenderGizmos(HelperDefinitions.instance, transform.localToWorldMatrix);
            }
        }

        protected virtual void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
        }

        protected void DrawGizmo(Mesh mesh, Material material, Matrix4x4 matrix)
        {
            Graphics.DrawMesh(mesh, matrix, material, 0);
        }

        protected virtual void OnValidate()
        {
            ValidateValues();
        }
        protected virtual void ValidateValues() 
        {
        }

        public static void PrepareXYZ(NativeArray<Vector3> vertices, int i, out float4 x, out float4 y, out float4 z)
        {
            int i4 = i * 4;
            int imax = vertices.Length - 1;
            int i0 = math.min(i4 + 0, imax);
            int i1 = math.min(i4 + 1, imax);
            int i2 = math.min(i4 + 2, imax);
            int i3 = math.min(i4 + 3, imax);
            float3 vert0 = vertices[i0];
            float3 vert1 = vertices[i1];
            float3 vert2 = vertices[i2];
            float3 vert3 = vertices[i3];
            x = new float4(vert0.x, vert1.x, vert2.x, vert3.x);
            y = new float4(vert0.y, vert1.y, vert2.y, vert3.y);
            z = new float4(vert0.z, vert1.z, vert2.z, vert3.z);
        }

        public static void ApplyXYZ(NativeArray<Vector3> vertices, int i, float4 x, float4 y, float4 z, bool4 error)
        {
            int i4 = i * 4;
            int imax = vertices.Length - 1;
            int i0 = math.min(i4 + 0, imax);
            int i1 = math.min(i4 + 1, imax);
            int i2 = math.min(i4 + 2, imax);
            int i3 = math.min(i4 + 3, imax);
            vertices[i0] = error.x ? vertices[i0] : new float3(x.x, y.x, z.x);
            vertices[i1] = error.y ? vertices[i1] : new float3(x.y, y.y, z.y);
            vertices[i2] = error.z ? vertices[i2] : new float3(x.z, y.z, z.z);
            vertices[i3] = error.w ? vertices[i3] : new float3(x.w, y.w, z.w);
        }



#if UNITY_EDITOR
        protected static void AddNewDeformerToDeformable(Deformer deformer)
        {
            GameObject selected = UnityEditor.Selection.activeObject as GameObject;
            if (selected != null)
            {
                //we have a selected object
                TF_Deformable deformable = selected.GetComponent<TF_Deformable>();
                if (deformable != null)
                {
                    if (deformable.deformers == null)
                    {
                        deformable.deformers = new Deformer[] { deformer };
                    }
                    else
                    {
                        var deformers = new Deformer[deformable.deformers.Length + 1];
                        for (int i = 0; i < deformable.deformers.Length; i++)
                        {
                            deformers[i] = deformable.deformers[i];
                        }
                        deformers[deformable.deformers.Length] = deformer;
                        deformable.deformers = deformers;
                    }
                }
                deformer.gameObject.transform.parent = selected.transform;
                deformer.gameObject.transform.localPosition = Vector3.zero;
                deformer.gameObject.transform.localRotation = Quaternion.identity;
                deformer.gameObject.transform.localScale = Vector3.one;
            }
        }
#endif
    }
}