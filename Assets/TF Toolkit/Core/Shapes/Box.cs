using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TF_Toolkit
{
    [System.Serializable]
    public class Box : Shape
    {
        public float sizeX = 1;
        public float sizeY = 1;
        public float sizeZ = 1;
        public override bool IsInside(float x, float y, float z)
        {
            float maxX = sizeX / 2;
            float maxY = sizeY / 2;
            float maxZ = sizeZ / 2;
            float minX = -maxX;
            float minY = -maxY;
            float minZ = -maxZ;

            return x <= maxX && y <= maxY && z <= maxZ && x >= minX && y >= minY && z >= minZ;
        }
        public override Vector3 ClosestPointOnSurface(float x, float y, float z)
        {
            float maxX = sizeX / 2;
            float maxY = sizeY / 2;
            float maxZ = sizeZ / 2;
            float minX = -maxX;
            float minY = -maxY;
            float minZ = -maxZ;

            if (IsInside(x, y, z))
            {
                float absX = maxX - Mathf.Abs(x);
                float absY = maxY - Mathf.Abs(y);
                float absZ = maxZ - Mathf.Abs(z);

                if (absX > absY && absX > absZ)
                {
                    x = x < 0 ? minX : maxX;
                }
                else if (absY > absZ)
                {
                    y = y < 0 ? minY : maxY;
                }
                else
                {
                    z = z < 0 ? minZ : maxZ;
                }
            }
            else
            {
                x = Mathf.Clamp(x, minX, maxX);
                y = Mathf.Clamp(y, minY, maxY);
                z = Mathf.Clamp(z, minZ, maxZ);
            }
            return new Vector3(x, y, z);
        }

        public override float DistanceToShape(float x, float y, float z)
        {
            float maxX = sizeX / 2;
            float maxY = sizeY / 2;
            float maxZ = sizeZ / 2;

            float distX = Mathf.Abs(x) - maxX;
            float distY = Mathf.Abs(y) - maxY;
            float distZ = Mathf.Abs(z) - maxZ;

            if (distX < 0 && distY < 0 && distZ < 0)
            {
                return Mathf.Max(distX, Mathf.Max(distY, distZ));
            }
            else
            {
                if (distX < 0) distX = 0;
                if (distY < 0) distY = 0;
                if (distZ < 0) distZ = 0;
                return Mathf.Sqrt(distX * distX + distY * distY + distZ * distZ);
            }

        }
    }
}