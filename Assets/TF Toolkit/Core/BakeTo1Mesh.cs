using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace TF_Toolkit
{
    public class BakeTo1Mesh : MonoBehaviour
    {
        public void Bake()
        {
            SkinnedMeshRenderer[] skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>();

            #region Get Materials
            List<Material> materials = new List<Material>();
            for (int i = 0; i < skinnedMeshRenderers.Length; i++)
            {
                materials.AddRange(skinnedMeshRenderers[i].sharedMaterials);
            }
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                materials.AddRange(meshRenderers[i].sharedMaterials);
            }
            #endregion

            List<Mesh> meshes = getMeshes(skinnedMeshRenderers, meshRenderers);



            Mesh _mergeMesh = createMergeMesh(meshes);
            _mergeMesh.name = name;

            foreach (Mesh m in meshes)
            {
                DestroyImmediate(m);
            }

            #region Save Blueprint and Mesh Assets

#if UNITY_EDITOR
            AssetDatabase.CreateAsset(_mergeMesh, "Assets/" + name + ".asset");
            AssetDatabase.SaveAssets();
#endif
            #endregion
        }

        List<Mesh> getMeshes(SkinnedMeshRenderer[] skinnedMeshRenderers, MeshRenderer[] meshRenderers)
        {
            List<Mesh> meshes = new List<Mesh>();

            Matrix4x4 scaleMatrix = Matrix4x4.Scale(transform.localScale);
            Matrix4x4 worldToLocal = scaleMatrix * transform.worldToLocalMatrix;

            for (int i = 0; i < skinnedMeshRenderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = skinnedMeshRenderers[i];
                if (!renderer.gameObject.activeInHierarchy) { continue; }
                if (renderer.sharedMesh == null) { continue; }

                GameObject copy = Instantiate(renderer.gameObject);
                SkinnedMeshRenderer rendererCopy = copy.GetComponent<SkinnedMeshRenderer>();

                rendererCopy.transform.parent = transform;
                rendererCopy.transform.localPosition = Vector3.zero;
                rendererCopy.transform.localRotation = Quaternion.identity;
                rendererCopy.transform.localScale = Vector3.one;

                Mesh mesh = new Mesh();
                rendererCopy.BakeMesh(mesh, true);

                DestroyImmediate(copy);

                meshes.Add(mesh);
            }

            for (int i = 0; i < meshRenderers.Length; i++)
            {
                MeshRenderer renderer = meshRenderers[i];
                if (!renderer.gameObject.activeInHierarchy) { continue; }
                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) { continue; }

                Mesh mesh = Instantiate(meshFilter.sharedMesh);
                Matrix4x4 matrix = worldToLocal * renderer.localToWorldMatrix;

                Vector3[] vertices = mesh.vertices;
                for (int j = 0; j < vertices.Length; j++)
                {
                    vertices[j] = matrix.MultiplyPoint3x4(vertices[j]);
                }
                mesh.vertices = vertices;

                meshes.Add(mesh);
            }

            return meshes;
        }

        Mesh createMergeMesh(List<Mesh> meshes)
        {
            Mesh mergeMesh = new Mesh();

            List<Vector3> mergeVertices = new List<Vector3>();
            List<Vector3> mergeNormals = new List<Vector3>();
            List<Vector2> mergeUvs = new List<Vector2>();
            List<int> mergeIndices = new List<int>();

            int vertexOffset = 0;
            uint indexOffset = 0;

            List<UnityEngine.Rendering.SubMeshDescriptor> subMeshes = new List<UnityEngine.Rendering.SubMeshDescriptor>();

            for (int meshIndex = 0; meshIndex < meshes.Count; meshIndex++)
            {
                if (indexOffset > int.MaxValue) throw new System.ArithmeticException("indexOffset is greater than int.MaxValue");

                Mesh mesh = meshes[meshIndex];

                mergeVertices.AddRange(mesh.vertices);
                mergeNormals.AddRange(mesh.normals);
                mergeIndices.AddRange(mesh.triangles);
                mergeUvs.AddRange(mesh.uv);

                uint indexCount = 0;

                for (int submeshIndex = 0; submeshIndex < mesh.subMeshCount; submeshIndex++)
                {
                    indexCount += mesh.GetIndexCount(submeshIndex);

                    UnityEngine.Rendering.SubMeshDescriptor subMeshDescriptor = mesh.GetSubMesh(submeshIndex);

                    subMeshDescriptor.baseVertex += vertexOffset;
                    subMeshDescriptor.indexStart += (int)indexOffset;

                    subMeshes.Add(subMeshDescriptor);
                }

                vertexOffset += mesh.vertexCount;
                indexOffset += indexCount;
            }

            if (mergeVertices.Count > ushort.MaxValue)
            {
                mergeMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            else
            {
                mergeMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
            }

            mergeMesh.vertices = mergeVertices.ToArray();
            mergeMesh.normals = mergeNormals.ToArray();
            mergeMesh.triangles = mergeIndices.ToArray();
            mergeMesh.uv = mergeUvs.ToArray();
            mergeMesh.SetSubMeshes(subMeshes, 0, subMeshes.Count);

            mergeMesh.RecalculateBounds();

            return mergeMesh;
        }
    }
}