/******************************************************************************/
/*
  Project - Physics Constraints
            https://github.com/TheAllenChou/unity-physics-constraints
  
  Author  - Ming-Lun "Allen" Chou
  Web     - http://AllenChou.net
  Twitter - @TheAllenChou
*/
/******************************************************************************/

using System.Collections.Generic;
using UnityEngine;

namespace JiggleBones
{
    public class World : MonoBehaviour
    {
        private static int s_velocityIterations = 10;
        public static int VelocityIterations
        {
            get { return s_velocityIterations; }
            set { s_velocityIterations = Mathf.Max(1, value); }
        }

        public static Vector3 Gravity = Vector3.zero;

        // constraints
        private static ICollection<IPhysicsConstraint> s_constraints;
        public static void Register(IPhysicsConstraint c)
        {
            ValidateWorld();
            s_constraints.Add(c);
        }
        public static void Unregister(IPhysicsConstraint c)
        {
            if (s_constraints == null)
                return;

            s_constraints.Remove(c);
        }

        // physics bodies
        private static ICollection<PhysicsBody> s_bodies;
        public static void Register(PhysicsBody b)
        {
            ValidateWorld();
            s_bodies.Add(b);
        }
        public static void Unregister(PhysicsBody b)
        {
            if (s_bodies == null)
                return;

            s_bodies.Remove(b);
        }

        private static GameObject s_world;
        private static void ValidateWorld()
        {

            if (s_world != null)
                return;

            s_constraints = new HashSet<IPhysicsConstraint>();
            s_bodies = new HashSet<PhysicsBody>();

            s_world = new GameObject("World (Physics Constraints)");
            s_world.AddComponent<World>();
        }

        private void FixedUpdate()
        {
            if (s_world != gameObject)
            {
                Destroy(gameObject);
                return;
            }

            //float dt = Mathf.Max(ConstraintUtil.Epsilon, Time.fixedDeltaTime);
            Step(Time.fixedDeltaTime);
        }

        public static void Step(float dt)
        {/*
            // inertia
            foreach (var body in s_bodies)
            {
                body.UpdateInertiaWs();
            }

            // gravity
            {
                Vector3 gravityImpulse = Gravity * dt;
                foreach (var body in s_bodies)
                {
                    body.LinearVelocity += gravityImpulse * body.GravityScale;
                }
            }
            */
            // init constraints
            foreach (var contact in s_constraints)
            {
                contact.InitVelocityConstraint(dt);
            }


            // solve constraints
            for (int i = 0; i < s_velocityIterations; ++i)
            {
                foreach (var constraint in s_constraints)
                {
                    constraint.SolveVelocityConstraint(dt);
                }
            }

            // integrate
            foreach (var body in s_bodies)
            {
                body.Integrate(dt);
            }

            // drag
            foreach (var body in s_bodies)
            {
                body.LinearVelocity *= Mathf.Pow(1.0f - body.LinearDrag, dt);
                body.AngularVelocity *= Mathf.Pow(1.0f - body.AngularDrag, dt);
            }

            // dbc collision
            foreach (var body in s_bodies)
            {
                foreach (var pair in body.pointConstraint.colliders)
                {
                    DynamicBoneCollider dynamicBoneCollider = pair.Value;
                    if (dynamicBoneCollider != null && dynamicBoneCollider.enabled)
                    {
                        Vector3 position = body.pointConstraint.transform.position;
                        dynamicBoneCollider.Collide(ref position, body.pointConstraint.colliderRadius);
                        body.pointConstraint.transform.position = position;
                    }
                }
            }
        }
    }
}
