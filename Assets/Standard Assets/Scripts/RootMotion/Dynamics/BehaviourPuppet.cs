using System;
using UnityEngine;

namespace RootMotion.Dynamics
{
	[HelpURL("http://root-motion.com/puppetmasterdox/html/page10.html")]
	[AddComponentMenu("Scripts/RootMotion.Dynamics/PuppetMaster/Behaviours/BehaviourPuppet")]
	public class BehaviourPuppet : BehaviourBase
	{
		[Serializable]
		public enum State
		{
			Puppet,
			Unpinned,
			GetUp
		}

		[Serializable]
		public enum NormalMode
		{
			Active,
			Unmapped,
			Kinematic
		}

		[Serializable]
		public class MasterProps
		{
			public NormalMode normalMode;

			public float mappingBlendSpeed = 10f;

			public bool activateOnStaticCollisions;

			public float activateOnImpulse;
		}

		[Serializable]
		public struct MuscleProps
		{
			[Tooltip("How much will collisions with muscles of this group unpin parent muscles?")]
			[Range(0f, 1f)]
			public float unpinParents;

			[Tooltip("How much will collisions with muscles of this group unpin child muscles?")]
			[Range(0f, 1f)]
			public float unpinChildren;

			[Tooltip("How much will collisions with muscles of this group unpin muscles of the same group?")]
			[Range(0f, 1f)]
			public float unpinGroup;

			[Tooltip("If 1, muscles of this group will always be mapped to the ragdoll.")]
			[Range(0f, 1f)]
			public float minMappingWeight;

			[Tooltip("If 0, muscles of this group will not be mapped to the ragdoll pose even if they are unpinned.")]
			[Range(0f, 1f)]
			public float maxMappingWeight;

			[Tooltip("If true, muscles of this group will have their colliders disabled while in puppet state (not unbalanced nor getting up).")]
			public bool disableColliders;

			[Tooltip("How fast will muscles of this group regain their pin weight (multiplier)?")]
			public float regainPinSpeed;

			[Tooltip("Smaller value means more unpinning from collisions (multiplier).")]
			public float collisionResistance;

			[Tooltip("If the distance from the muscle to it's target is larger than this value, the character will be knocked out.")]
			public float knockOutDistance;

			[Tooltip("The PhysicsMaterial applied to the muscles while the character is in Puppet or GetUp state. Using a lower friction material reduces the risk of muscles getting stuck and pulled out of their joints.")]
			public PhysicMaterial puppetMaterial;

			[Tooltip("The PhysicsMaterial applied to the muscles while the character is in Unpinned state.")]
			public PhysicMaterial unpinnedMaterial;
		}

		[Serializable]
		public struct MusclePropsGroup
		{
			[HideInInspector]
			public string name;

			[Tooltip("Muscle groups to which those properties apply.")]
			public Muscle.Group[] groups;

			[Tooltip("The muscle properties for those muscle groups.")]
			public MuscleProps props;
		}

		[Serializable]
		public struct CollisionResistanceMultiplier
		{
			public LayerMask layers;

			public float multiplier;
		}

		public delegate void CollisionImpulseDelegate(MuscleCollision m, float impulse);

		[LargeHeader("Collision And Recovery")]
		public MasterProps masterProps = new MasterProps();

		[Tooltip("Will ground the target to those layers when getting up.")]
		public LayerMask groundLayers;

		[Tooltip("Will unpin the muscles that collide with those layers.")]
		public LayerMask collisionLayers;

		[Tooltip("The collision impulse sqrMagnitude threshold under which collisions will be ignored.")]
		public float collisionThreshold;

		public Weight collisionResistance = new Weight(3f, "Smaller value means more unpinning from collisions so the characters get knocked out more easily. If using a curve, the value will be evaluated by each muscle's target velocity magnitude. This can be used to make collision resistance higher while the character moves or animates faster.");

		[Tooltip("Multiplies collision resistance for the specified layers.")]
		public CollisionResistanceMultiplier[] collisionResistanceMultipliers;

		[Tooltip("An optimisation. Will only process up to this number of collisions per physics step.")]
		[Range(1f, 30f)]
		public int maxCollisions = 30;

		[Tooltip("How fast will the muscles of this group regain their pin weight?")]
		[Range(0.001f, 10f)]
		public float regainPinSpeed = 1f;

		[Tooltip("'Boosting' is a term used for making muscles temporarily immune to collisions and/or deal more damage to the muscles of other characters. That is done by increasing Muscle.State.immunity and Muscle.State.impulseMlp. For example when you set muscle.state.immunity to 1, boostFalloff will determine how fast this value will fall back to normal (0). Use BehaviourPuppet.BoostImmunity() and BehaviourPuppet.BoostImpulseMlp() for boosting from your own scripts. It is helpful for making the puppet stronger and deliever more punch while playing a melee hitting/kicking animation.")]
		public float boostFalloff = 1f;

