using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(RigMeister2000))]
public class RigMeister2000Ed : Editor {
	new RigMeister2000 target { get { return base.target as RigMeister2000; } }

	static GUIContent[]	ms_axisNames;
	static GUIContent[]	ms_alignModeNames;
	static GUIStyle[]	ms_listRowStyles;
	static GUIStyle		ms_headerStyle;
	static GUIStyle		ms_headerRevStyle;

	List<Transform>		m_transformCache = new List<Transform>();

	SerializedProperty	m_spSkeletonRoot;
	SerializedProperty	m_spSourceSkeletonRoot;
	SerializedProperty	m_spConstraints;
	SerializedProperty	m_spShowConstrainedJointsOnly;

	string				m_jointFilter;
	Transform			m_jointSelected;
	Vector2				m_jointsScroll;
	Transform			m_deferredJointSelected;


	Vector2				m_posConstraintScrollPos;
	Vector2				m_oriConstraintScrollPos;
	Vector2				m_latConstraintScrollPos;

	void OnEnable() {
		m_spSkeletonRoot = serializedObject.FindProperty("skeletonRoot");
		m_spSourceSkeletonRoot = serializedObject.FindProperty("sourceSkeletonRoot");
		m_spConstraints = serializedObject.FindProperty("constraints");
		m_spShowConstrainedJointsOnly = serializedObject.FindProperty("showConstrainedJointsOnly");
	}

	void EnsureStyles() {
		if(Event.current.type == EventType.Layout && ms_listRowStyles == null) {
			ms_headerStyle = new GUIStyle(EditorStyles.helpBox);
			ms_headerStyle.fontSize = 24;
			ms_headerStyle.fontStyle = FontStyle.BoldAndItalic;
			ms_headerStyle.alignment = TextAnchor.UpperCenter;
			ms_headerRevStyle = new GUIStyle(EditorStyles.miniLabel);
			ms_headerRevStyle.alignment = TextAnchor.LowerRight;
			ms_headerRevStyle.padding = new RectOffset(5, 5, 3, 3);
			ms_headerRevStyle.normal.textColor = Color.gray;
			ms_listRowStyles = new GUIStyle[4];
			ms_listRowStyles[0] = EditorStyles.label;
			ms_listRowStyles[0].margin = new RectOffset(EditorStyles.label.margin.left, EditorStyles.label.margin.right, 0, 1);
			ms_listRowStyles[1] = new GUIStyle(ms_listRowStyles[0]);
			ms_listRowStyles[1].fontStyle = FontStyle.Bold;
			ms_listRowStyles[2] = new GUIStyle(ms_listRowStyles[0]);
			ms_listRowStyles[2].normal.textColor = Color.white;
			ms_listRowStyles[2].normal.background = new Texture2D(1, 1, TextureFormat.ARGB32, false, true);
			ms_listRowStyles[2].normal.background.SetPixel(0, 0, new Color(0.45f, 0.55f, 1f, 1f));
			ms_listRowStyles[2].normal.background.Apply(false, true);
			ms_listRowStyles[3] = new GUIStyle(ms_listRowStyles[2]);
			ms_listRowStyles[3].fontStyle = FontStyle.Bold;
			ms_axisNames = new[] { new GUIContent("X"), new GUIContent("Y"), new GUIContent("Z") };
			ms_alignModeNames = new[] { new GUIContent("Look At"), new GUIContent("Axis Align") };
		}
	}

	public override void OnInspectorGUI() {
		// These need to be created from inside an OnGUI callback.
		EnsureStyles();

		if(!DoInspectorHeader()) {
			EditorGUILayout.HelpBox("Skeleton Root required! Source Skeleton Root required for pose reset functionality (currently, this should be pointed to the asset matching the skeleton instenace).", MessageType.Info);
			return;
		}

		EditorGUILayout.Space();

		DoInspectorJointSelection();
		EditorGUILayout.Space();

		var selectedHasConstraint = target.constraints.Any(c => c.sourceJoint == m_jointSelected);
		DoInspectorConstraintCreateDestroy(selectedHasConstraint);

		EditorGUILayout.Space();
	
		selectedHasConstraint = target.constraints.Any(c => c.sourceJoint == m_jointSelected); // might have changed
		DoInspectorSelectedConstraint(selectedHasConstraint);

		if(GUI.changed /*serializedObject.hasModifiedProperties*/) {
			serializedObject.ApplyModifiedProperties();

			foreach(var c in target.constraints)
				c.lookAtConstraint.UpdateImplicitAxis();

			serializedObject.Update();
		}

		EditorGUILayout.Space();

		DoInspectorDebug();
	}

