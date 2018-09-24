﻿/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

using UnityEngine;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Events;
using Opsive.UltimateCharacterController.Game;
using Opsive.UltimateCharacterController.Objects.ItemAssist;
using Opsive.UltimateCharacterController.StateSystem;
using Opsive.UltimateCharacterController.SurfaceSystem;
using Opsive.UltimateCharacterController.Traits;
using Opsive.UltimateCharacterController.Utility;
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER
using Opsive.UltimateCharacterController.Networking.Game;
#endif

namespace Opsive.UltimateCharacterController.Objects
{
    /// <summary>
    /// The Destructible class is an abstract class which acts as the base class for any object that destroys itself and applies a damange.
    /// Primary uses include projectiles and grenades.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public abstract class Destructible : TrajectoryObject
    {
        [Tooltip("Should the projectile be destroyed when it collides with another object?")]
        [SerializeField] protected bool m_DestroyOnCollision = true;
        [Tooltip("The amount of time after a collision that the object should be destroyed.")]
        [SerializeField] protected float m_DestructionDelay;
        [Tooltip("The objects which should spawn when the object is destroyed.")]
        [SerializeField] protected ObjectSpawnInfo[] m_SpawnedObjectsOnDestruction;
        [Tooltip("Unity event invoked when the destructable hits another object.")]
        [SerializeField] protected UnityFloatVector3Vector3GameObjectEvent m_OnImpactEvent;

        public bool DestroyOnCollision { get { return m_DestroyOnCollision; } set { m_DestroyOnCollision = value; } }
        public float DestructionDelay { get { return m_DestructionDelay; } set { m_DestructionDelay = value; } }
        public ObjectSpawnInfo[] SpawnedObjectsOnDestruction { get { return m_SpawnedObjectsOnDestruction; } set { m_SpawnedObjectsOnDestruction = value; } }
        public UnityFloatVector3Vector3GameObjectEvent OnImpactEvent { get { return m_OnImpactEvent; } set { m_OnImpactEvent = value; } }

        private float m_DamageAmount;
        private float m_ImpactForce;
        private int m_ImpactForceFrames;
        private string m_ImpactStateName;
        private float m_ImpactStateDisableTimer;
        private TrailRenderer m_TrailRenderer;
        private ParticleSystem m_ParticleSystem;

        private UltimateCharacterLocomotion m_StickyCharacterLocomotion;

        /// <summary>
        /// Initialize the defualt values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_TrailRenderer = GetComponent<TrailRenderer>();
            if (m_TrailRenderer != null) {
                m_TrailRenderer.enabled = false;
            }
            m_ParticleSystem = GetComponent<ParticleSystem>();
            if (m_ParticleSystem != null) {
                m_ParticleSystem.Stop();
            }

            // The Rigidbody is only used to notify Unity that the object isn't static. The Rigidbody doesn't control any movement.
            var rigidbody = GetComponent<Rigidbody>();
            rigidbody.mass = m_Mass;
            rigidbody.isKinematic = true;
            rigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }

        /// <summary>
        /// Initializes the object. This will be called from an object creating the projectile (such as a weapon).
        /// </summary>
        /// <param name="velocity">The velocity to apply.</param>
        /// <param name="torque">The torque to apply.</param>
        /// <param name="damageAmount">The amount of damage to apply to the hit object.</param>
        /// <param name="impactForce">The amount of force to apply to the hit object.</param>
        /// <param name="impactForceFrames">The number of frames to add the force to.</param>
        /// <param name="impactLayers">The layers that the projectile can impact with.</param>
        /// <param name="impactStateName">The name of the state to activate upon impact.</param>
        /// <param name="impactStateDisableTimer">The number of seconds until the impact state is disabled.</param>
        /// <param name="surfaceImpact">A reference to the Surface Impact triggered when the object hits an object.</param>
        /// <param name="originator">The object that instantiated the trajectory object.</param>
        public virtual void Initialize(Vector3 velocity, Vector3 torque, float damageAmount, float impactForce, int impactForceFrames, LayerMask impactLayers,
                                     string impactStateName, float impactStateDisableTimer, SurfaceImpact surfaceImpact, GameObject originator)
        {
            InitializeDestructibleProperties(damageAmount, impactForce, impactForceFrames, impactLayers, impactStateName, impactStateDisableTimer, surfaceImpact);

            base.Initialize(velocity, torque, originator);
        }