		[LargeHeader("Muscle Group Properties")]
		[Tooltip("The default muscle properties. If there are no 'Group Overrides', this will be used for all muscles.")]
		public MuscleProps defaults;

		[Tooltip("Overriding default muscle properties for some muscle groups (for example making the feet stiffer or the hands looser).")]
		public MusclePropsGroup[] groupOverrides;

		[LargeHeader("Losing Balance")]
		[Tooltip("If the distance from the muscle to it's target is larger than this value, the character will be knocked out.")]
		[Range(0.001f, 10f)]
		public float knockOutDistance = 1f;

		[Tooltip("Smaller value makes the muscles weaker when the puppet is knocked out.")]
		[Range(0f, 1f)]
		public float unpinnedMuscleWeightMlp = 0.3f;

		[Tooltip("If true, all muscles of the 'Prop' group will be detached from the puppet when it loses balance.")]
		public bool dropProps;

		[LargeHeader("Getting Up")]
		[Tooltip("If true, GetUp state will be triggerred automatically after 'Get Up Delay' and when the velocity of the hip muscle is less than 'Max Get Up Velocity'.")]
		public bool canGetUp = true;

		[Tooltip("Minimum delay for getting up after loosing balance. After that time has passed, will wait for the velocity of the hip muscle to come down below 'Max Get Up Velocity' and then switch to the GetUp state.")]
		public float getUpDelay = 5f;

		[Tooltip("The duration of blending the animation target from the ragdoll pose to the getting up animation once the GetUp state has been triggered.")]
		public float blendToAnimationTime = 0.2f;

		[Tooltip("Will not get up before the velocity of the hip muscle has come down to this value.")]
		public float maxGetUpVelocity = 0.3f;

		[Tooltip("Will not get up before this amount of time has passed since loosing balance.")]
		public float minGetUpDuration = 1f;

		[Tooltip("Collision resistance multiplier while in the GetUp state. Increasing this will prevent the character from loosing balance again immediatelly after going from Unpinned to GetUp state.")]
		public float getUpCollisionResistanceMlp = 2f;

		[Tooltip("Regain pin weight speed multiplier while in the GetUp state. Increasing this will prevent the character from loosing balance again immediatelly after going from Unpinned to GetUp state.")]
		public float getUpRegainPinSpeedMlp = 2f;

		[Tooltip("Knock out distance multiplier while in the GetUp state. Increasing this will prevent the character from loosing balance again immediatelly after going from Unpinned to GetUp state.")]
		public float getUpKnockOutDistanceMlp = 10f;

		[Tooltip("Offset of the target character (in character rotation space) from the hip bone when initiating getting up animation from a prone pose. Tweak this value if your character slides a bit when starting to get up.")]
		public Vector3 getUpOffsetProne;

		[Tooltip("Offset of the target character (in character rotation space) from the hip bone when initiating getting up animation from a supine pose. Tweak this value if your character slides a bit when starting to get up.")]
		public Vector3 getUpOffsetSupine;

		[LargeHeader("Events")]
		[Tooltip("Called when the character starts getting up from a prone pose (facing down).")]
		public PuppetEvent onGetUpProne;

		[Tooltip("Called when the character starts getting up from a supine pose (facing up).")]
		public PuppetEvent onGetUpSupine;

		[Tooltip("Called when the character is knocked out (loses balance). Doesn't matter from which state.")]
		public PuppetEvent onLoseBalance;

		[Tooltip("Called when the character is knocked out (loses balance) only from the normal Puppet state.")]
		public PuppetEvent onLoseBalanceFromPuppet;

		[Tooltip("Called when the character is knocked out (loses balance) only from the GetUp state.")]
		public PuppetEvent onLoseBalanceFromGetUp;

		[Tooltip("Called when the character has fully recovered and switched to the Puppet state.")]
		public PuppetEvent onRegainBalance;

		public CollisionImpulseDelegate OnCollisionImpulse;

		private float unpinnedTimer;

		private float getUpTimer;

		private Vector3 hipsForward;

		private Vector3 hipsUp;

		private float getupAnimationBlendWeight;

		private float getupAnimationBlendWeightV;

		private bool getUpTargetFixed;

		private NormalMode lastNormalMode;

		private int collisions;

		private bool eventsEnabled;

		private float lastKnockOutDistance;

		private float knockOutDistanceSqr;

		private bool getupDisabled;

		private MuscleCollisionBroadcaster broadcaster;

		private Vector3 getUpPosition;

		public State state
		{
			get;
			private set;
		}

		[ContextMenu("User Manual")]
		private void OpenUserManual()
		{
			Application.OpenURL("http://root-motion.com/puppetmasterdox/html/page10.html");
		}

		[ContextMenu("Scrpt Reference")]
		private void OpenScriptReference()
		{
			Application.OpenURL("http://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_behaviour_puppet.html");
		}