	static readonly string ms_revisionString = "rev" + "$Rev: 512 $".Split(' ')[1];
	bool DoInspectorHeader() {
		EditorGUILayout.LabelField("RigMeister 2000", ms_headerStyle);
		GUI.Label(GUILayoutUtility.GetLastRect(), ms_revisionString, ms_headerRevStyle);

		EditorGUILayout.PropertyField(m_spSkeletonRoot);
		EditorGUILayout.PropertyField(m_spSourceSkeletonRoot);

		if(GUI.changed /*serializedObject.hasModifiedProperties*/)
			serializedObject.ApplyModifiedProperties();

		return m_spSkeletonRoot.objectReferenceValue;
	}

	void DoInspectorJointSelection() {
		var skeletonRoot = m_spSkeletonRoot.objectReferenceValue as Transform;
		skeletonRoot.GetComponentsInChildren<Transform>(m_transformCache);

		var showConstrainedOnly = m_spShowConstrainedJointsOnly.boolValue;
		GUILayout.Label("Skeleton Joints:");

		EditorGUILayout.BeginHorizontal();
		{
			GUI.enabled = !m_spShowConstrainedJointsOnly.boolValue;
			EditorGUILayout.LabelField("Filter:", GUILayout.MaxWidth(50));
			m_jointFilter = EditorGUILayout.TextField(m_jointFilter);
			if(GUILayout.Button("X", GUILayout.Width(20))) {
				m_jointFilter = "";
				Repaint();
			}
			GUI.enabled = true;
		}
		EditorGUILayout.EndHorizontal();

		GUILayout.BeginHorizontal(GUI.skin.box);
		m_jointsScroll = GUILayout.BeginScrollView(m_jointsScroll, GUILayout.Height(showConstrainedOnly ? Mathf.Clamp(18f * target.constraints.Length, 18f, 5f * 18f) : 200));
		for(int i = 0, n = m_transformCache.Count; i < n; ++i) {
			var joint = m_transformCache[i];

			if(!string.IsNullOrEmpty(m_jointFilter) && !joint.name.ToLowerInvariant().Contains(m_jointFilter))
				continue;

			var hasConstraint = target.constraints.Any(c => c.sourceJoint == joint);

			if(m_spShowConstrainedJointsOnly.boolValue && !hasConstraint) {
				if(joint == m_jointSelected)
					m_jointSelected = null;

				continue;
			}

			var styleIndex = (m_jointSelected == joint ? 2 : 0) + (hasConstraint ? 1 : 0);
			EditorGUILayout.BeginHorizontal();
			{
				GUILayout.Label(joint.name, ms_listRowStyles[styleIndex]);
				if(Event.current.type == EventType.MouseDown) {
					if(GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
						m_deferredJointSelected = joint;
				}
			}
			EditorGUILayout.EndHorizontal();
		}
		GUILayout.EndScrollView();
		GUILayout.EndHorizontal();

		if(Event.current.type == EventType.Layout && m_deferredJointSelected) {
			if(m_jointSelected == m_deferredJointSelected)
				m_jointSelected = null;
			else
				m_jointSelected = m_deferredJointSelected;

			if(m_jointSelected && target.constraints.Any(c => c.sourceJoint == m_jointSelected))
				EditorGUIUtility.PingObject(m_jointSelected);

			m_deferredJointSelected = null;
			Repaint();
		}
		
		if(GUILeftToggle(m_spShowConstrainedJointsOnly))
			m_jointFilter = "";
	}

	void DoInspectorConstraintCreateDestroy(bool selectedHasConstraint) {
		GUI.enabled = m_jointSelected && !selectedHasConstraint;
		if(GUILayout.Button("Add Multi Constraint")) {
			m_spConstraints.InsertArrayElementAtIndex(m_spConstraints.arraySize);
			serializedObject.ApplyModifiedProperties();

			target.constraints[target.constraints.Length - 1].SetDefaults();
			target.constraints[target.constraints.Length - 1].sourceJoint = m_jointSelected;

			// Should really just insert directly into correct index.. but we just happen to have this function.. :)
			FullConstraintsReorder(false);  // does so.Update()
		}
		GUI.enabled = true;

		GUI.enabled = m_jointSelected && selectedHasConstraint;
		if(GUILayout.Button("Remove Multi Constraint")) {
			m_spConstraints.DeleteArrayElementAtIndex(FindArrayIndex(m_spConstraints, "sourceJoint", m_jointSelected));
			serializedObject.ApplyModifiedProperties();
		}
		GUI.enabled = true;
	}

	void DoInspectorSelectedConstraint(bool selectedHasConstraint) {
		if(!selectedHasConstraint)
			return;

		EditorGUILayout.LabelField("Constraints:");

		SerializedProperty constraint = m_spConstraints.GetArrayElementAtIndex(FindArrayIndex(m_spConstraints, "sourceJoint", m_jointSelected));
		DoInspectorPositionConstraint(constraint);
		DoInspectorOrientationConstraint(constraint);
		DoInspectorLookAtConstraint(constraint);
	}

	void DoInspectorPositionConstraint(SerializedProperty constraint) {
		if(!DoInspectorConstraintHeader(constraint, new GUIContent("Position Constraint"), "usePositionConstraint", "showPositionConstraint"))
			return;

		DoWeightedTargetsList(ref m_posConstraintScrollPos, constraint, constraint.FindPropertyRelative("positionConstraint.weightedTargetJoints"));
	}

	void DoInspectorOrientationConstraint(SerializedProperty constraint) {
		if(!DoInspectorConstraintHeader(constraint, new GUIContent("Orientation Constraint"), "useOrientationConstraint", "showOrientationConstraint"))
			return;

		DoWeightedTargetsList(ref m_oriConstraintScrollPos, constraint, constraint.FindPropertyRelative("orientationConstraint.weightedTargetJoints"));
	}

	void DoInspectorLookAtConstraint(SerializedProperty constraint) {
		if(!DoInspectorConstraintHeader(constraint, new GUIContent("LookAt Constraint"), "useLookAtConstraint", "showLookAtConstraint"))
			return;

		var spLookAtConstraint = constraint.FindPropertyRelative("lookAtConstraint");
		var spSourceLookAtAxis = spLookAtConstraint.FindPropertyRelative("sourceLookAtAxis");
		var spSourceLookAtAxisFlip = spLookAtConstraint.FindPropertyRelative("sourceLookAtAxisFlip");
		var spUpWorld = spLookAtConstraint.FindPropertyRelative("upWorld");
		var spUpJoint = spLookAtConstraint.FindPropertyRelative("upJoint");
		var spUpLookAt = spLookAtConstraint.FindPropertyRelative("upLookAt");
		var spUpSourceAxis = spLookAtConstraint.FindPropertyRelative("upSourceAxis");
		var spUpSourceAxisFlip = spLookAtConstraint.FindPropertyRelative("upSourceAxisFlip");
		var spUpTargetAxis = spLookAtConstraint.FindPropertyRelative("upTargetAxis");
		var spImplicitAxis = spLookAtConstraint.FindPropertyRelative("implicitAxis");

		DoWeightedTargetsList(ref m_latConstraintScrollPos, constraint, spLookAtConstraint.FindPropertyRelative("weightedTargetJoints"));
		EditorGUILayout.Space();

		GUIAxisSelect("LookAt Axis", spSourceLookAtAxis, spSourceLookAtAxisFlip);

		EditorGUILayout.BeginHorizontal();
		{
			EditorGUILayout.LabelField(spUpWorld.displayName, GUILayout.MaxWidth(100));
			EditorGUILayout.PropertyField(spUpWorld, GUIContent.none, GUILayout.Width(40));
			GUI.enabled = !spUpWorld.boolValue;
			EditorGUILayout.LabelField(spUpJoint.displayName, GUILayout.MaxWidth(60));
			EditorGUILayout.PropertyField(spUpJoint, GUIContent.none);
			GUI.enabled = true;
		}
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.BeginHorizontal();
		{
			EditorGUILayout.LabelField("Up Align Mode", GUILayout.MaxWidth(100));
			EditorGUI.BeginChangeCheck();
			var newMode = GUILayout.SelectionGrid(spUpLookAt.boolValue ? 0 : 1, ms_alignModeNames, ms_alignModeNames.Length, EditorStyles.miniButton);
			if(EditorGUI.EndChangeCheck())
				spUpLookAt.boolValue = newMode == 0;
		}
		EditorGUILayout.EndHorizontal();

		GUIAxisSelect("Up Source Axis", spUpSourceAxis, spUpSourceAxisFlip);

		GUI.enabled = !spUpLookAt.boolValue;
		GUIAxisSelect("Up Target Axis", spUpTargetAxis, null);
		GUI.enabled = true;

		GUI.color = Color.grey;
		GUI.enabled = false;
		GUIAxisSelect("Implicit Axis", spImplicitAxis, null);
		GUI.enabled = true;
		GUI.color = Color.white;
	}

	bool DoInspectorConstraintHeader(SerializedProperty constraint, GUIContent title, string useName, string showName) {
		var spUse = constraint.FindPropertyRelative(useName);
		var spShow = constraint.FindPropertyRelative(showName);

		EditorGUILayout.BeginHorizontal();
		{
			bool useChanged;
			GUI.enabled = GUILeftToggleSpecial(spUse, 15, out useChanged);
			if(useChanged)
				spShow.boolValue = GUI.enabled;
			
			++EditorGUI.indentLevel;
			spShow.boolValue = EditorGUILayout.Foldout(spShow.boolValue, title);
			--EditorGUI.indentLevel;
			GUI.enabled = true;
		}
		EditorGUILayout.EndHorizontal();
		
		return spUse.boolValue && spShow.boolValue;
	}

	void DoWeightedTargetsList(ref Vector2 scrollPos, SerializedProperty constraint, SerializedProperty spWeightedList) {
		GUILayout.Label("Weighted Target Joints:");
		GUILayout.BeginHorizontal(GUI.skin.box);
		scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.MinHeight(Mathf.Clamp(22f * spWeightedList.arraySize, 22f, 66f)), GUILayout.MaxHeight(88f));
		for(int i = 0; i < spWeightedList.arraySize; ++i) {
			var spWeightedTarget = spWeightedList.GetArrayElementAtIndex(i);

			EditorGUILayout.BeginHorizontal();
			{
				EditorGUILayout.PropertyField(spWeightedTarget.FindPropertyRelative("joint"), GUIContent.none, GUILayout.MinWidth(20f));
				EditorGUILayout.PropertyField(spWeightedTarget.FindPropertyRelative("weight"), GUIContent.none, GUILayout.MinWidth(20f), GUILayout.MaxWidth(60f));
				if(GUILayout.Button("X", GUILayout.Width(20)))
					spWeightedList.DeleteArrayElementAtIndex(i--);
			}
			EditorGUILayout.EndHorizontal();
		}
		GUILayout.EndScrollView();
		GUILayout.EndHorizontal();

		if(GUILayout.Button("Add Target")) {
			spWeightedList.InsertArrayElementAtIndex(spWeightedList.arraySize);
			var spNewElement = spWeightedList.GetArrayElementAtIndex(spWeightedList.arraySize - 1);
			spNewElement.FindPropertyRelative("joint").objectReferenceValue = constraint.FindPropertyRelative("sourceJoint").objectReferenceValue;
			spNewElement.FindPropertyRelative("weight").floatValue = 100f;
			serializedObject.ApplyModifiedProperties();
		}
	}

