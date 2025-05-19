using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TF_Toolkit
{
    [System.Serializable]
    public class Cube : Shape
    {
        public float size = 1;
        public override bool IsInside(float x, float y, float z)
        {
            float max = size / 2;
            float min = -max;

            return x <= max && y <= max && z <= max && x >= min && y >= min && z >= min;
        }
        public override Vector3 ClosestPointOnSurface(float x, float y, float z)
        {
            float max = size / 2;
            float min = -max;

            if (IsInside(x, y, z))
            {
                float absX = Mathf.Abs(x);
                float absY = Mathf.Abs(y);
                float absZ = Mathf.Abs(z);

                if (absX > absY && absX > absZ)
                {
                    x = x < 0 ? min : max;
                }
                else if (absY > absZ)
                {
                    y = y < 0 ? min : max;
                }
                else
                {
                    z = z < 0 ? min : max;
                }
            }
            else
            {
                x = Mathf.Clamp(x, min, max);
                y = Mathf.Clamp(y, min, max);
                z = Mathf.Clamp(z, min, max);
            }
            return new Vector3(x, y, z);
        }

        public override float DistanceToShape(float x, float y, float z)
        {
            float max = size / 2;

            float absX = Mathf.Abs(x);
            float absY = Mathf.Abs(y);
            float absZ = Mathf.Abs(z);

            if (absX <= max && absY <= max && absZ <= max)
            {
                return Mathf.Max(absX, Mathf.Max(absY, absZ)) - max;
            }
            else
            {
                float distX = Mathf.Max(absX - max, 0);
                float distY = Mathf.Max(absY - max, 0);
                float distZ = Mathf.Max(absZ - max, 0);
                return Mathf.Sqrt(distX * distX + distY * distY + distZ * distZ);
            }
        
        }
    }
}