		public override void OnReactivate()
		{
			state = ((base.puppetMaster.state != 0) ? State.Unpinned : State.Puppet);
			getUpTimer = 0f;
			unpinnedTimer = 0f;
			getupAnimationBlendWeight = 0f;
			getupAnimationBlendWeightV = 0f;
			getUpTargetFixed = false;
			getupDisabled = (base.puppetMaster.state != PuppetMaster.State.Alive);
			state = ((base.puppetMaster.state != 0) ? State.Unpinned : State.Puppet);
			Muscle[] muscles = base.puppetMaster.muscles;
			foreach (Muscle m in muscles)
			{
				SetColliders(m, state == State.Unpinned);
			}
			Activate();
		}

		public void Reset(Vector3 position, Quaternion rotation)
		{
			if (Application.isPlaying)
			{
				base.puppetMaster.targetRoot.position = position;
				base.puppetMaster.targetRoot.rotation = rotation;
				Muscle[] muscles = base.puppetMaster.muscles;
				foreach (Muscle muscle in muscles)
				{
					muscle.transform.position = muscle.target.position;
					muscle.transform.rotation = muscle.target.rotation * muscle.targetRotationRelative;
				}
				base.puppetMaster.StoreTargetMappedState();
				base.puppetMaster.SampleTargetMappedState();
				Muscle[] muscles2 = base.puppetMaster.muscles;
				foreach (Muscle muscle2 in muscles2)
				{
					muscle2.Read();
				}
				state = ((base.puppetMaster.state != 0) ? State.Unpinned : State.Puppet);
				getUpTimer = 0f;
				unpinnedTimer = 0f;
				getupAnimationBlendWeight = 0f;
				getupAnimationBlendWeightV = 0f;
				getUpTargetFixed = false;
				Muscle[] muscles3 = base.puppetMaster.muscles;
				foreach (Muscle muscle3 in muscles3)
				{
					muscle3.state.pinWeightMlp = ((base.puppetMaster.state != 0) ? 0f : 1f);
					muscle3.state.muscleWeightMlp = ((base.puppetMaster.state != 0) ? base.puppetMaster.stateSettings.deadMuscleWeight : 1f);
					muscle3.state.muscleDamperAdd = ((base.puppetMaster.state != 0) ? base.puppetMaster.stateSettings.deadMuscleDamper : 0f);
					SetColliders(muscle3, state == State.Unpinned);
				}
			}
		}

		protected override void OnInitiate()
		{
			CollisionResistanceMultiplier[] array = collisionResistanceMultipliers;
			for (int i = 0; i < array.Length; i++)
			{
				CollisionResistanceMultiplier collisionResistanceMultiplier = array[i];
				if ((int)collisionResistanceMultiplier.layers == 0)
				{
					UnityEngine.Debug.LogWarning("BehaviourPuppet has a Collision Resistance Multiplier that's layers is set to Nothing. Please add some layers.", base.transform);
				}
			}
			Muscle[] muscles = base.puppetMaster.muscles;
			foreach (Muscle muscle in muscles)
			{
				if (muscle.joint.gameObject.layer == base.puppetMaster.targetRoot.gameObject.layer)
				{
					UnityEngine.Debug.LogWarning("One of the ragdoll bones is on the same layer as the animated character. This might make the ragdoll collide with the character controller.");
				}
				if (!Physics.GetIgnoreLayerCollision(muscle.joint.gameObject.layer, base.puppetMaster.targetRoot.gameObject.layer))
				{
					UnityEngine.Debug.LogWarning("The ragdoll layer (" + muscle.joint.gameObject.layer + ") and the character controller layer (" + base.puppetMaster.targetRoot.gameObject.layer + ") are not set to ignore each other in Edit/Project Settings/Physics/Layer Collision Matrix. This might cause the ragdoll bones to collide with the character controller.");
				}
			}
			hipsForward = Quaternion.Inverse(base.puppetMaster.muscles[0].transform.rotation) * base.puppetMaster.targetRoot.forward;
			hipsUp = Quaternion.Inverse(base.puppetMaster.muscles[0].transform.rotation) * base.puppetMaster.targetRoot.up;
			state = State.Unpinned;
			SetState(State.Puppet);
			eventsEnabled = true;
			if (base.enabled)
			{
				Activate();
			}
		}

		public override void KillStart()
		{
			getupDisabled = true;
			Muscle[] muscles = base.puppetMaster.muscles;
			foreach (Muscle muscle in muscles)
			{
				muscle.state.pinWeightMlp = 0f;
				muscle.state.immunity = 0f;
				SetColliders(muscle, unpinned: true);
			}
		}

		public override void KillEnd()
		{
			SetState(State.Unpinned);
		}

		public override void Resurrect()
		{
			getupDisabled = false;
			state = State.Unpinned;
			getUpTimer = float.PositiveInfinity;
			unpinnedTimer = float.PositiveInfinity;
			getupAnimationBlendWeight = 0f;
			getupAnimationBlendWeightV = 0f;
			Muscle[] muscles = base.puppetMaster.muscles;
			foreach (Muscle muscle in muscles)
			{
				muscle.state.pinWeightMlp = 0f;
			}
		}

