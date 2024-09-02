﻿/******************************************************************************/
/*
  Project - Physics Constraints
            https://github.com/TheAllenChou/unity-physics-constraints
  
  Author  - Ming-Lun "Allen" Chou
  Web     - http://AllenChou.net
  Twitter - @TheAllenChou
*/
/******************************************************************************/

using UnityEngine;

namespace JiggleBones
{
    public class ConstraintUtil
    {
        // h: delta time
        // d: damping coefficient
        // k: spring constant
        // omega: angular velocity
        // zeta: damping ratio
        // gamma: softness (force feedback factor)
        // beta: Baumgarte (positional error feedback factor)

        internal static readonly float Epsilon = 1.0e-10f;
        internal static readonly float TwoPi = 2.0f * Mathf.PI;

        private static void VelocityConstraintBias(float dampingCoefficient, float springConstant, float dt, out float positionBiasCoefficient, out float softnessBiasCoefficient)
        {
            float hk = dt * springConstant;
            float gamma = dampingCoefficient + hk;
            if (gamma > 0.0f)
                gamma = 1.0f / gamma;

            float dtInv = 1.0f / dt;
            float beta = hk * gamma;

            positionBiasCoefficient = beta * dtInv;
            softnessBiasCoefficient = gamma * dtInv;
        }

        public static void VelocityConstraintBias(float mass, ConstraintParams p, float dt, out float positionBiasCoefficient, out float softnessBiasCoefficient)
        {
            if (p.Mode == ConstraintParams.ParameterMode.Hard)
            {
                positionBiasCoefficient = 1.0f / dt;
                softnessBiasCoefficient = 0.0f;
                return;
            }

            float dampingCoefficient;
            float springConstant;

            p.GenerateVelocityConstraintParams(mass, out dampingCoefficient, out springConstant);

            VelocityConstraintBias(dampingCoefficient, springConstant, dt, out positionBiasCoefficient, out softnessBiasCoefficient);
        }
    }
}

