using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerRollers : Deformer
    {
        public float[] rollerRadiuses = new float[] { 0.25f, 0.25f };

        const float MINIMUM_FALLOFF = 0.005f;
      
        public float flattenedHeight = MINIMUM_FALLOFF;

        public override void UpdateVertices(DeformingMeshManager manager)
        {
            manager.GetMinMax();

            var rollerRadiusesForTask = new NativeArray<float>(rollerRadiuses.Length, Allocator.TempJob);
            rollerRadiusesForTask.CopyFrom(rollerRadiuses);
            for (int i = 0; i < manager.deformingMeshes.Length; i++)
            {
                var dm = manager.deformingMeshes[i];
                JobHandle previousJob = manager.jobHandles[i];

                manager.jobHandles[i] = new MeshJob
                {
                    vertices = dm.vertices,
                    minMax = manager.minMax,
                    rollerRadiuses = rollerRadiusesForTask,
                    flattenedHeight = flattenedHeight,
                }
                .Schedule(dm.vertices.Length, 2048, previousJob);
            }

            JobHandle allJobs = JobHandle.CombineDependencies(manager.jobHandles);

            QueueNativeArrayDisposal(rollerRadiusesForTask, allJobs);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
       
            [ReadOnly] public NativeArray<float> rollerRadiuses;
            public float flattenedHeight;

            [ReadOnly]
            public NativeArray<float3> minMax;

            public void Execute(int i)
            {
                Vector3 vert = vertices[i];
                float x = vert.x;
                float y = vert.y;
                float z = vert.z;

                float smallestStartRollerRadius = math.min(rollerRadiuses[0], rollerRadiuses[1]);

                float min = minMax[0].y;
                float max = minMax[1].y;

                float height = math.max(max, -min);

                if (z <= -smallestStartRollerRadius)
                {
                    return;
                }

                if (z < 0)
                {
                    float heightToRoller = smallestStartRollerRadius - math.sqrt(smallestStartRollerRadius * smallestStartRollerRadius - z * z);

                    if (heightToRoller > height)
                    {
                        return;
                    }

                    float compression = math.max(heightToRoller, flattenedHeight) / height;
                    y *= compression;
                }
                else
                {
                    //We are inside or beyond the rollers
                    float compression = flattenedHeight / height;

                    y *= compression;
                    //x never changes unless i add spread

                    float totalDistanceBetweenEntranceAndExit = 0;

                    for (int rollerIndex = 2; rollerIndex < rollerRadiuses.Length; rollerIndex++)
                    {
                        float rollerRadius = rollerRadiuses[rollerIndex - 1];

                        float halfCircleDistance = rollerRadius * math.PI;

                        totalDistanceBetweenEntranceAndExit += halfCircleDistance;
                    }

                    if (z > totalDistanceBetweenEntranceAndExit)
                    {
                        //Its in the bit after all rollers

                        float currentYPos = 0;

                        for (int rollerIndex = 2; rollerIndex < rollerRadiuses.Length; rollerIndex++)
                        {
                            float rollerRadius = rollerRadiuses[rollerIndex - 1];
                            currentYPos -= rollerRadius * 2;
                        }

                        int exitDirection = (rollerRadiuses.Length % 2 == 0) ? 1 : -1;

                        z = (z - totalDistanceBetweenEntranceAndExit) * exitDirection;
                        y = currentYPos + y * exitDirection;
                    }
                    else
                    {
                        //Its within the rollers

                        float currentYPos = 0;
                        int zSign = 1;
                        float distanceSoFar = 0;
                        for (int rollerIndex = 2; rollerIndex < rollerRadiuses.Length; rollerIndex++)
                        {
                            float rollerRadius = rollerRadiuses[rollerIndex - 1];

                            currentYPos -= rollerRadius;

                            float halfCircleDistance = rollerRadius * math.PI;

                            if (z < distanceSoFar + halfCircleDistance)
                            {
                                //its in this part!

                                float distanceIntoHalfCircle = z - distanceSoFar;

                                float angle = math.PI * (distanceIntoHalfCircle / halfCircleDistance);

                                float halfAngle = angle / 2;

                                Quaternion pointRotation = new Quaternion(math.sin(halfAngle), 0, 0, math.cos(halfAngle));
                                Vector3 point = pointRotation * (Vector3.up * (rollerRadius + y * zSign));
                                z = point.z * zSign;
                                y = point.y + currentYPos;
                                break;
                            }

                            distanceSoFar += halfCircleDistance;
                            zSign = -zSign;
                            currentYPos -= rollerRadius;
                        }
                    }
                }

                vert.x = x;
                vert.y = y;
                vert.z = z;
                vertices[i] = vert;
            }
        }

        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            #region size

            float currentYPos = rollerRadiuses[0] * 2;

            bool blue = true;

            for (int i = 0; i < rollerRadiuses.Length; i++)
            {
                float radius = rollerRadiuses[i];

                currentYPos -= radius;

                DrawGizmo(
                    helpers.Cylinder,
                    blue ? helpers.TransparentBlue : helpers.TransparentRed,
                    objectMatrix * Matrix4x4.TRS(Vector3.up * currentYPos, Quaternion.Euler(90, 90, 0), new Vector3(radius, 1, radius))
                );

                blue = !blue;

                currentYPos -= radius;
            }


          


            #endregion



        }

        protected override void ValidateValues()
        {
            base.ValidateValues();

            if (rollerRadiuses.Length == 0)
            {
                rollerRadiuses = new float[] { 0.25f, 0.25f };
            }
            else if (rollerRadiuses.Length == 1)
            {
                float firstRadius = rollerRadiuses[0];
                rollerRadiuses = new float[] { firstRadius, firstRadius };
            }

            for (int i = 0; i < rollerRadiuses.Length; i++)
            {
                rollerRadiuses[i] = Mathf.Max(rollerRadiuses[i], 0);
            }

            flattenedHeight = Mathf.Max(flattenedHeight, MINIMUM_FALLOFF);
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Rollers";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerRollers>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}