		private void OnDisable()
		{
			state = State.Unpinned;
		}

		protected override void OnFixedUpdate()
		{
			collisions = 0;
			if (!base.puppetMaster.isActive)
			{
				SetState(State.Puppet);
				return;
			}
			if (!base.puppetMaster.isAlive)
			{
				Muscle[] muscles = base.puppetMaster.muscles;
				foreach (Muscle muscle in muscles)
				{
					muscle.state.pinWeightMlp = 0f;
					muscle.state.mappingWeightMlp = Mathf.MoveTowards(muscle.state.mappingWeightMlp, 1f, Time.deltaTime * 5f);
				}
				return;
			}
			Muscle[] muscles2 = base.puppetMaster.muscles;
			foreach (Muscle muscle2 in muscles2)
			{
				muscle2.state.immunity = Mathf.MoveTowards(muscle2.state.immunity, 0f, Time.deltaTime * boostFalloff);
				muscle2.state.impulseMlp = Mathf.Lerp(muscle2.state.impulseMlp, 1f, Time.deltaTime * boostFalloff);
			}
			if (state == State.Unpinned)
			{
				unpinnedTimer += Time.deltaTime;
				if (unpinnedTimer >= getUpDelay && canGetUp && !getupDisabled && base.puppetMaster.muscles[0].rigidbody.velocity.magnitude < maxGetUpVelocity)
				{
					SetState(State.GetUp);
					return;
				}
				Muscle[] muscles3 = base.puppetMaster.muscles;
				foreach (Muscle muscle3 in muscles3)
				{
					muscle3.state.pinWeightMlp = 0f;
					muscle3.state.mappingWeightMlp = Mathf.MoveTowards(muscle3.state.mappingWeightMlp, 1f, Time.deltaTime * masterProps.mappingBlendSpeed);
				}
			}
			if (state != State.Unpinned)
			{
				if (knockOutDistance != lastKnockOutDistance)
				{
					knockOutDistanceSqr = Mathf.Sqrt(knockOutDistance);
					lastKnockOutDistance = knockOutDistance;
				}
				Muscle[] muscles4 = base.puppetMaster.muscles;
				foreach (Muscle muscle4 in muscles4)
				{
					MuscleProps props = GetProps(muscle4.props.group);
					float num = 1f;
					if (state == State.GetUp)
					{
						num = Mathf.Lerp(getUpKnockOutDistanceMlp, num, muscle4.state.pinWeightMlp);
					}
					if (!base.puppetMaster.isBlending && muscle4.state.pinWeightMlp < 0.5f && muscle4.positionOffset.sqrMagnitude * muscle4.props.pinWeight > props.knockOutDistance * knockOutDistanceSqr * num)
					{
						if (state != State.GetUp || getUpTargetFixed)
						{
							SetState(State.Unpinned);
						}
						return;
					}
					muscle4.state.muscleWeightMlp = Mathf.Lerp(unpinnedMuscleWeightMlp, 1f, muscle4.state.pinWeightMlp);
					if (state == State.GetUp)
					{
						muscle4.state.muscleDamperAdd = 0f;
					}
					if (!base.puppetMaster.isKilling)
					{
						float num2 = 1f;
						if (state == State.GetUp)
						{
							num2 = Mathf.Lerp(getUpRegainPinSpeedMlp, 1f, muscle4.state.pinWeightMlp);
						}
						muscle4.state.pinWeightMlp += Time.deltaTime * props.regainPinSpeed * regainPinSpeed * num2;
					}
				}
				float num3 = 1f;
				Muscle[] muscles5 = base.puppetMaster.muscles;
				foreach (Muscle muscle5 in muscles5)
				{
					if ((muscle5.props.group == Muscle.Group.Leg || muscle5.props.group == Muscle.Group.Foot) && muscle5.state.pinWeightMlp < num3)
					{
						num3 = muscle5.state.pinWeightMlp;
					}
				}
				Muscle[] muscles6 = base.puppetMaster.muscles;
				foreach (Muscle muscle6 in muscles6)
				{
					muscle6.state.pinWeightMlp = Mathf.Clamp(muscle6.state.pinWeightMlp, 0f, num3 * 5f);
				}
			}
			if (state == State.GetUp)
			{
				getUpTimer += Time.deltaTime;
				if (getUpTimer > minGetUpDuration)
				{
					getUpTimer = 0f;
					SetState(State.Puppet);
				}
			}
		}

