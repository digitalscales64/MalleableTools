using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace TF_Toolkit {
    public class TF_DeformerCylinderSplineXZ : Deformer
    {
        public float height = 1;
        public float fallOffDistance = 1;
        private int SegmentsAround = 32;
        public int SegmentsHeight = 60;
        public float[] heightSegmentWidths = new float[] { 0, 0.2f, 0 };

        Mesh mesh;

        float prev_height = 0;
        float prev_SegmentsAround = 0;
        float prev_SegmentsHeight = 0;
        float[] prev_heightSegmentWidths = new float[0];

        public Vector2[] cubicPoints;

        bool visualNeedsUpdate = false;

        // Start is called before the first frame update
        void Start()
        {
            mesh = new Mesh();
            mesh.MarkDynamic();
        }

        void UpdateVisualMesh()
        {
            CubicSpline.GenerateCylinderGeometry(mesh, cubicPoints, height, SegmentsAround);

            prev_SegmentsAround = SegmentsAround;

            visualNeedsUpdate = false;
        }
        void UpdatePhysicalValues()
        {
            Vector2[] points = new Vector2[heightSegmentWidths.Length];
            for (int i = 0; i < points.Length; i++)
            {
                points[i].x = (((float)i) / (float)(heightSegmentWidths.Length - 1f)) * height - height / 2f;
                points[i].y = heightSegmentWidths[i];
            }
            cubicPoints = CubicSpline.InterpolateXY(points, SegmentsHeight);

            prev_height = height;
            prev_SegmentsHeight = SegmentsHeight;
            prev_heightSegmentWidths = new float[heightSegmentWidths.Length];
            for (int i = 0; i < heightSegmentWidths.Length; i++)
            {
                prev_heightSegmentWidths[i] = heightSegmentWidths[i];
            }

            visualNeedsUpdate = true;
        }

        bool PhysicalValuesHaveChanged()
        {
            if (height != prev_height)
            {
                return true;
            }
            if (SegmentsHeight != prev_SegmentsHeight)
            {
                return true;
            }
            if (heightSegmentWidths.Length != prev_heightSegmentWidths.Length)
            {
                return true;
            }
            for (int i = 0; i < heightSegmentWidths.Length; i++)
            {
                if (heightSegmentWidths[i] != prev_heightSegmentWidths[i])
                {
                    return true;
                }
            }
            return false;
        }
        bool VisualMeshHasChanged()
        {
            if (SegmentsAround != prev_SegmentsAround)
            {
                return true;
            }
            return false;
        }

        public override void UpdateVertices(DeformingMeshManager manager)
        {
            if (height == 0)
            {
                return;
            }

            if (PhysicalValuesHaveChanged())
            {
                UpdatePhysicalValues();
            }

            if (cubicPoints.Length == 0)
            {
                return;
            }

            float biggestRadius = 0;
            for (int i = 0; i < cubicPoints.Length; i++)
            {
                if (biggestRadius < cubicPoints[i].y)
                {
                    biggestRadius = cubicPoints[i].y;
                }
            }

            if (biggestRadius == 0)
            {
                return;
            }

            float top = height / 2;
            float bottomn = -height / 2;

            NativeArray<Vector3> values = new NativeArray<Vector3>(cubicPoints.Length - 1, Allocator.TempJob);

            for (int i = 1; i < cubicPoints.Length; i++)
            {
                ref Vector2 nextPoint = ref cubicPoints[i];
                ref Vector2 prevPoint = ref cubicPoints[i - 1];

                float prevHeight = prevPoint.x;
                float prevRadius = prevPoint.y;

                float nextHeight = nextPoint.x;
                float nextRadius = nextPoint.y;

                if (nextHeight - prevHeight == 0)
                {
                    values[i - 1] = new Vector4(
                        nextPoint.x,
                        0,
                        prevRadius
                    );
                }
                else
                {
                    float radiusDiffDivHeightDiff = (nextRadius - prevRadius) / (nextHeight - prevHeight);

                    values[i - 1] = new Vector4(
                        nextPoint.x,
                        radiusDiffDivHeightDiff,
                        prevRadius - prevHeight * radiusDiffDivHeightDiff
                    );
                }
            }

            float biggestOuterRadiusSquared = (biggestRadius + fallOffDistance) * (biggestRadius + fallOffDistance);

            for (int i = 0; i < manager.deformingMeshes.Length; i++)
            {
                var dm = manager.deformingMeshes[i];
                JobHandle previousJob = manager.jobHandles[i];

                manager.jobHandles[i] = new MeshJob
                {
                    vertices = dm.vertices,
                    values = values,
                    top = top,
                    bottomn = bottomn,
                    biggestOuterRadiusSquared = biggestOuterRadiusSquared,
                    fallOffDistance = fallOffDistance,
                }
                .Schedule(dm.vertices.Length, 2048, previousJob);
            }

            JobHandle allJobs = JobHandle.CombineDependencies(manager.jobHandles);
            QueueNativeArrayDisposal(values, allJobs);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            [ReadOnly] 
            public NativeArray<Vector3> values;
            public float top;
            public float bottomn;
            public float biggestOuterRadiusSquared;
            public float fallOffDistance;

            public void Execute(int i)
            {
                Vector3 vert = vertices[i];
                float x = vert.x;
                float y = vert.y;
                float z = vert.z;


                if (y >= top || y <= bottomn)
                {
                    return;
                }


                float distanceSquared = x * x + z * z;

                if (distanceSquared >= biggestOuterRadiusSquared)
                {
                    return; //outside endless cylinder
                }

                float radius = GetRadius(y);

                if (radius == 0)
                {
                    return;
                }

                float outerRadius = radius + fallOffDistance;

                if (distanceSquared > outerRadius * outerRadius)
                {
                    return;
                }

                float multiplier = 1 + radius / Mathf.Sqrt(distanceSquared) - radius / outerRadius;

                x *= multiplier;
                z *= multiplier;
                //y += pushAmount;

                vert.x = x;
                vert.y = y;
                vert.z = z;
                vertices[i] = vert;
            }

            float GetRadius(float y)
            {
                unchecked
                {
                    for (int i = 0; i < values.Length; i++)
                    {
                        if (y < values[i].x)
                        {
                            return y * values[i].y + values[i].z;
                        }
                    }
                    return 0;
                }
            }
        }

        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            if (PhysicalValuesHaveChanged())
            {
                UpdatePhysicalValues();
            }
            if (visualNeedsUpdate || VisualMeshHasChanged())
            {
                UpdateVisualMesh();
            }

            DrawGizmo(
                mesh,
                helpers.OpaqueGreen,
                objectMatrix
            );
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();
            height = Mathf.Max(height, 0);
            SegmentsAround = (int)Mathf.Max(SegmentsAround, 1);
            SegmentsHeight = (int)Mathf.Max(SegmentsHeight, 1);
            fallOffDistance = Mathf.Max(fallOffDistance, 0);
            if (heightSegmentWidths == null || heightSegmentWidths.Length < 2)
            {
                heightSegmentWidths = new float[2];
            }
            for (int i = 0; i < heightSegmentWidths.Length; i++)
            {
                heightSegmentWidths[i] = Mathf.Max(heightSegmentWidths[i], 0);
            }
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Cylinder Spline XZ";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerCylinderSplineXZ>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}