        /// <summary>
        /// Initializes the destructible properties.
        /// </summary>
        /// <param name="damageAmount">The amount of damage to apply to the hit object.</param>
        /// <param name="impactForce">The amount of force to apply to the hit object.</param>
        /// <param name="impactForceFrames">The number of frames to add the force to.</param>
        /// <param name="impactLayers">The layers that the projectile can impact with.</param>
        /// <param name="impactStateName">The name of the state to activate upon impact.</param>
        /// <param name="impactStateDisableTimer">The number of seconds until the impact state is disabled.</param>
        /// <param name="surfaceImpact">A reference to the Surface Impact triggered when the object hits an object.</param>
        public void InitializeDestructibleProperties(float damageAmount, float impactForce, int impactForceFrames, LayerMask impactLayers, string impactStateName, float impactStateDisableTimer, SurfaceImpact surfaceImpact)
        {
            m_DamageAmount = damageAmount;
            m_ImpactForce = impactForce;
            m_ImpactForceFrames = impactForceFrames;
            m_ImpactLayers = impactLayers;
            m_ImpactStateName = impactStateName;
            m_ImpactStateDisableTimer = impactStateDisableTimer;
            // The SurfaceImpact may be set directly on the destructible prefab.
            if (m_SurfaceImpact == null) {
                m_SurfaceImpact = surfaceImpact;
            }
            if (m_TrailRenderer != null) {
                m_TrailRenderer.Clear();
                m_TrailRenderer.enabled = true;
            }
            if (m_ParticleSystem != null) {
                m_ParticleSystem.Play();
            }
            if (m_Collider != null) {
                m_Collider.enabled = false;
            }
            // The object may be reused and was previously stuck to a character.
            if (m_StickyCharacterLocomotion != null) {
                m_StickyCharacterLocomotion.RemoveSubCollider(m_Collider);
                m_StickyCharacterLocomotion = null;
            }
        }