		protected override void OnLateUpdate()
		{
			base.forceActive = (state != State.Puppet);
			if (!base.puppetMaster.isAlive)
			{
				return;
			}
			if (masterProps.normalMode != lastNormalMode)
			{
				if (lastNormalMode == NormalMode.Unmapped)
				{
					Muscle[] muscles = base.puppetMaster.muscles;
					foreach (Muscle muscle in muscles)
					{
						muscle.state.mappingWeightMlp = 1f;
					}
				}
				if (lastNormalMode == NormalMode.Kinematic && base.puppetMaster.mode == PuppetMaster.Mode.Kinematic)
				{
					base.puppetMaster.mode = PuppetMaster.Mode.Active;
				}
				lastNormalMode = masterProps.normalMode;
			}
			switch (masterProps.normalMode)
			{
			case NormalMode.Unmapped:
				if (base.puppetMaster.isActive)
				{
					bool to = false;
					for (int j = 0; j < base.puppetMaster.muscles.Length; j++)
					{
						BlendMuscleMapping(j, ref to);
					}
				}
				break;
			case NormalMode.Kinematic:
				if (SetKinematic())
				{
					base.puppetMaster.mode = PuppetMaster.Mode.Kinematic;
				}
				break;
			}
		}

		private bool SetKinematic()
		{
			if (!base.puppetMaster.isActive)
			{
				return false;
			}
			if (state != 0)
			{
				return false;
			}
			if (base.puppetMaster.isBlending)
			{
				return false;
			}
			if (getupAnimationBlendWeight > 0f)
			{
				return false;
			}
			if (!base.puppetMaster.isAlive)
			{
				return false;
			}
			Muscle[] muscles = base.puppetMaster.muscles;
			foreach (Muscle muscle in muscles)
			{
				if (muscle.state.pinWeightMlp < 1f)
				{
					return false;
				}
			}
			return true;
		}

		protected override void OnReadBehaviour()
		{
			if (!base.enabled)
			{
				return;
			}
			if (!base.puppetMaster.isFrozen && state == State.Unpinned && base.puppetMaster.isActive)
			{
				base.puppetMaster.targetRoot.position = base.puppetMaster.muscles[0].rigidbody.position;
				GroundTarget(groundLayers);
				getUpPosition = base.puppetMaster.targetRoot.position;
			}
			if (getupAnimationBlendWeight > 0f)
			{
				Vector3 vector = Vector3.Project(base.puppetMaster.targetRoot.position - getUpPosition, base.puppetMaster.targetRoot.up);
				getUpPosition += vector;
				base.puppetMaster.targetRoot.position = getUpPosition;
				getupAnimationBlendWeight = Mathf.SmoothDamp(getupAnimationBlendWeight, 0f, ref getupAnimationBlendWeightV, blendToAnimationTime);
				if (getupAnimationBlendWeight < 0.01f)
				{
					getupAnimationBlendWeight = 0f;
				}
				base.puppetMaster.FixTargetToSampledState(getupAnimationBlendWeight);
			}
			getUpTargetFixed = true;
		}

		private void BlendMuscleMapping(int muscleIndex, ref bool to)
		{
			if (base.puppetMaster.muscles[muscleIndex].state.pinWeightMlp < 1f)
			{
				to = true;
			}
			MuscleProps props = GetProps(base.puppetMaster.muscles[muscleIndex].props.group);
			float target = (!to) ? props.minMappingWeight : ((state != 0) ? 1f : props.maxMappingWeight);
			base.puppetMaster.muscles[muscleIndex].state.mappingWeightMlp = Mathf.MoveTowards(base.puppetMaster.muscles[muscleIndex].state.mappingWeightMlp, target, Time.deltaTime * masterProps.mappingBlendSpeed);
		}

		public override void OnMuscleAdded(Muscle m)
		{
			base.OnMuscleAdded(m);
			SetColliders(m, state == State.Unpinned);
		}

		public override void OnMuscleRemoved(Muscle m)
		{
			base.OnMuscleRemoved(m);
			SetColliders(m, unpinned: true);
		}

		private void OnDrawGizmosSelected()
		{
			for (int i = 0; i < groupOverrides.Length; i++)
			{
				groupOverrides[i].name = string.Empty;
				if (groupOverrides[i].groups.Length <= 0)
				{
					continue;
				}
				for (int j = 0; j < groupOverrides[i].groups.Length; j++)
				{
					if (j > 0)
					{
						groupOverrides[i].name += ", ";
					}
					groupOverrides[i].name += groupOverrides[i].groups[j].ToString();
				}
			}
		}

		public void Boost(float immunity, float impulseMlp)
		{
			for (int i = 0; i < base.puppetMaster.muscles.Length; i++)
			{
				Boost(i, immunity, impulseMlp);
			}
		}

		public void Boost(int muscleIndex, float immunity, float impulseMlp)
		{
			BoostImmunity(muscleIndex, immunity);
			BoostImpulseMlp(muscleIndex, impulseMlp);
		}

		public void Boost(int muscleIndex, float immunity, float impulseMlp, float boostParents, float boostChildren)
		{
			if (boostParents <= 0f && boostChildren <= 0f)
			{
				Boost(muscleIndex, immunity, impulseMlp);
				return;
			}
			for (int i = 0; i < base.puppetMaster.muscles.Length; i++)
			{
				float falloff = GetFalloff(i, muscleIndex, boostParents, boostChildren);
				Boost(i, immunity * falloff, impulseMlp * falloff);
			}
		}

