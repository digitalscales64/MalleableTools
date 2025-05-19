using System.Collections.Generic;
using UnityEngine;

namespace TF_Toolkit
{
    [DefaultExecutionOrder(20000)]
    [ExecuteInEditMode]
    public class TF_Deformable : DeformingMeshManager
    {
        float timeSinceLastRendererUpdateCheck = 0;

        [HideInInspector] public bool DEBUG_RecreateMeshesEveryFrame = false;

        bool RenderersNeedsUpdate(List<SkinnedMeshRenderer> skinnedMeshRenderers, List<MeshRenderer> meshRenderer, List<MeshFilter> meshFilters)
        {
            timeSinceLastRendererUpdateCheck = 0;

            if (skinnedMeshRenderers.Count != renderers.Count)
            {
                return true;
            }
            if (meshRenderer.Count != meshRenderers.Count)
            {
                return true;
            }

            for (int i = 0; i < skinnedMeshRenderers.Count; i++)
            {
                if (skinnedMeshRenderers[i] != renderers[i])
                {
                    return true;
                }
            }
            for (int i = 0; i < meshRenderer.Count; i++)
            {
                if (meshRenderer[i] != meshRenderers[i])
                {
                    return true;
                }
            }

            foreach (Mesh mesh in ORIGINAL_MESHES)
            {
                if (mesh == null) return true;
                if (mesh.vertexCount == 0) return true;
                if (!mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Position)) return true;
            }

            int meshIndex = 0;
            for (int i = 0; i < renderers.Count; i++)
            {
                SkinnedMeshRenderer renderer = renderers[i];

                if (renderer == null) return true;
                if (!renderer.enabled) return true;
                if (!renderer.gameObject.activeInHierarchy) return true;

                meshIndex++;
            }

            for (int i = 0; i < meshRenderers.Count; i++)
            {
                MeshRenderer renderer = meshRenderers[i];
                MeshFilter meshFilter = meshFilters[i];

                if (renderer == null) return true;
                if (meshFilter == null) return true;
                if (!renderer.enabled) return true;
                if (!renderer.gameObject.activeInHierarchy) return true;

                meshIndex++;
            }

            return false;
        }

        public void UpdateRenderers()
        {
            Disable(false);
    
            var skinnedMeshRenderers = GetRenderers();
            GetMeshRenderers(out List<MeshRenderer> meshRenderers, out List<MeshFilter> meshFilters);
            if (!RenderersNeedsUpdate(skinnedMeshRenderers, meshRenderers, meshFilters) && !DEBUG_RecreateMeshesEveryFrame)
            {
                Enable();
                return;
            }
        
            RecreateMeshes(skinnedMeshRenderers, meshRenderers, meshFilters);   
        }

        void Init()
        {
            Disable(false);
            List<SkinnedMeshRenderer> skinnedMeshRenderers = GetRenderers();
            GetMeshRenderers(out List<MeshRenderer> meshRenderers, out List<MeshFilter> meshFilters);
            RecreateMeshes(skinnedMeshRenderers, meshRenderers, meshFilters);
            initialized = true;
        }

        void LateUpdate()
        {
            CodeTimer.printSavedTimes();
            //CodeTimer timer = new CodeTimer();

#if UNITY_EDITOR
            if (!Application.isPlaying && !updateInEditMode && wasRunningInEditMode != updateInEditMode)
            {
                Dispose();
                return;
            }
            wasRunningInEditMode = updateInEditMode;
#endif

            if (!HasActiveDeformer())
            {
                Dispose();
                return;
            }

            if (!initialized || !jobHandles.IsCreated)
            {
                Init();
            }
            else
            {
                timeSinceLastRendererUpdateCheck += Time.deltaTime;

                if (renderersChangedCheckCooldown >= 0 && timeSinceLastRendererUpdateCheck > renderersChangedCheckCooldown)
                {
                    UpdateRenderers();
                }
            }

            Deform(deformers);

            //timer.addTime("update: ", 100);
        }

        List<SkinnedMeshRenderer> GetRenderers()
        {
            SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            List<SkinnedMeshRenderer> validRenderers = new List<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                //These 3 things are checked automatically by not passing IncludeInactive = true to GetComponentsInChildren
                if (renderer == null) continue;
                if (!renderer.enabled) continue;
                if (!renderer.gameObject.activeInHierarchy) continue;
                if (renderer.sharedMesh == null) continue;
                if (renderer.sharedMesh.vertexCount == 0) continue;
                if (!renderer.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Position)) continue;

                validRenderers.Add(renderer);
            }
            return validRenderers;
        }
        void GetMeshRenderers(out List<MeshRenderer> outMeshRenderers, out List<MeshFilter> outMeshFilters)
        {
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
            outMeshRenderers = new List<MeshRenderer>();
            outMeshFilters = new List<MeshFilter>();
            foreach (MeshRenderer renderer in renderers)
            {
                //These 3 things are checked automatically by not passing IncludeInactive = true to GetComponentsInChildren
                if (renderer == null) continue;
                if (!renderer.enabled) continue;
                if (!renderer.gameObject.activeInHierarchy) continue;
                if (!renderer.TryGetComponent<MeshFilter>(out MeshFilter meshFilter)) continue;
                if (meshFilter.sharedMesh == null) continue;
                if (meshFilter.sharedMesh.vertexCount == 0) continue;
                if (!meshFilter.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Position)) continue;

                outMeshRenderers.Add(renderer);
                outMeshFilters.Add(meshFilter);
            }
        }

        //void OnDestroy()
        //{
        //    DestroyDeformingMesh();
        //}

        void OnDisable()
        {
            Dispose();
        }

        void Dispose()
        {
            DisposeArrays();
            DestroyMeshes();
            initialized = false;
        }

        


































    

        
















    }
}