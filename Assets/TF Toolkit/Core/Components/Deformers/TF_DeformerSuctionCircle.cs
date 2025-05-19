using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerSuctionCircle : Deformer
    {
        [Tooltip("Past this distance, this deformer does nothing.")]
        public float zeroPowerDistance = 2;
        [Tooltip("Vertices closer than this distance will be pulled at full power.")]
        public float fullPowerDistance = 0;
        [Tooltip("Vertices are pulled this many meters towards the center when at full power.")]
        public float power = 0;

        public float radius = 0.05f;

        protected override bool HasWork()
        {
            if (power <= 0)
            {
                return false;
            }

            return true;
        }

        protected override JobHandle UpdateVertices(DeformingMesh dm, JobHandle jobHandle = default)
        {
            if (zeroPowerDistance == fullPowerDistance)
            {
                if (radius <= 0)
                {
                    return new MeshJob_ZeroEqualsFull_ZeroRadius
                    {
                        vertices = dm.vertices,
                        zeroPowerDistance = zeroPowerDistance,
                        power = power,
                    }
                .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
                }
                return new MeshJob_ZeroEqualsFull
                {
                    vertices = dm.vertices,
                    radius = radius,
                    zeroPowerDistance = zeroPowerDistance,
                    power = power,
                }
                .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
            }

            float oneOverFullMinusZeroTimesPower = power / (fullPowerDistance - zeroPowerDistance);

            if (radius <= 0)
            {
                return new MeshJob_ZeroRadius
                {
                    vertices = dm.vertices,
                    zeroPowerDistance = zeroPowerDistance,
                    oneOverFullMinusZeroTimesPower = oneOverFullMinusZeroTimesPower,
                    power = power,
                }
                .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
            }

            return new MeshJob
            {
                vertices = dm.vertices,
                radius = radius,
                zeroPowerDistance = zeroPowerDistance,
                oneOverFullMinusZeroTimesPower = oneOverFullMinusZeroTimesPower,
                power = power,
            }
            .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float radius;
            public float zeroPowerDistance;
           
            public float oneOverFullMinusZeroTimesPower;
            public float power;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                float4 xxzz = x * x + z * z;

                bool4 error = false;

                float4 horizontalMultiplier = radius / math.max(math.sqrt(xxzz), radius);

                float4 closestX = x * horizontalMultiplier;
                float4 closestZ = z * horizontalMultiplier;

                float4 distanceX = closestX - x;
                float4 distanceZ = closestZ - z;
                float4 distanceSquared = distanceX * distanceX + y * y + distanceZ * distanceZ;
                float4 distance = math.sqrt(distanceSquared);

                error |= distance >= zeroPowerDistance;
                error |= distance == 0;

                float4 distanceToMove = math.min((distance - zeroPowerDistance) * oneOverFullMinusZeroTimesPower, power);

                bool4 moveMask = distanceToMove <= distance;
                float4 moveAmount = math.select(0, 1 - distanceToMove / distance, moveMask);

                x = math.lerp(closestX, x, moveAmount);
                y *= moveAmount;
                z = math.lerp(closestZ, z, moveAmount);
                
                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_ZeroRadius : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float zeroPowerDistance;
            public float oneOverFullMinusZeroTimesPower;
            public float power;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                bool4 error = false;

                float4 distance = math.sqrt(x * x + y * y + z * z);

                error |= distance >= zeroPowerDistance;
                error |= distance == 0;

                float4 distanceToMove = math.min((distance - zeroPowerDistance) * oneOverFullMinusZeroTimesPower, power);

                bool4 moveMask = distanceToMove <= distance;
                float4 moveAmount = math.select(0, 1 - distanceToMove / distance, moveMask);

                x *= moveAmount;
                y *= moveAmount;
                z *= moveAmount;

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_ZeroEqualsFull : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float radius;
            public float zeroPowerDistance;
            public float power;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                float4 xxzz = x * x + z * z;

                bool4 error = false;

                float4 horizontalMultiplier = radius / math.max(math.sqrt(xxzz), radius);

                float4 closestX = x * horizontalMultiplier;
                float4 closestZ = z * horizontalMultiplier;

                float4 distanceX = closestX - x;
                float4 distanceZ = closestZ - z;
                float4 distanceSquared = distanceX * distanceX + y * y + distanceZ * distanceZ;
                float4 distance = math.sqrt(distanceSquared);

                error |= distance >= zeroPowerDistance;
                error |= distance == 0;

                bool4 moveMask = power <= distance;
                float4 moveAmount = math.select(0, 1 - power / distance, moveMask);

                x = math.lerp(closestX, x, moveAmount);
                y *= moveAmount;
                z = math.lerp(closestZ, z, moveAmount);

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_ZeroEqualsFull_ZeroRadius : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float zeroPowerDistance;
            public float power;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                bool4 error = false;

                float4 distance = math.sqrt(x * x + y * y + z * z);

                error |= distance >= zeroPowerDistance;
                error |= distance == 0;

                bool4 moveMask = power <= distance;
                float4 moveAmount = math.select(0, 1 - power / distance, moveMask);

                x *= moveAmount;
                y *= moveAmount;
                z *= moveAmount;

                ApplyXYZ(vertices, i, x, y, z, error);
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
                helpers.Cylinder,
                helpers.OpaqueRed,
                objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(radius, 0.01f, radius))
            );
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();
            zeroPowerDistance = Mathf.Max(zeroPowerDistance, 0);
            fullPowerDistance = Mathf.Max(fullPowerDistance, 0);
            fullPowerDistance = Mathf.Min(fullPowerDistance, zeroPowerDistance);
            power = Mathf.Max(power, 0);
            radius = Mathf.Max(radius, 0);
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Suction Circle";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerSuctionCircle>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}