		public void BoostImmunity(float immunity)
		{
			if (!(immunity < 0f))
			{
				for (int i = 0; i < base.puppetMaster.muscles.Length; i++)
				{
					BoostImmunity(i, immunity);
				}
			}
		}

		public void BoostImmunity(int muscleIndex, float immunity)
		{
			base.puppetMaster.muscles[muscleIndex].state.immunity = Mathf.Clamp(immunity, base.puppetMaster.muscles[muscleIndex].state.immunity, 1f);
		}

		public void BoostImmunity(int muscleIndex, float immunity, float boostParents, float boostChildren)
		{
			for (int i = 0; i < base.puppetMaster.muscles.Length; i++)
			{
				float falloff = GetFalloff(i, muscleIndex, boostParents, boostChildren);
				BoostImmunity(i, immunity * falloff);
			}
		}

		public void BoostImpulseMlp(float impulseMlp)
		{
			for (int i = 0; i < base.puppetMaster.muscles.Length; i++)
			{
				BoostImpulseMlp(i, impulseMlp);
			}
		}

		public void BoostImpulseMlp(int muscleIndex, float impulseMlp)
		{
			base.puppetMaster.muscles[muscleIndex].state.impulseMlp = Mathf.Max(impulseMlp, base.puppetMaster.muscles[muscleIndex].state.impulseMlp);
		}

		public void BoostImpulseMlp(int muscleIndex, float impulseMlp, float boostParents, float boostChildren)
		{
			for (int i = 0; i < base.puppetMaster.muscles.Length; i++)
			{
				float falloff = GetFalloff(i, muscleIndex, boostParents, boostChildren);
				BoostImpulseMlp(i, impulseMlp * falloff);
			}
		}

		public void Unpin()
		{
			UnityEngine.Debug.Log("BehaviourPuppet.Unpin() has been deprecated. Use SetState(BehaviourPuppet.State) instead.");
			SetState(State.Unpinned);
		}

		protected override void OnMuscleHitBehaviour(MuscleHit hit)
		{
			if (masterProps.normalMode == NormalMode.Kinematic)
			{
				base.puppetMaster.mode = PuppetMaster.Mode.Active;
			}
			UnPin(hit.muscleIndex, hit.unPin);
			base.puppetMaster.muscles[hit.muscleIndex].rigidbody.isKinematic = false;
			base.puppetMaster.muscles[hit.muscleIndex].rigidbody.AddForceAtPosition(hit.force, hit.position);
		}

		protected override void OnMuscleCollisionBehaviour(MuscleCollision m)
		{
			if (!base.enabled || state == State.Unpinned || collisions > maxCollisions || !LayerMaskExtensions.Contains(collisionLayers, m.collision.gameObject.layer) || (masterProps.normalMode == NormalMode.Kinematic && !base.puppetMaster.isActive && !masterProps.activateOnStaticCollisions && m.collision.gameObject.isStatic))
			{
				return;
			}
			float num = GetImpulse(m);
			if (num <= 0f)
			{
				return;
			}
			collisions++;
			if (m.collision.collider.attachedRigidbody != null)
			{
				broadcaster = m.collision.collider.attachedRigidbody.GetComponent<MuscleCollisionBroadcaster>();
				if (broadcaster != null && broadcaster.muscleIndex < broadcaster.puppetMaster.muscles.Length)
				{
					num *= broadcaster.puppetMaster.muscles[broadcaster.muscleIndex].state.impulseMlp;
					float d = (!m.isStay) ? 0.1f : 0.05f;
					broadcaster.puppetMaster.muscles[broadcaster.muscleIndex].offset -= m.collision.impulse * Time.deltaTime * d;
				}
			}
			if (Activate(m.collision, num))
			{
				base.puppetMaster.mode = PuppetMaster.Mode.Active;
			}
			if (OnCollisionImpulse != null)
			{
				OnCollisionImpulse(m, num);
			}
			UnPin(m.muscleIndex, num);
		}

		private float GetImpulse(MuscleCollision m)
		{
			float sqrMagnitude = m.collision.impulse.sqrMagnitude;
			if (collisionThreshold > 0f)
			{
				float num = (!(Singleton<PuppetMasterSettings>.instance != null)) ? 1f : (1f + (float)Singleton<PuppetMasterSettings>.instance.currentlyActivePuppets * Singleton<PuppetMasterSettings>.instance.activePuppetCollisionThresholdMlp);
				if (sqrMagnitude < collisionThreshold * num)
				{
					return 0f;
				}
			}
			sqrMagnitude *= 0.04f;
			CollisionResistanceMultiplier[] array = collisionResistanceMultipliers;
			for (int i = 0; i < array.Length; i++)
			{
				CollisionResistanceMultiplier collisionResistanceMultiplier = array[i];
				if (LayerMaskExtensions.Contains(collisionResistanceMultiplier.layers, m.collision.collider.gameObject.layer))
				{
					sqrMagnitude = ((!(collisionResistanceMultiplier.multiplier <= 0f)) ? (sqrMagnitude / collisionResistanceMultiplier.multiplier) : float.PositiveInfinity);
					break;
				}
			}
			return sqrMagnitude;
		}

