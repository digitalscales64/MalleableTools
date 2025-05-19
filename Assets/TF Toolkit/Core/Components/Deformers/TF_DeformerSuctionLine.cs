using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TF_Toolkit
{
    public class TF_DeformerSuctionLine : Deformer
    {
        [Tooltip("Past this distance, this deformer does nothing.")]
        public float zeroPowerDistance = 2;
        [Tooltip("Vertices closer than this distance will be pulled at full power.")]
        public float fullPowerDistance = 0;
        [Tooltip("Vertices are pulled this many meters towards the center when at full power.")]
        public float power = 0;
        [Tooltip("The length of the suction line.")]
        public float lineLength = 0.3f;

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
                return new MeshJob_ZeroEqualsFull
                {
                    vertices = dm.vertices,
                    halfLineLength = lineLength / 2f,
                    zeroPowerDistance = zeroPowerDistance,
                    power = power,
                }
                .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
            }

            return new MeshJob
            {
                vertices = dm.vertices,
                halfLineLength = lineLength / 2f,
                zeroPowerDistance = zeroPowerDistance,
                oneOverFullMinusZeroTimesPower = 1 / (fullPowerDistance - zeroPowerDistance) * power,
                power = power,
            }
            .Schedule(dm.vertices.Length / 4 + 1, 512, jobHandle);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float halfLineLength;
            public float zeroPowerDistance;
            public float oneOverFullMinusZeroTimesPower;
            public float power;

            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                bool4 error = false;

                float4 yClamped = math.clamp(y, -halfLineLength, halfLineLength);
                float4 yDistance = y - yClamped;

                float4 distanceSquared = x * x + z * z + yDistance * yDistance;
                float4 distance = math.sqrt(distanceSquared);
                float4 distanceToMove = math.min((distance - zeroPowerDistance) * oneOverFullMinusZeroTimesPower, power);

                bool4 moveMask = distanceToMove <= distance;
                float4 multiplier = math.select(0, 1 - distanceToMove / distance, moveMask);

                y = yDistance * multiplier + yClamped;

                x *= multiplier;
                z *= multiplier;

                error |= distance > zeroPowerDistance;
                error |= distance == 0;
                error |= distanceToMove <= 0;

                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
        struct MeshJob_ZeroEqualsFull : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            public float halfLineLength;
            public float zeroPowerDistance;
            public float power;
        
            public void Execute(int i)
            {
                PrepareXYZ(vertices, i, out float4 x, out float4 y, out float4 z);

                bool4 error = false;

                float4 yClamped = math.clamp(y, -halfLineLength, halfLineLength);
                float4 yDistance = y - yClamped;

                float4 distanceSquared = x * x + z * z + yDistance * yDistance;
                float4 distance = math.sqrt(distanceSquared);
        
                bool4 moveMask = power <= distance;
                float4 multiplier = math.select(0, 1 - power / distance, moveMask);

                y = yDistance * multiplier + yClamped;

                x *= multiplier;
                z *= multiplier;

                error |= distance > zeroPowerDistance;
                error |= distance == 0;
            
                ApplyXYZ(vertices, i, x, y, z, error);
            }
        }

        protected override void RenderGizmos(HelperDefinitions helpers, Matrix4x4 objectMatrix)
        {
            void DrawCapsule(Material material, float _radius, float _height)
            {
                DrawGizmo(
                    helpers.HalfSphereWithoutCap,
                    material,
                    objectMatrix * Matrix4x4.TRS(Vector3.up * _height / 2, Quaternion.identity, Vector3.one * _radius)
                );
                DrawGizmo(
                    helpers.HalfSphereWithoutCap,
                    material,
                    objectMatrix * Matrix4x4.TRS(Vector3.down * _height / 2, Quaternion.Euler(180, 0, 0), Vector3.one * _radius)
                );
                DrawGizmo(
                    helpers.CylinderWithoutCaps,
                    material,
                    objectMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_radius, _height / 2, _radius))
                );
            }

            //zeroPowerDistance
            DrawCapsule(helpers.TransparentBlue, zeroPowerDistance, lineLength);
 
            //fullPowerDistance
            DrawCapsule(helpers.TransparentRed, fullPowerDistance, lineLength);

            //Line
            DrawCapsule(helpers.OpaqueRed, 0.01f, lineLength);
        }

        protected override void ValidateValues()
        {
            base.ValidateValues();
            zeroPowerDistance = Mathf.Max(zeroPowerDistance, 0);
            fullPowerDistance = Mathf.Max(fullPowerDistance, 0);
            fullPowerDistance = Mathf.Min(fullPowerDistance, zeroPowerDistance);
            power = Mathf.Max(power, 0);
            lineLength = Mathf.Max(lineLength, 0);
        }

#if UNITY_EDITOR
        const string DEFORMER_NAME = "Suction Line";
        [UnityEditor.MenuItem("GameObject/TF Toolkit/" + DEFORMER_NAME, false, 0)] //10
        private static void MenuItem(UnityEditor.MenuCommand menuCommand)
        {
            var gameObject = new GameObject("TF_Deformer " + DEFORMER_NAME);
            var createdDeformer = gameObject.AddComponent<TF_DeformerSuctionLine>();
            AddNewDeformerToDeformable(createdDeformer);
        }
#endif
    }
}