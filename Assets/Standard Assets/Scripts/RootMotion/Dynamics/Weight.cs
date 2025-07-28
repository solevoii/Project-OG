using System;
using UnityEngine;

namespace RootMotion.Dynamics
{
	[Serializable]
	public class Weight
	{
		[Serializable]
		public enum Mode
		{
			Float,
			Curve
		}

		public Mode mode;

		public float floatValue;

		public AnimationCurve curve;

		public string tooltip = string.Empty;

		public Weight(float floatValue)
		{
			this.floatValue = floatValue;
		}

		public Weight(float floatValue, string tooltip)
		{
			this.floatValue = floatValue;
			this.tooltip = tooltip;
		}

		public float GetValue(float param)
		{
			Mode mode = this.mode;
			if (mode == Mode.Curve)
			{
				return curve.Evaluate(param);
			}
			return floatValue;
		}
	}
}