		private void UnPin(int muscleIndex, float unpin)
		{
			if (muscleIndex < base.puppetMaster.muscles.Length)
			{
				MuscleProps props = GetProps(base.puppetMaster.muscles[muscleIndex].props.group);
				for (int i = 0; i < base.puppetMaster.muscles.Length; i++)
				{
					UnPinMuscle(i, unpin * GetFalloff(i, muscleIndex, props.unpinParents, props.unpinChildren, props.unpinGroup));
				}
			}
		}

		private void UnPinMuscle(int muscleIndex, float unpin)
		{
			if (!(unpin <= 0f) && !(base.puppetMaster.muscles[muscleIndex].state.immunity >= 1f))
			{
				MuscleProps props = GetProps(base.puppetMaster.muscles[muscleIndex].props.group);
				float num = 1f;
				if (state == State.GetUp)
				{
					num = Mathf.Lerp(getUpCollisionResistanceMlp, 1f, base.puppetMaster.muscles[muscleIndex].state.pinWeightMlp);
				}
				float num2 = (collisionResistance.mode != 0) ? collisionResistance.GetValue(base.puppetMaster.muscles[muscleIndex].targetVelocity.magnitude) : collisionResistance.floatValue;
				float num3 = unpin / (props.collisionResistance * num2 * num);
				num3 *= 1f - base.puppetMaster.muscles[muscleIndex].state.immunity;
				base.puppetMaster.muscles[muscleIndex].state.pinWeightMlp -= num3;
			}
		}

		private bool Activate(Collision collision, float impulse)
		{
			if (masterProps.normalMode != NormalMode.Kinematic)
			{
				return false;
			}
			if (base.puppetMaster.mode != PuppetMaster.Mode.Kinematic)
			{
				return false;
			}
			if (impulse < masterProps.activateOnImpulse)
			{
				return false;
			}
			if (collision.gameObject.isStatic)
			{
				return masterProps.activateOnStaticCollisions;
			}
			return true;
		}

		public bool IsProne()
		{
			float num = Vector3.Dot(base.puppetMaster.muscles[0].transform.rotation * hipsForward, base.puppetMaster.targetRoot.up);
			return num < 0f;
		}

		private float GetFalloff(int i, int muscleIndex, float falloffParents, float falloffChildren)
		{
			if (i == muscleIndex)
			{
				return 1f;
			}
			bool flag = base.puppetMaster.muscles[muscleIndex].childFlags[i];
			int num = base.puppetMaster.muscles[muscleIndex].kinshipDegrees[i];
			return Mathf.Pow((!flag) ? falloffParents : falloffChildren, num);
		}

		private float GetFalloff(int i, int muscleIndex, float falloffParents, float falloffChildren, float falloffGroup)
		{
			float num = GetFalloff(i, muscleIndex, falloffParents, falloffChildren);
			if (falloffGroup > 0f && i != muscleIndex && InGroup(base.puppetMaster.muscles[i].props.group, base.puppetMaster.muscles[muscleIndex].props.group))
			{
				num = Mathf.Max(num, falloffGroup);
			}
			return num;
		}