	void DoInspectorDebug() {
		EditorGUILayout.LabelField("Debug Options:");

		var showDebug = GUILayout.Toggle(target.debugShowConstraintsGizmos, "Draw Constraints Gizmos", "button");
		var showActive = GUILayout.Toggle(target.debugShowActiveJointGizmo, "Draw Selected Joint Gizmo", "button");
		if(showDebug != target.debugShowConstraintsGizmos || showActive != target.debugShowActiveJointGizmo) {
			target.debugShowConstraintsGizmos = showDebug;
			target.debugShowActiveJointGizmo = showActive;
			SceneView.RepaintAll();
		}
		if(GUILayout.Button("Full Pose Reset"))
			target.FullPoseReset();
		if(GUILayout.Button("Full Constraints Reorder"))
			FullConstraintsReorder(true);
	}

	void GUIAxisSelect(string label, SerializedProperty spAxis, SerializedProperty spFlip) {
		EditorGUILayout.BeginHorizontal();
		{
			EditorGUILayout.LabelField(label, GUILayout.MaxWidth(100));

			EditorGUI.BeginChangeCheck();
			var newAxis = GUILayout.SelectionGrid(spAxis.intValue, ms_axisNames, ms_axisNames.Length, EditorStyles.miniButton);
			if(EditorGUI.EndChangeCheck())
				spAxis.intValue = newAxis;

			if(spFlip != null) {
				EditorGUI.BeginChangeCheck();
				var newFlip = GUILayout.Toggle(spFlip.floatValue < 0f, "Flip", EditorStyles.miniButton);
				if(EditorGUI.EndChangeCheck())
					spFlip.floatValue = newFlip ? -1f : 1f;
			}
		}
		EditorGUILayout.EndHorizontal();
	}

