using System;
using UnityEngine;

namespace TF_Toolkit
{
    [System.Serializable]
    public abstract class Shape
    {
        public bool IsInside(Vector3 point)
        {
            return this.IsInside(point.x, point.y, point.z);
        }
        public bool IsInside(ref Vector3 point)
        {
            return this.IsInside(point.x, point.y, point.z);
        }
        public virtual bool IsInside(float x, float y, float z)
        {
            throw new System.NotImplementedException();
        }
        public Vector3 ClosestPointOnSurface(Vector3 point)
        {
            return this.ClosestPointOnSurface(point.x, point.y, point.z);
        }
        public Vector3 ClosestPointOnSurface(ref Vector3 point)
        {
            return this.ClosestPointOnSurface(point.x, point.y, point.z);
        }
        public virtual Vector3 ClosestPointOnSurface(float x, float y, float z)
        {
            throw new System.NotImplementedException();
        }
        public float DistanceToShape(Vector3 point)
        {
            return this.DistanceToShape(point.x, point.y, point.z);
        }
        public float DistanceToShape(ref Vector3 point)
        {
            return this.DistanceToShape(point.x, point.y, point.z);
        }
        public virtual float DistanceToShape(float x, float y, float z)
        {
            bool isInside = this.IsInside(x, y, z);
            Vector3 closestPointOnSurface = this.ClosestPointOnSurface(x, y, z);
            float distX = x - closestPointOnSurface.x;
            float distY = y - closestPointOnSurface.y;
            float distZ = z - closestPointOnSurface.z;
            return (isInside ? -1 : 1) * Mathf.Sqrt(distX * distX + distY * distY + distZ * distZ);
        }
    }
}