		private bool InGroup(Muscle.Group group1, Muscle.Group group2)
		{
			if (group1 == group2)
			{
				return true;
			}
			MusclePropsGroup[] array = groupOverrides;
			for (int i = 0; i < array.Length; i++)
			{
				MusclePropsGroup musclePropsGroup = array[i];
				Muscle.Group[] groups = musclePropsGroup.groups;
				foreach (Muscle.Group group3 in groups)
				{
					if (group3 != group1)
					{
						continue;
					}
					Muscle.Group[] groups2 = musclePropsGroup.groups;
					foreach (Muscle.Group group4 in groups2)
					{
						if (group4 == group2)
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		private MuscleProps GetProps(Muscle.Group group)
		{
			MusclePropsGroup[] array = groupOverrides;
			for (int i = 0; i < array.Length; i++)
			{
				MusclePropsGroup musclePropsGroup = array[i];
				Muscle.Group[] groups = musclePropsGroup.groups;
				foreach (Muscle.Group group2 in groups)
				{
					if (group2 == group)
					{
						return musclePropsGroup.props;
					}
				}
			}
			return defaults;
		}

		public void SetState(State newState)
		{
			if (state == newState)
			{
				return;
			}
			switch (newState)
			{
			case State.Puppet:
				base.puppetMaster.SampleTargetMappedState();
				unpinnedTimer = 0f;
				getUpTimer = 0f;
				if (state == State.Unpinned)
				{
					Muscle[] muscles5 = base.puppetMaster.muscles;
					foreach (Muscle muscle5 in muscles5)
					{
						muscle5.state.pinWeightMlp = 1f;
						muscle5.state.muscleWeightMlp = 1f;
						muscle5.state.muscleDamperAdd = 0f;
						SetColliders(muscle5, unpinned: false);
					}
				}
				state = State.Puppet;
				if (eventsEnabled)
				{
					onRegainBalance.Trigger(base.puppetMaster);
					if (onRegainBalance.switchBehaviour)
					{
						return;
					}
				}
				break;
			case State.Unpinned:
			{
				unpinnedTimer = 0f;
				getUpTimer = 0f;
				getupAnimationBlendWeight = 0f;
				getupAnimationBlendWeightV = 0f;
				Muscle[] muscles2 = base.puppetMaster.muscles;
				foreach (Muscle muscle2 in muscles2)
				{
					muscle2.state.immunity = 0f;
					SetColliders(muscle2, unpinned: true);
				}
				if (dropProps)
				{
					RemoveMusclesOfGroup(Muscle.Group.Prop);
				}
				Muscle[] muscles3 = base.puppetMaster.muscles;
				foreach (Muscle muscle3 in muscles3)
				{
					muscle3.state.muscleWeightMlp = ((!base.puppetMaster.isAlive) ? base.puppetMaster.stateSettings.deadMuscleWeight : unpinnedMuscleWeightMlp);
				}
				onLoseBalance.Trigger(base.puppetMaster, base.puppetMaster.isAlive);
				if (onLoseBalance.switchBehaviour)
				{
					state = State.Unpinned;
					return;
				}
				if (state == State.Puppet)
				{
					onLoseBalanceFromPuppet.Trigger(base.puppetMaster, base.puppetMaster.isAlive);
					if (onLoseBalanceFromPuppet.switchBehaviour)
					{
						state = State.Unpinned;
						return;
					}
				}
				else
				{
					onLoseBalanceFromGetUp.Trigger(base.puppetMaster, base.puppetMaster.isAlive);
					if (onLoseBalanceFromGetUp.switchBehaviour)
					{
						state = State.Unpinned;
						return;
					}
				}
				Muscle[] muscles4 = base.puppetMaster.muscles;
				foreach (Muscle muscle4 in muscles4)
				{
					muscle4.state.pinWeightMlp = 0f;
				}
				break;
			}
			case State.GetUp:
			{
				unpinnedTimer = 0f;
				getUpTimer = 0f;
				bool flag = IsProne();
				state = State.GetUp;
				if (flag)
				{
					onGetUpProne.Trigger(base.puppetMaster);
					if (onGetUpProne.switchBehaviour)
					{
						return;
					}
				}
				else
				{
					onGetUpSupine.Trigger(base.puppetMaster);
					if (onGetUpSupine.switchBehaviour)
					{
						return;
					}
				}
				Muscle[] muscles = base.puppetMaster.muscles;
				foreach (Muscle muscle in muscles)
				{
					muscle.state.muscleWeightMlp = 0f;
					muscle.state.pinWeightMlp = 0f;
					muscle.state.muscleDamperAdd = 0f;
					SetColliders(muscle, unpinned: false);
				}
				Vector3 tangent = base.puppetMaster.muscles[0].rigidbody.rotation * hipsUp;
				Vector3 normal = base.puppetMaster.targetRoot.up;
				Vector3.OrthoNormalize(ref normal, ref tangent);
				base.puppetMaster.targetRoot.rotation = Quaternion.LookRotation((!flag) ? (-tangent) : tangent, base.puppetMaster.targetRoot.up);
				base.puppetMaster.SampleTargetMappedState();
				Vector3 point = (!flag) ? getUpOffsetSupine : getUpOffsetProne;
				base.puppetMaster.targetRoot.position = base.puppetMaster.muscles[0].rigidbody.position;
				base.puppetMaster.targetRoot.position += base.puppetMaster.targetRoot.rotation * point;
				GroundTarget(groundLayers);
				getUpPosition = base.puppetMaster.targetRoot.position;
				getupAnimationBlendWeight = 1f;
				getUpTargetFixed = false;
				break;
			}
			}
			state = newState;
		}

		public void SetColliders(bool unpinned)
		{
			Muscle[] muscles = base.puppetMaster.muscles;
			foreach (Muscle m in muscles)
			{
				SetColliders(m, unpinned);
			}
		}

		private void SetColliders(Muscle m, bool unpinned)
		{
			MuscleProps props = GetProps(m.props.group);
			if (unpinned)
			{
				Collider[] colliders = m.colliders;
				foreach (Collider collider in colliders)
				{
					collider.material = ((!(props.unpinnedMaterial != null)) ? defaults.unpinnedMaterial : props.unpinnedMaterial);
					collider.enabled = true;
				}
				return;
			}
			Collider[] colliders2 = m.colliders;
			foreach (Collider collider2 in colliders2)
			{
				collider2.material = ((!(props.puppetMaterial != null)) ? defaults.puppetMaterial : props.puppetMaterial);
				if (props.disableColliders)
				{
					collider2.enabled = false;
				}
			}
		}
	}
}