	bool GUILeftToggle(SerializedProperty sp) {
		EditorGUI.BeginChangeCheck();
		var toggle = EditorGUILayout.ToggleLeft(sp.displayName, sp.boolValue);
		if(EditorGUI.EndChangeCheck())
			sp.boolValue = toggle;
		return toggle;
	}

	bool GUILeftToggleSpecial(SerializedProperty sp, float width, out bool changed) {
		EditorGUI.BeginChangeCheck();
		var toggle = EditorGUILayout.Toggle(sp.boolValue, GUILayout.Width(width));
		if(EditorGUI.EndChangeCheck()) {
			sp.boolValue = toggle;
			changed = true;
		} else {
			changed = false;
		}
		return toggle;
	}

	int FindArrayIndex(SerializedProperty sp, string name, Object o) {
		for(int i = 0, n = sp.arraySize; i < n; ++i) {
			var s = sp.GetArrayElementAtIndex(i);
			if(s.FindPropertyRelative(name).objectReferenceValue == o)
				return i;
		}
		return -1;
	}

	void FullConstraintsReorder(bool dumpDebug = false) {
		System.Text.StringBuilder debugSB = null;
		if(dumpDebug) {
			debugSB = new System.Text.StringBuilder("Pre-sort constraints order:\n");
			foreach(var c in target.constraints)
				debugSB.AppendLine(c.sourceJoint.name);
		}

		var skeletonRoot = m_spSkeletonRoot.objectReferenceValue as Transform;
		skeletonRoot.GetComponentsInChildren<Transform>(m_transformCache);

		var sortingTable = new SortedDictionary<int, RigMeister2000.ConstraintSet>();
		foreach(var c in target.constraints) {
			int index = 0;
			try {
				for(; index < m_transformCache.Count && m_transformCache[index] != c.sourceJoint; ++index)
					;

				sortingTable.Add(index, c);
			} catch(System.Exception) {
				Debug.LogErrorFormat("{0} ERROR! Duplicate joint/index found while re-ordering (talk to someone): ", index, sortingTable[index].sourceJoint.name);
			}
		}

		int newIdx = 0;
		foreach(var c in sortingTable.Values)
			target.constraints[newIdx++] = c;

		if(dumpDebug) {
			debugSB.Append("\nPost-sort constraints order:\n");
			foreach(var c in target.constraints)
				debugSB.AppendLine(c.sourceJoint.name);

			Debug.Log(debugSB.ToString());
		}

		serializedObject.Update();
	}

