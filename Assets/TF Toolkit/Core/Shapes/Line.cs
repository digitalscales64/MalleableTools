using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TF_Toolkit
{
    [System.Serializable]
    public class Line : Shape
    {
        public Vector3 start;
        public Vector3 end;

        public bool clampToStart = true;
        public bool clampToEnd = true;

        public override bool IsInside(float x, float y, float z)
        {
            return false;
        }
        public override Vector3 ClosestPointOnSurface(float x, float y, float z)
        {
            float lineDirX = end.x - start.x;
            float lineDirY = end.y - start.y;
            float lineDirZ = end.z - start.z;
            float lineDirSquared = lineDirX * lineDirX + lineDirY * lineDirY + lineDirZ * lineDirZ;

            if (lineDirSquared == 0)
            {
                return start;
            }

            float pointDirX = x - start.x;
            float pointDirY = y - start.y;
            float pointDirZ = z - start.z;
            float dotProduct = pointDirX * lineDirX + lineDirY * pointDirY + lineDirZ * pointDirZ;

            float t = dotProduct / lineDirSquared;

            if (clampToStart && t < 0) t = 0;
            if (clampToEnd && t > 1) t = 1;

            return new Vector3(
                start.x + lineDirX * t,
                start.y + lineDirY * t,
                start.z + lineDirZ * t
            );
        }

        public override float DistanceToShape(float x, float y, float z)
        {
            float lineDirX = end.x - start.x;
            float lineDirY = end.y - start.y;
            float lineDirZ = end.z - start.z;
            float lineDirSquared = lineDirX * lineDirX + lineDirY * lineDirY + lineDirZ * lineDirZ;

            float pointDirX = x - start.x;
            float pointDirY = y - start.y;
            float pointDirZ = z - start.z;

            if (lineDirSquared == 0)
            {
                return Mathf.Sqrt(pointDirX * pointDirX + pointDirY * pointDirY + pointDirZ * pointDirZ);
            }

            float dotProduct = pointDirX * lineDirX + lineDirY * pointDirY + lineDirZ * pointDirZ;

            float t = dotProduct / lineDirSquared;
            if (lineDirSquared == 0) t = 0;
 
            if (clampToStart && t < 0) t = 0;
            if (clampToEnd && t > 1) t = 1;

            float distX = lineDirX * t - pointDirX;
            float distY = lineDirY * t - pointDirY;
            float distZ = lineDirZ * t - pointDirZ;

            return Mathf.Sqrt(distX * distX + distY * distY + distZ * distZ);
        }
    }
}