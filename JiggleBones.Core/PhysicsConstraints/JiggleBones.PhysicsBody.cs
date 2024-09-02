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
using UnityEngine;

namespace JiggleBones
{
    public class PhysicsBody : MonoBehaviour
    {
        public PointConstraint pointConstraint;
        public GuideObject guideObject;

        // mass
        private float m_mass;
        private float m_inverseMass;
        public float Mass
        {
            get { return m_mass; }
            set
            {
                m_mass = value;
                m_inverseMass = (m_mass == float.MaxValue) ? 0.0f : 1.0f / m_mass;
            }
        }
        public float InverseMass
        {
            get { return m_inverseMass; }
            set
            {
                m_inverseMass = value;
                m_mass = (m_inverseMass == 0.0f) ? float.MaxValue : 1.0f / m_inverseMass;
            }
        }

        // moment of inertia
        private Matrix3x3 m_inertiaLs;
        private Matrix3x3 m_inverseInertiaLs;
        private Matrix3x3 m_inverseInertiaWs;
        public Matrix3x3 InertiaLs
        {
            get { return m_inertiaLs; }
            set
            {
                m_inertiaLs = value;
                m_inverseInertiaLs = m_inertiaLs.Inverted;
            }
        }
        public Matrix3x3 InverseInertiaLs { get { return m_inverseInertiaLs; } }
        public Matrix3x3 InverseInertiaWs { get { return m_inverseInertiaWs; } }
        public void UpdateInertiaWs()
        {
            var t = transform;
            var world2Local =
              Matrix3x3.FromRows
              (
                t.TransformVector(new Vector3(1.0f, 0.0f, 0.0f)),
                t.TransformVector(new Vector3(0.0f, 1.0f, 0.0f)),
                t.TransformVector(new Vector3(0.0f, 0.0f, 1.0f))
              );
            m_inverseInertiaWs = world2Local.Transposed * m_inverseInertiaLs * world2Local;
        }

        // center of mass
        [HideInInspector]
        public Vector3 m_centerOfMassLs;
        [HideInInspector]
        public Vector3 CenterOfMassLs
        {
            get { return m_centerOfMassLs; }
            set { m_centerOfMassLs = value; }
        }
        // velocity
        public Vector3 LinearVelocity;
        public Vector3 AngularVelocity;
        [Range(0.0f, 1.0f)]
        public float LinearDrag = 0.0f;
        [Range(0.0f, 1.0f)]
        public float AngularDrag = 0.0f;

        // gravity
        [Range(0.0f, 1.0f)]
        public float GravityScale = 1.0f;

        // contact
        [Range(0.0f, 1.0f)]
        public float ContactBeta = 0.5f;
        [Range(0.0f, 1.0f)]
        public float Restitution = 0.7f;
        [Range(0.0f, 100.0f)]
        public float Friction = 1.0f;

        // transform
        public bool LockPosition = false;
        public bool LockRotation = false;

        public PhysicsBody()
        {
            Mass = 1.0f;
            InertiaLs = Matrix3x3.Identity;
            CenterOfMassLs = Vector3.zero;
        }

        private void OnEnable()
        {
            World.Register(this);

            UpdateInertiaWs();
        }

        private void OnDisable()
        {
            World.Unregister(this);
        }

        public void Integrate(float dt)
        {
            if (guideObject == null) return;

            if (!LockPosition)
            {
                //guideObject.changeAmount.pos = VectorUtil.Integrate(transform.position, LinearVelocity, dt);

                Vector3 targetPosition = VectorUtil.Integrate(transform.position, LinearVelocity, dt);

                if (pointConstraint.useRadiusLimit)
                {
                    Vector3 clampedPosition = Vector3.ClampMagnitude(targetPosition - pointConstraint.Parent.position, pointConstraint.radiusLimit) + pointConstraint.Parent.position;
                    guideObject.changeAmount.pos = clampedPosition;
                }
                else
                    guideObject.changeAmount.pos = targetPosition;
            }

            if (!LockRotation)
            {
                guideObject.changeAmount.rot = QuaternionUtil.Integrate(transform.rotation, AngularVelocity, dt).eulerAngles;
            }
        }
    }
}

