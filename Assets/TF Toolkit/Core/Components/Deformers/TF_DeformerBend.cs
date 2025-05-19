using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerBend : Deformer
    {
        public float angle;
        public float length = 1;
        public bool limited = true;

        protected override bool HasWork()
        {
            if (angle == 0)
            {
                return false;
            }
            return true;
        }

        protected override JobHandle UpdateVertices(DeformingMesh dm, JobHandle jobHandle)
        {
            if (limited)
            {
                return new MeshJob_Limited
                {
                    vertices = dm.vertices,
                    length = length,
                    heightDivAngle = length / (angle * Mathf.Deg2Rad),
                    angleDivHeight = angle * Mathf.Deg2Rad / length,
                    cosTop = Mathf.Cos(Mathf.PI - angle * Mathf.Deg2Rad),
                    sinTop = Mathf.Sin(Mathf.PI - angle * Mathf.Deg2Rad),
                }
                .Schedule(dm.vertices.Length, 2048, jobHandle);
            }
            else
            {
                return new MeshJob
                {
                    vertices = dm.vertices,
                    radians = Mathf.Deg2Rad * Mathf.Min(angle / Mathf.Max(length, 0.0001f), 36000),
                }
                .Schedule(dm.vertices.Length, 2048, jobHandle);
            }
        }



        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        public struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float radians;
       
            public void Execute(int i)
            {
                Vector3 vert = vertices[i];

                var scale = 1f / radians;
                var rotation = Mathf.PI - vert.y * radians;

                var c = math.cos(rotation);
                var s = math.sin(rotation);

                vert.y = (scale - vert.x) * s;
                vert.x = scale * (c + 1) - vert.x * c;

                vertices[i] = vert;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        public struct MeshJob_Limited : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float length;
            public float heightDivAngle;
            public float angleDivHeight;

            public float cosTop;
            public float sinTop;

            public void Execute(int i)
            {
                Vector3 vert = vertices[i];
                float x = vert.x;
                float y = vert.y;
                float z = vert.z;

                if (y <= 0)
                {
                    return;
                }

                if (y > length)
                {
                    float distanceFromTop = y - length;
                    y = (heightDivAngle - x) * sinTop - cosTop * distanceFromTop;
                    x = heightDivAngle * (cosTop + 1) - x * cosTop + sinTop * distanceFromTop;
                }
                else
                {
                    float rotation = Mathf.PI - y * angleDivHeight;

                    var c = math.cos(rotation);
                    var s = math.sin(rotation);

                    y = (heightDivAngle - x) * s;
                    x = heightDivAngle * (c + 1) - x * c;
                }

                vert.x = x;
                vert.y = y;
                vert.z = z;
                vertices[i] = vert;
            }
        }

        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            DrawGizmo(
                helpers.Plane,
                helpers.TransparentBlue,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(90, 0, 0), Vector3.one)
            );

            DrawGizmo(
                helpers.Circle,
                helpers.TransparentGreen,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 0), Vector3.one * length)
            );

            DrawGizmo(
                helpers.Cylinder,
                helpers.OpaqueBlue,
                objectMatrix * Matrix4x4.TRS(Vector3.up * length / 2, Quaternion.identity, new Vector3(0.01f, length / 2, 0.01f))
            );

            DrawGizmo(
                helpers.Cylinder,
                helpers.OpaqueRed,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, -angle), Vector3.one) * Matrix4x4.TRS(Vector3.up * length / 2, Quaternion.identity, new Vector3(0.01f, length / 2, 0.01f))
            );
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();
            length = Mathf.Max(length, 0);
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Bend";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerBend>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}