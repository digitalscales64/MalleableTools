using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerSuctionPoint : Deformer
    {
        [Tooltip("Past this distance, this deformer does nothing.")]
        public float zeroPowerDistance = 2;
        [Tooltip("Vertices closer than this distance will be pulled at full power.")]
        public float fullPowerDistance = 0;
        [Tooltip("Vertices are pulled this many meters towards the center when at full power.")]
        public float power = 0;

        protected override bool HasWork()
        {
            if (power == 0)
            {
                return false;
            }
            return true;
        }

        protected override JobHandle UpdateVertices(DeformingMesh dm, JobHandle jobHandle = default)
        {
            if (zeroPowerDistance == fullPowerDistance)
            {
                return new MeshJob_ZeroAndFullPowerEqual
                {
                    vertices = dm.vertices,
                    zeroPowerDistance = zeroPowerDistance,
                    power = power,
                }
                .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
            }

            return new MeshJob_NEW
            {
                vertices = dm.vertices,
                zeroPowerDistance = zeroPowerDistance,
                oneOverFullMinusZeroTimesPower = power / (fullPowerDistance - zeroPowerDistance),
                power = power,
            }
            .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_NEW : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float zeroPowerDistance;
            public float oneOverFullMinusZeroTimesPower;
            public float power;
 
            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                float4 distanceSquared = x * x + y * y + z * z;
                float4 distance = math.sqrt(distanceSquared);
                float4 distanceToMove = math.min((distance - zeroPowerDistance) * oneOverFullMinusZeroTimesPower, power);

                bool4 moveMask = distanceToMove <= distance;
                float4 multiplier = math.select(0, 1 - distanceToMove / distance, moveMask);

                x *= multiplier;
                y *= multiplier;
                z *= multiplier;

                bool4 error = distance >= zeroPowerDistance;

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_ZeroAndFullPowerEqual : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float zeroPowerDistance;
            public float power;
 
            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                float4 distanceSquared = x * x + y * y + z * z;
                float4 distance = math.sqrt(distanceSquared);

                bool4 moveMask = power <= distance;
                float4 multiplier = math.select(0, 1 - power / distance, moveMask);

                x *= multiplier;
                y *= multiplier;
                z *= multiplier;

                bool4 error = distance >= zeroPowerDistance;

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public Matrix4x4 inMatrix;
            public Matrix4x4 outMatrix;

            public float zeroPowerDistanceSquared;
            public float zeroPowerDistance;
            public float oneOverFullMinusZeroTimesPower;
            public float power;
            public Vector3 suctionPoint;

            public void Execute(int i)
            {
                Vector3 vert = inMatrix.MultiplyPoint3x4(vertices[i]);
   
                float distanceSquared = vert.sqrMagnitude;
                if (distanceSquared > zeroPowerDistanceSquared)
                {
                    return;
                }

                float distance = Mathf.Sqrt(distanceSquared);

                float distanceToMove = Mathf.Min((distance - zeroPowerDistance) * oneOverFullMinusZeroTimesPower, power);

                if (distanceToMove >= distance)
                {
                    vertices[i] = suctionPoint;
                    return;
                }

                vert *= 1 - distanceToMove / distance;
                
                vertices[i] = outMatrix.MultiplyPoint3x4(vert);
            }
        }

        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            //zeroPowerDistance
            DrawGizmo(
                helpers.Sphere,
                helpers.TransparentBlue,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * zeroPowerDistance * 2)
            );

            //fullPowerDistance
            DrawGizmo(
                helpers.Sphere,
                helpers.TransparentRed,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * fullPowerDistance * 2)
            );

            //point
            DrawGizmo(
                helpers.Sphere,
                helpers.OpaqueRed,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * 0.01f)
            );
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();
            zeroPowerDistance = Mathf.Max(zeroPowerDistance, 0);
            fullPowerDistance = Mathf.Max(fullPowerDistance, 0);
            fullPowerDistance = Mathf.Min(fullPowerDistance, zeroPowerDistance);
            power = Mathf.Max(power, 0);
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Suction Point";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerSuctionPoint>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}