using UnityEngine;

namespace RootMotion.Dynamics
{
	public class JointBreakBroadcaster : MonoBehaviour
	{
		[SerializeField]
		[HideInInspector]
		public PuppetMaster puppetMaster;

		[SerializeField]
		[HideInInspector]
		public int muscleIndex;

		private void OnJointBreak()
		{
			puppetMaster.RemoveMuscleRecursive(puppetMaster.muscles[muscleIndex].joint, attachTarget: true, blockTargetAnimation: true, MuscleRemoveMode.Numb);
		}
	}
}
