using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerPlane : Deformer
    {
        const float MINIMUM_FALLOFF = 0.005f;
        public bool movePlanesTogether = true;
        public float planePositionAbove = 0.2f;
        public float planePositionBelow = -0.2f;
        public float spreadX = 0;
        public float spreadZ = 0;

        [SerializeField]
        [HideInInspector]
        private float prev_planePositionAbove = 0.2f;
        [SerializeField]
        [HideInInspector]
        private float prev_planePositionBelow = -0.2f;
        [SerializeField]
        [HideInInspector]
        private bool prev_movePlanesTogether = true;

        public override void UpdateVertices(DeformingMeshManager manager)
        {
            manager.GetMinMax();

            var matrix = new NativeArray<float4x4>(1, Allocator.TempJob);
            var createMatrixJob = new CreateMatrixJob
            {
                matrix = matrix,
                minMax = manager.minMax,//output,
                planePositionAbove = planePositionAbove,
                planePositionBelow = planePositionBelow,
                spreadX = spreadX,
                spreadZ = spreadZ,
            }
            .Schedule(manager.jobHandles[0]);

            for (int i = 0; i < manager.deformingMeshes.Length; i++)
            {
                var dm = manager.deformingMeshes[i];
            
                manager.jobHandles[i] = new MeshJob
                {
                    vertices = dm.vertices,
                    matrix = matrix,
                }
                .Schedule(dm.vertices.Length, 2048, createMatrixJob);           
            }

            QueueNativeArrayDisposal(matrix, JobHandle.CombineDependencies(manager.jobHandles));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct CreateMatrixJob : IJob
        {
            [WriteOnly]
            public NativeArray<float4x4> matrix;
            [ReadOnly]
            public NativeArray<float3> minMax;
            public float planePositionAbove;
            public float planePositionBelow;
            public float spreadX;
            public float spreadZ;

            public void Execute()
            {
                float min = minMax[0].y;
                float max = minMax[1].y;
                float height = max - min;

                float distanceBetweenPlanes = planePositionAbove - planePositionBelow;
                float compression = math.min(height, distanceBetweenPlanes) / height;

                float move;
                float spreadMultiplier;
                if (distanceBetweenPlanes > height)
                {
                    move = math.min(0, planePositionAbove - max) + math.max(0, planePositionBelow - min);
                }
                else
                {
                    move = planePositionBelow - min * compression;
                }

                if (height == MINIMUM_FALLOFF * 2)
                {
                    spreadMultiplier = 0; //Doing this here makes it so that a NaN check can be skipped later
                }
                else
                {
                    spreadMultiplier = math.clamp((distanceBetweenPlanes - height) / (MINIMUM_FALLOFF * 2 - height), 0, 1);
                }

                float3 spread = new float3(
                    1 + spreadX * spreadMultiplier,
                    compression,
                    1 + spreadZ * spreadMultiplier
                );

                matrix[0] = new float4x4(
                    spread.x, 0, 0, 0,
                    0, spread.y, 0, move,
                    0, 0, spread.z, 0,
                    0, 0, 0, 1
                );
            }
        }
        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] 
            public NativeArray<Vector3> vertices;
            [ReadOnly]
            //[DeallocateOnJobCompletion]
            public NativeArray<float4x4> matrix;
     
            public void Execute(int i)
            {
                vertices[i] = math.transform(matrix[0], vertices[i]);
            }
        }

        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            #region size
            DrawGizmo(
                helpers.Plane,
                helpers.TransparentBlue,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(90, 0, 0), Vector3.one * 1.5f)
            );
            #endregion

            #region fallOffDistance
            DrawGizmo(
                helpers.Plane,
                helpers.TransparentRed,
                objectMatrix * Matrix4x4.TRS(Vector3.up * planePositionAbove, Quaternion.Euler(90, 0, 0), Vector3.one * 1.5f)
            );
            DrawGizmo(
                helpers.Plane,
                helpers.TransparentRed,
                objectMatrix * Matrix4x4.TRS(Vector3.up * planePositionBelow, Quaternion.Euler(90, 0, 0), Vector3.one * 1.5f)
            );
            #endregion
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();
            //planePositionAbove = Mathf.Max(planePositionAbove, MINIMUM_FALLOFF * 0.5f);
            //planePositionBelow = Mathf.Max(planePositionBelow, MINIMUM_FALLOFF * 0.5f);
            spreadX = Mathf.Max(spreadX, 0);
            spreadZ = Mathf.Max(spreadZ, 0);

            if (movePlanesTogether)
            {
                if (planePositionAbove != prev_planePositionAbove)
                {
                    planePositionBelow = -planePositionAbove;
                }
                else if (planePositionBelow != prev_planePositionBelow)
                {
                    planePositionAbove = -prev_planePositionBelow;
                }

                if (!prev_movePlanesTogether)
                {
                    if (Mathf.Abs(planePositionAbove) > Mathf.Abs(planePositionBelow))
                    {
                        planePositionBelow = -planePositionAbove;
                    }
                    else
                    {
                        planePositionAbove = -planePositionBelow;
                    }
                }


                planePositionAbove = Mathf.Max(planePositionAbove, MINIMUM_FALLOFF * 0.5f);
                planePositionBelow = Mathf.Min(planePositionBelow, -MINIMUM_FALLOFF * 0.5f);
            }
            else
            {
                if (planePositionAbove != prev_planePositionAbove)
                {
                    planePositionAbove = Mathf.Max(planePositionAbove, planePositionBelow + MINIMUM_FALLOFF);
                }
                else if (planePositionBelow != prev_planePositionBelow)
                {
                    planePositionBelow = Mathf.Min(planePositionBelow, planePositionAbove - MINIMUM_FALLOFF);
                }
            }
            prev_movePlanesTogether = movePlanesTogether;
            prev_planePositionBelow = planePositionBelow;
            prev_planePositionAbove = planePositionAbove;
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Plane";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerPlane>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}