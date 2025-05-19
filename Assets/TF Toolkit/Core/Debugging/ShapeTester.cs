using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TF_Toolkit
{
    public class ShapeTester : MonoBehaviour
    {
        

        public Transform start;
        public Transform end;
        public Transform point;
        public Transform result;

        public bool clampStart = true;
        public bool clampEnd = true;


        public Transform a;
        public Transform b;
        public Transform c;


        void Update()
        {
            //Line line = new Line();
            //line.start = start.position;
            //line.end = end.position;
            //line.clampToStart = clampStart;
            //line.clampToEnd = clampEnd;
    

            //Triangle triangle = new Triangle();
            //triangle.a = a.position;
            //triangle.b = b.position;
            //triangle.c = c.position;
      

            Plane shape = new Plane();
            shape.point = a.position;
            shape.normal = b.position - a.position;



            result.position = shape.ClosestPointOnSurface(point.position);
        }
    }
}