        /// <summary>
        /// The object has collided with another object.
        /// </summary>
        /// <param name="hit">The RaycastHit of the object. Can be null.</param>
        protected override void OnCollision(RaycastHit? hit)
        {
            base.OnCollision(hit);

            if (m_BounceMode == BounceMode.None) {
                // When there is a collision the object should move to the position that was hit so if it's not destroyed then it looks like it
                // is penetrating the hit object.
                if (hit != null && hit.HasValue && m_Collider != null) {
                    var closestPoint = m_Collider.ClosestPoint(hit.Value.point);
                    m_Transform.position += (hit.Value.point - closestPoint);
                    // Only set the parent to the hit transform on uniform objects to prevent stretching.
                    if (MathUtility.IsUniform(hit.Value.transform.localScale)) {
                        m_Transform.parent = hit.Value.transform;

                        // If the destructible sticks to a character then the object should be added as a sub collider so collisions will be ignored.
                        m_StickyCharacterLocomotion = hit.Value.transform.gameObject.GetCachedComponent<UltimateCharacterLocomotion>();
                        if (m_StickyCharacterLocomotion != null) {
                            m_StickyCharacterLocomotion.AddSubCollider(m_Collider);
                        }
                    }
                }
            }

            if (m_TrailRenderer != null) {
                m_TrailRenderer.enabled = false;
            }
            if (m_ParticleSystem != null) {
                m_ParticleSystem.Stop();
            }

            // The object may not have been initialized before it collides.
            if (m_GameObject == null) {
                InitializeComponentReferences();
            }

            if (hit != null && hit.HasValue) {
                var hitValue = hit.Value;
                var hitGameObject = hitValue.collider.gameObject;
                // The shield can absorb some (or none) of the damage from the destructible.
                var damageAmount = m_DamageAmount;
#if ULTIMATE_CHARACTER_CONTROLLER_MELEE
                ShieldCollider shieldCollider;
                if ((shieldCollider = hitGameObject.GetCachedComponent<ShieldCollider>()) != null) {
                    damageAmount = shieldCollider.Shield.Damage(damageAmount, false);
                }
#endif

                // Allow a custom event to be received.
                EventHandler.ExecuteEvent(hitGameObject, "OnObjectImpact", damageAmount, hitValue.point, hitValue.normal * m_ImpactForce, m_Originator, hitValue.collider);
                if (m_OnImpactEvent != null && m_OnImpactEvent.GetPersistentEventCount() > 0) {
                    m_OnImpactEvent.Invoke(damageAmount, hitValue.point, hitValue.normal * m_ImpactForce, m_Originator);
                }

                // If the shield didn't absorb all of the damage then it should be applied to the character.
                if (damageAmount > 0) {
                    // If the Health component exists it will apply a force to the rigidbody in addition to deducting the health. Otherwise just apply the force to the rigidbody. 
                    Health hitHealth;
                    if ((hitHealth = hitGameObject.GetCachedParentComponent<Health>()) != null) {
                        hitHealth.Damage(damageAmount, hitValue.point, -hitValue.normal, m_ImpactForce, m_ImpactForceFrames, 0, m_Originator, hitValue.collider);
                    } else if (m_ImpactForce > 0) {
                        var collisionRigidbody = hitGameObject.GetCachedParentComponent<Rigidbody>();
                        if (collisionRigidbody != null && !collisionRigidbody.isKinematic) {
                            collisionRigidbody.AddForceAtPosition(-hitValue.normal * m_ImpactForce * MathUtility.RigidbodyForceMultiplier, hitValue.point);
                        } else {
                            var forceObject = hitGameObject.GetCachedParentComponent<IForceObject>();
                            if (forceObject != null) {
                                forceObject.AddForce(m_Transform.forward * m_ImpactForce);
                            }
                        }
                    }
                }

                // An optional state can be activated on the hit object.
                if (!string.IsNullOrEmpty(m_ImpactStateName)) {
                    StateManager.SetState(hitGameObject, m_ImpactStateName, true);
                    // If the timer isn't -1 then the state should be disabled after a specified amount of time. If it is -1 then the state
                    // will have to be disabled manually.
                    if (m_ImpactStateDisableTimer != -1) {
                        StateManager.DeactivateStateTimer(hitGameObject, m_ImpactStateName, m_ImpactStateDisableTimer);
                    }
                }
            }

            // The object can destroy itself after a small delay.
            if (m_DestroyOnCollision) {
                Scheduler.ScheduleFixed(m_DestructionDelay, Destruct, hit);
            }
        }

        /// <summary>
        /// The object should be destroyed.
        /// </summary>
        /// <param name="hit">The RaycastHit of the object. Can be null.</param>
        protected void Destruct(RaycastHit? hit)
        {
            // The RaycastHit will be null if the destruction happens with no collision.
            var hitPosition = (hit != null && hit.HasValue) ? hit.Value.point : m_Transform.position;
            var hitNormal = (hit != null && hit.HasValue) ? hit.Value.normal : m_Transform.up;
            for (int i = 0; i < m_SpawnedObjectsOnDestruction.Length; ++i) {
                if (m_SpawnedObjectsOnDestruction[i] == null) {
                    continue;
                }

                var spawnedObject = m_SpawnedObjectsOnDestruction[i].Instantiate(hitPosition, hitNormal, m_NormalizedGravity);
                if (spawnedObject == null) {
                    continue;
                }
                var explosion = spawnedObject.GetCachedComponent<Explosion>();
                if (explosion != null) {
                    explosion.Explode(m_DamageAmount, m_ImpactForce, m_ImpactForceFrames, m_Originator);
                }
            }

            // The component and collider no longer need to be enabled after the object has been destroyed.
            if (m_Collider != null) {
                m_Collider.enabled = false;
            }
            enabled = false;

            // The destructible should be destroyed.
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER
            if (NetworkObjectPool.IsNetworkActive()) {
                NetworkObjectPool.Destroy(m_GameObject);
            } else {
#endif
                ObjectPool.Destroy(m_GameObject);
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER
            }
#endif
        }
    }
}