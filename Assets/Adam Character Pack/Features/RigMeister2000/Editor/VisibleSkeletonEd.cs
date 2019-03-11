using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(VisibleSkeleton))]
public class VisibleSkeletonEd : Editor {
	new VisibleSkeleton target { get { return base.target as VisibleSkeleton; } }

	public override void OnInspectorGUI() {
		EditorGUI.BeginChangeCheck();

		var showFullSkeleton = GUILayout.Toggle(target.showFullSkeleton, "Show Full Skeleton", "button");
		var showHumanSkeleton = GUILayout.Toggle(target.showHumanSkeleton, "Show Human Skeleton", "button");
		var showBoneAxes = GUILayout.Toggle(target.showBoneAxes, "Show Bone Axes", "button");
		var muteHands = GUILayout.Toggle(target.muteHands, "Mute Hand Bones", "button");

		if(EditorGUI.EndChangeCheck()) {
			target.showFullSkeleton = showFullSkeleton;
			target.showHumanSkeleton = showHumanSkeleton;
			target.showBoneAxes = showBoneAxes;
			target.muteHands = muteHands;

			SceneView.RepaintAll();
		}
	}

	const float BONE_RADIUS = 0.025f;
	const float AXIS_LENGTH = 0.075f;
	//static Color[]	ms_axisColor = new[] { Color.red, Color.green, Color.blue };
	void OnSceneGUI() {
		if(!target.enabled)
			return;

		var animator = target.GetComponent<Animator>();
		var rootBone = animator ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;

		if(!animator || !rootBone)
			return;

		var leftHandRoot = target.muteHands ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
		var rightHandRoot = target.muteHands ?  animator.GetBoneTransform(HumanBodyBones.RightHand) : null;

		if(target.showFullSkeleton) {
			Handles.color = Color.magenta;
			DrawSkeleton(rootBone, null, null, leftHandRoot, rightHandRoot);
		}

		if(target.showHumanSkeleton) {
			var avatarBones = new HashSet<Transform>();
			for(int i = 0; i < (int)HumanBodyBones.LastBone; ++i)
				avatarBones.Add(animator.GetBoneTransform((HumanBodyBones)i));

			Handles.color = Color.cyan;
			DrawSkeleton(rootBone, null, avatarBones, leftHandRoot, rightHandRoot);
		}
	}

	void DrawSkeleton(Transform root, Transform prevRoot, HashSet<Transform> avatarBones, Transform leftHandRoot, Transform rightHandRoot) {
		if(leftHandRoot && root.IsChildOf(leftHandRoot) && root != leftHandRoot)
			return;

		if(rightHandRoot && root.IsChildOf(rightHandRoot) && root != rightHandRoot)
			return;

		if(prevRoot)
			Handles.DrawLine(root.position, prevRoot.position);

		Handles.SphereCap(-1, root.position, Quaternion.identity, BONE_RADIUS);

		foreach(Transform child in root)
			if(avatarBones == null || avatarBones.Contains(child))
				DrawSkeleton(child, root, avatarBones, leftHandRoot, rightHandRoot);

		if(target.showBoneAxes)
			DrawAxes(root);
	}

	void DrawAxes(Transform root) {
		var c = Handles.color;

		Handles.color = Color.red;
		Handles.ArrowCap(-1, root.position, Quaternion.LookRotation(root.right), AXIS_LENGTH);

		Handles.color = Color.green;
		Handles.ArrowCap(-1, root.position, Quaternion.LookRotation(root.up), AXIS_LENGTH);

		Handles.color = Color.blue;
		Handles.ArrowCap(-1, root.position, Quaternion.LookRotation(root.forward), AXIS_LENGTH);

		Handles.color = c;
	}
}
