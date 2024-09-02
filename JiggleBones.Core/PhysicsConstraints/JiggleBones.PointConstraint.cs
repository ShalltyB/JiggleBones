/******************************************************************************/
/*
  Project - Physics Constraints
            https://github.com/TheAllenChou/unity-physics-constraints
  
  Author  - Ming-Lun "Allen" Chou
  Web     - http://AllenChou.net
  Twitter - @TheAllenChou
*/
/******************************************************************************/

using Studio;
using System.Collections.Generic;
using UnityEngine;

namespace JiggleBones
{
    [RequireComponent(typeof(PhysicsBody))]
    public class PointConstraint : PointConstraintBase
    {
        public Transform Parent;
        public Vector3 Offset;

        public bool useRadiusLimit = false;
        public float radiusLimit = 0.02f;

        public float colliderRadius = 0.05f;
        public List<KeyValuePair<ObjectCtrlInfo, DynamicBoneCollider>> colliders = new List<KeyValuePair<ObjectCtrlInfo, DynamicBoneCollider>>();

        protected override Vector3 GetTarget()
        {
            return (Parent != null) ? Parent.position : transform.position;
        }

        protected override Vector3 GetLocalAnchor()
        {
            return Offset;
        }
    }
}
