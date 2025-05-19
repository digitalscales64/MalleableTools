using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TF_Toolkit
{
    [System.Serializable]
    public class SaveInfo
    {
        public List<MeshInfo> meshInfos = new List<MeshInfo>();

        [System.Serializable]
        public class MeshInfo
        {
            public SkinnedMeshRenderer skinnedMeshRenderer;
            public MeshFilter meshFilter;
            public Mesh originalMesh;

            public MeshInfo(SkinnedMeshRenderer skinnedMeshRenderer, Mesh originalMesh)
            {
                this.skinnedMeshRenderer = skinnedMeshRenderer;
                this.meshFilter = null;
                this.originalMesh = originalMesh;
            }

            public MeshInfo(MeshFilter meshFilter, Mesh originalMesh)
            {
                this.skinnedMeshRenderer = null;
                this.meshFilter = meshFilter;
                this.originalMesh = originalMesh;
            }
        }
        
        public bool IsEmpty()
        {
            return meshInfos.Count == 0;
        }

        public void Clear()
        {
            if (!Application.isPlaying)
            {
                meshInfos.Clear();
            }
        } 

        public Mesh GetMesh(SkinnedMeshRenderer skinnedMeshRenderer) {
            if (skinnedMeshRenderer == null)
            {
                return null;
            }
            for (int i = meshInfos.Count - 1; i >= 0; i--)
            {
                MeshInfo info = meshInfos[i];  
                if (info.skinnedMeshRenderer == skinnedMeshRenderer)
                {
                    Mesh original = info.originalMesh;
                    return original;
                }
            }
            return null;
        }
        public Mesh GetMesh(MeshFilter meshFilter)
        {
            if (meshFilter == null)
            {
                return null;
            }
            for (int i = meshInfos.Count - 1; i >= 0; i--)
            {
                MeshInfo info = meshInfos[i];
                if (info.meshFilter == meshFilter)
                {
                    Mesh original = info.originalMesh;
                    return original;
                }
            }
            return null;
        }

        public void AddMesh(SkinnedMeshRenderer skinnedMeshRenderer, Mesh originalMesh)
        {
            if (!Application.isPlaying)
            {
                if (skinnedMeshRenderer == null || originalMesh == null)
                {
                    return;
                }

                meshInfos.Add(new MeshInfo(skinnedMeshRenderer, originalMesh));
            }
        }

        public void AddMesh(MeshFilter meshFilter, Mesh originalMesh)
        {
            if (!Application.isPlaying) 
            {
                if (meshFilter == null || originalMesh == null)
                {
                    return;
                }

                meshInfos.Add(new MeshInfo(meshFilter, originalMesh));
            }
        }
    }
}