	#region Gizmo Rendering
	static Color[]	ms_axisColor = new[] { Color.red, Color.green, Color.blue };
	void OnSceneGUI() {
		if(!target.enabled)
			return;

		if(target.debugShowActiveJointGizmo && m_jointSelected) {
			Handles.color = Color.white;
			Handles.RectangleCap(-1, m_jointSelected.position, Camera.current ? Camera.current.transform.rotation : m_jointSelected.rotation, 0.05f);
		}

		if(!target.debugShowConstraintsGizmos || target.constraints == null)
			return;

		foreach(var constraint in target.constraints) {
			var sourcePos = constraint.sourceJoint.position;

			if(constraint.usePositionConstraint) try {
				var pc = constraint.positionConstraint;
				//var weightedTargetPos = RigMeister2000.ConstraintWeightedTarget.GetWeightedPosition(pc.weightedTargetJoints);
				//var sourceToTarget = weightedTargetPos - sourcePos;

				if(pc.weightedTargetJoints.Length > 1) {
					foreach(var wtj in pc.weightedTargetJoints) {
						Handles.color = Color.cyan;
						Handles.DrawLine(sourcePos, wtj.joint.position);
						Handles.color = Color.magenta;
						Handles.CubeCap(-1, wtj.joint.position, Quaternion.identity, 0.025f);
					}
				}

				Handles.color = Color.cyan;
				Handles.CubeCap(-1, sourcePos, Quaternion.identity, 0.025f);
			} catch(System.Exception) {}

			if(constraint.useOrientationConstraint) try {
				var oc = constraint.orientationConstraint;
				var weightedTargetOri = RigMeister2000.ConstraintWeightedTarget.GetWeightedOrientation(oc.weightedTargetJoints, true);

				foreach(var wtj in oc.weightedTargetJoints) {
					Handles.color = Color.cyan;
					Handles.DrawLine(sourcePos, wtj.joint.position);
					Handles.color = Color.magenta;
					Handles.SphereCap(-1, wtj.joint.position, wtj.joint.rotation, 0.025f);
					Handles.ArrowCap(-1, wtj.joint.position, wtj.joint.rotation, 0.1f);
				}

				Handles.color = Color.cyan;
				Handles.SphereCap(-1, sourcePos, weightedTargetOri, 0.025f);
				Handles.ArrowCap(-1, sourcePos, weightedTargetOri, 0.1f);
			} catch(System.Exception) { }

			if(constraint.useLookAtConstraint) try {
				var lac = constraint.lookAtConstraint;
				var weightedTargetPos = RigMeister2000.ConstraintWeightedTarget.GetWeightedPosition(lac.weightedTargetJoints);
				var sourceToTarget = weightedTargetPos - sourcePos;
				var sourceTargetOri = Quaternion.LookRotation(sourceToTarget);

				Handles.color = ms_axisColor[(int)lac.sourceLookAtAxis];
				Handles.ConeCap(-1, sourcePos + sourceToTarget.normalized * 0.080f, sourceTargetOri, 0.0125f);
				Handles.ArrowCap(-1, sourcePos, sourceTargetOri, 0.1f);

				Handles.color = ms_axisColor[(int)lac.upSourceAxis];
				if(lac.upWorld) {
					Handles.ArrowCap(-1, sourcePos, Quaternion.LookRotation(Vector3.up), 0.1f);
				} else {
					var sourceToUp = lac.upJoint.position - sourcePos;
					Handles.ArrowCap(-1, sourcePos, Quaternion.LookRotation(sourceToUp), 0.1f);
				}
			} catch(System.Exception) {}
		}		
	}
	#endregion
}
