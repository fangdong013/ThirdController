/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

using UnityEngine;

namespace Opsive.UltimateCharacterController.Items.Actions
{
    /// <summary>
    /// The Shield will absorb damage applied to the character. It has its own strength factor so when too much damage has been taken it will no longer be effective.
    /// </summary>
    public class Shield : ItemAction
    {
        [Tooltip("Determines how much damage the shield absorbs. A value of 1 will absorb all of the damage, a value of 0 will absorb none of the damage.")]
        [Range(0, 1)] [SerializeField] protected float m_AbsorptionFactor = 1;
        [Tooltip("Is the shield invincible?")]
        [SerializeField] protected bool m_Invincible;
        [Tooltip("The strength of the shield. A value of 0 means the shield doesn't absorb any damage.")]
        [SerializeField] protected float m_Strength = 100;
        [Tooltip("Should the shield absorb damage caused by explosions?")]
        [SerializeField] protected bool m_AbsorbExplosions;

        public float AbsorptionFactor { get { return m_AbsorptionFactor; } set { m_AbsorptionFactor = value; } }
        public bool Invincible { get { return m_Invincible; } set { m_Invincible = value; } }
        public float Strength { get { return m_Strength; } set { m_Strength = value; } }
        public bool AbsorbExplosions { get { return m_AbsorbExplosions; } set { m_AbsorbExplosions = value; } }

        private float m_CurrentStrength;
        public float CurrentStrength { get { return m_CurrentStrength; } }

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_CurrentStrength = m_Strength;
        }

        /// <summary>
        /// Damages the shield.
        /// </summary>
        /// <param name="amount">The amount of damage to apply/</param>
        /// <param name="explosion">Does the damage occur from an explosion?</param>
        /// <returns>The amount of damage remaining which should be applied to the character.</returns>
        public float Damage(float amount, bool explosion)
        {
            // The shield may not be able to absorb damage caused by explosions.
            if (explosion && !m_AbsorbExplosions) {
                return amount;
            }

            // If the shield is invincible then no damage is applied to it and the resulting absorption factor should be returned.
            if (m_Invincible) {
                return amount * (1 - m_AbsorptionFactor);
            }

            // If the shield's strength is depleted then the entire damage amount should be applied to the character.
            if (m_CurrentStrength == 0) {
                return amount;
            }

            // Damage the shield and amount of damage which be applied to the character.
            var damageAmount = Mathf.Min(amount * m_AbsorptionFactor, m_CurrentStrength);
            m_CurrentStrength -= damageAmount;

            return amount - damageAmount;
        }
    }
}