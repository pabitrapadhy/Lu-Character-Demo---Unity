using UnityEngine;

[ExecuteInEditMode]
public class RigMeister2000 : MonoBehaviour {
	public Transform		skeletonRoot;
	public Transform		sourceSkeletonRoot;
	public ConstraintSet[]	constraints;
	public bool				showConstrainedJointsOnly;
	public bool				debugShowConstraintsGizmos = true;
	public bool				debugShowActiveJointGizmo = true;

	public void RestoreSourceOrientations() {
		if(constraints == null)
			return;

		if(!skeletonRoot || !sourceSkeletonRoot) {
			Debug.LogWarning("Unable to restore source orientations! Both Skeleton Root and Source Skeleton Root need to be set.");
			return;
		}

		var src = sourceSkeletonRoot.GetComponentsInChildren<Transform>(true);
		foreach(var c in constraints) {
			foreach(var s in src) {
				if(c.sourceJoint && c.sourceJoint.name == s.name) {
					c.sourceJoint.localPosition = s.localPosition;
					c.sourceJoint.localRotation = s.localRotation;
					c.sourceJoint.localScale = s.localScale;
				}
			}
		}
	}

	public void FullPoseReset() {
		if(!sourceSkeletonRoot || !skeletonRoot) {
			Debug.LogWarning("Unable to perform full pose reset! Both Skeleton Root and Source Skeleton Root need to be set.");
			return;
		}

		var src = sourceSkeletonRoot.GetComponentsInChildren<Transform>(true);
		var dst = skeletonRoot.GetComponentsInChildren<Transform>();

		for(int i = 0, n = src.Length; i < n; ++i) {
			dst[i].localPosition = src[i].localPosition;
			dst[i].localRotation = src[i].localRotation;
			dst[i].localScale = src[i].localScale;
		}
	}

	void OnDisable() {
		RestoreSourceOrientations();	
	}

	void LateUpdate () {
		EvaluateConstraints();
	}

	void EvaluateConstraints() {
		for(int i = 0, n = constraints != null ? constraints.Length : 0; i < n; ++i) {
			var constraint = constraints[i];

			if(constraint.usePositionConstraint)
			if(constraint.positionConstraint.weightedTargetJoints.Length > 0)
				constraint.positionConstraint.ApplyConstraint(constraint.sourceJoint);

			if(constraint.useOrientationConstraint)
				if(constraint.orientationConstraint.weightedTargetJoints.Length > 0)
				constraint.orientationConstraint.ApplyConstraint(constraint.sourceJoint);

			if(constraint.useLookAtConstraint)
				constraint.lookAtConstraint.ApplyConstraint(constraint.sourceJoint);
		}
	}

	#region Constraints
	public enum Axis : int { X = 0, Y = 1, Z = 2 }

	[System.Serializable]
	public class ConstraintWeightedTarget {
		public Transform	joint;
		public float		weight;

		public static Vector3 GetWeightedPosition(ConstraintWeightedTarget[] targets) {
			float totWeightRcp = 0f;
			for(int i = 0, n = targets.Length; i < n; ++i)
				totWeightRcp += targets[i].weight;
			totWeightRcp = 1f / totWeightRcp;

			Vector3 p = Vector3.zero;
			for(int i = 0, n = targets.Length; i < n; ++i) {
				var tgt = targets[i];
				p += tgt.joint.position * tgt.weight * totWeightRcp;
			}

			return p;
		}

		public static Quaternion GetWeightedOrientation(ConstraintWeightedTarget[] targets, bool worldSpace) {
			if(worldSpace) {
				float accumW = targets[0].weight;
				Quaternion o = targets[0].joint.rotation;
				for(int i = 1, n = targets.Length; i < n; ++i) {
					var tgt = targets[i];
					var combinedWeight = tgt.weight + accumW;
					o = Quaternion.SlerpUnclamped(o, tgt.joint.rotation, tgt.weight / combinedWeight);
				}
				return o;
			} else {
				float accumW = targets[0].weight;
				Quaternion o = targets[0].joint.localRotation;
				for(int i = 1, n = targets.Length; i < n; ++i) {
					var tgt = targets[i];
					var combinedWeight = tgt.weight + accumW;
					o = Quaternion.SlerpUnclamped(o, tgt.joint.localRotation, tgt.weight / combinedWeight);
				}
				return o;
			}
		}
	}

	[System.Serializable]
	public class LookAtConstraint {
		public Axis							sourceLookAtAxis;
		public float						sourceLookAtAxisFlip;
		public ConstraintWeightedTarget[]	weightedTargetJoints;

		public bool							upWorld;
		public Transform					upJoint;
		public bool							upLookAt;
		public Axis							upSourceAxis;
		public float						upSourceAxisFlip;
		public Axis							upTargetAxis;

		public Axis							implicitAxis;

		static readonly Vector3[]	ms_axisVectors	= new[] { Vector3.right, Vector3.up, Vector3.forward };
		static Vector3[]			ms_lookAtVectors= new Vector3[3];

		public void SetDefaults() {
			sourceLookAtAxis = Axis.X;
			sourceLookAtAxisFlip = 1f;
			System.Array.Resize(ref weightedTargetJoints, 0);
			upWorld = true;
			upJoint = null;
			upLookAt = false;
			upSourceAxis = Axis.Y;
			upSourceAxisFlip = 1f;
			upTargetAxis = Axis.Y;
			UpdateImplicitAxis();
		}

		public void UpdateImplicitAxis() {
			var xyz = 7;
			if(sourceLookAtAxis == Axis.X || upSourceAxis == Axis.X)
				xyz &= ~1;
			if(sourceLookAtAxis == Axis.Y || upSourceAxis == Axis.Y)
				xyz &= ~2;
			if(sourceLookAtAxis == Axis.Z || upSourceAxis == Axis.Z)
				xyz &= ~4;

			implicitAxis = xyz == 1 ? Axis.X : (xyz == 2 ? Axis.Y : Axis.Z);
		}

		public void ApplyConstraint(Transform sourceJoint) {
			if(weightedTargetJoints.Length == 0)
				return;

			var weightedTargetPosition = ConstraintWeightedTarget.GetWeightedPosition(weightedTargetJoints);
			ms_lookAtVectors[(int)sourceLookAtAxis] = (weightedTargetPosition - sourceJoint.position).normalized * sourceLookAtAxisFlip;

			if(upLookAt) {
				if(upWorld)
					ms_lookAtVectors[(int)upSourceAxis] = ms_axisVectors[(int)Axis.Y] * upSourceAxisFlip; // Is this correct?
				else
					ms_lookAtVectors[(int)upSourceAxis] = (upJoint.position - sourceJoint.position).normalized * upSourceAxisFlip;
			} else {
				if(upWorld) {
					ms_lookAtVectors[(int)upSourceAxis] = ms_axisVectors[(int)upTargetAxis] * upSourceAxisFlip;
				} else {
					var weightedTargetOrientation = ConstraintWeightedTarget.GetWeightedOrientation(weightedTargetJoints, true);
					ms_lookAtVectors[(int)upSourceAxis] = weightedTargetOrientation * ms_axisVectors[(int)upTargetAxis] * upSourceAxisFlip;
				}
			}

			ms_lookAtVectors[(int)implicitAxis] = Vector3.Cross(ms_lookAtVectors[(int)sourceLookAtAxis], ms_lookAtVectors[(int)upSourceAxis]);
			ms_lookAtVectors[(int)upSourceAxis] = Vector3.Cross(ms_lookAtVectors[(int)implicitAxis], ms_lookAtVectors[(int)sourceLookAtAxis]);

			sourceJoint.rotation = Quaternion.LookRotation(ms_lookAtVectors[2], ms_lookAtVectors[1]);
		}
	}

	[System.Serializable]
	public class PositionConstraint {
		public ConstraintWeightedTarget[]	weightedTargetJoints;

		public void SetDefaults() {
			System.Array.Resize(ref weightedTargetJoints,0 );
		}

		public void ApplyConstraint(Transform sourceJoint) {
			if(weightedTargetJoints.Length == 0)
				return;

			sourceJoint.position = ConstraintWeightedTarget.GetWeightedPosition(weightedTargetJoints);
		}
	}

	[System.Serializable]
	public class OrientationConstraint {
		public ConstraintWeightedTarget[]	weightedTargetJoints;
		public bool							worldSpace;

		public void SetDefaults() {
			System.Array.Resize(ref weightedTargetJoints, 0);
			worldSpace = true;
		}

		public void ApplyConstraint(Transform sourceJoint) {
			if(weightedTargetJoints.Length == 0)
				return;

			sourceJoint.rotation = ConstraintWeightedTarget.GetWeightedOrientation(weightedTargetJoints, worldSpace);
		}
	}

	[System.Serializable]
	public class ConstraintSet {
		public Transform					sourceJoint;
		public bool							usePositionConstraint, useOrientationConstraint, useLookAtConstraint;
		public bool							showPositionConstraint, showOrientationConstraint, showLookAtConstraint;
		public PositionConstraint			positionConstraint;
		public OrientationConstraint		orientationConstraint;
		public LookAtConstraint				lookAtConstraint;

		public void SetDefaults() {
			sourceJoint = null;
			usePositionConstraint = useOrientationConstraint = useLookAtConstraint = false;
			showPositionConstraint = showOrientationConstraint = showLookAtConstraint = false;
			positionConstraint.SetDefaults();
			orientationConstraint.SetDefaults();
			lookAtConstraint.SetDefaults();
		}
	}
	#endregion // constraints
}
