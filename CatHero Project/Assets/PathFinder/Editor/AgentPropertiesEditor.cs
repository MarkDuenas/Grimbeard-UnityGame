using UnityEngine;
using System.Collections;
using UnityEditor;
using System.Collections.Generic;
using System;


namespace K_PathFinder {
    [CustomEditor(typeof(AgentProperties))]
    public class AgentPropertiesEditor : Editor {
        #region general tab
        [SerializeField] bool generalTab = true;
        static GUIContent generalTabContent = new GUIContent("General", "Tab where you can change agent size, step height and max slope. And also setup what should be included or excluded from navmesh generation");

        SerializedProperty radius;
        static string radiusString = "radius";
        static GUIContent radiusContent = new GUIContent("Radius", "Agent radius in world space");

        SerializedProperty height;
        static string heightString = "height";
        static GUIContent heightContent = new GUIContent("Height", "Agent height in world space");

        SerializedProperty maxSlope;
        static string maxSlopeString = "maxSlope";
        static GUIContent maxSlopeContent = new GUIContent("Max slope", "Maximum slope agent can pass in degree");

        SerializedProperty maxStepHeight;
        static string maxStepHeightString = "maxStepHeight";
        static GUIContent maxStepHeightContent = new GUIContent("Step height", "Maximum step height in world space. Describe how much height difference agent can handle while moving up and down");


        SerializedProperty includedLayers;
        static string includedLayersString = "includedLayers";
        static GUIContent includedLayersContent = new GUIContent("Included Layers", "Physical layers that included into navmesh generation");

        SerializedProperty ignoredTags;
        static string ignoredTagsString = "ignoredTags";
        static GUIContent ignoredTagsContent = new GUIContent("Ignored Tags", "Excluded tags from navmesh generation. Recomend to put here things like Player or UI. Tags are always excluded even if object included into Layers tab");

        SerializedProperty checkHierarchyTag;
        static string checkHierarchyTagString = "checkHierarchyTag";
        static GUIContent checkHierarchyTagContent = new GUIContent("Use Hierarchy Tag", "If enabled then agent will also check all upper hierarchy if it tag in excluded tags list. Userful if you want to explude all childs of GameObject with specific tag");
        #endregion

        #region voxel tab
        [SerializeField] bool voxelTab = true;
        static GUIContent voxelTabContent = new GUIContent("Voxel", "Tab where you setup density of voxels for navmesh generation");

        SerializedProperty voxelsPerChunk;
        static string voxelsPerChunkString = "voxelsPerChunk";
        static GUIContent voxelsPerChunkContent = new GUIContent("Voxel Per Chunk Side", "Amount of voxel per chunk side. voxel size are chunk size / this number");

        SerializedProperty voxelBleed;
        static string voxelBleedString = "voxelBleed";
        static GUIContent voxelBleedContent = new GUIContent("Voxel Bleed Distance", "Height difference threshold between voxels when it tries to override passability and area to more prioritized one. If 2 objectswith different area?passability are this close then it's matter");
        #endregion

        #region movement tab
        [SerializeField] bool movementTab = true;
        static GUIContent movementTabContent = new GUIContent("Movement", "Tab where you setup movement aspect of this properties");

        SerializedProperty doNavMesh;
        static string doNavMeshString = "doNavMesh";
        static GUIContent doNavMeshContent = new GUIContent("Do NavMesh", "do NavMesh at all. (maybe you just need grid or covers?)");

        SerializedProperty walkMod;
        static string walkModString = "walkMod";
        static GUIContent walkModContent = new GUIContent("Walk Cost Modifier", "generic move cost modifyer. (maybe you need one?) 1f = move cost equal to distance of movement");

        SerializedProperty canCrouch;
        static string canCrouchString = "canCrouch";
        static GUIContent canCrouchContent = new GUIContent("Can Crouch", "If true then Pathfinder will add aditional area where agent can crouch");

        SerializedProperty crouchHeight;
        static string crouchHeightString = "crouchHeight";
        static GUIContent crouchHeightContent = new GUIContent("Crouch height", "lowest limit where crouch start in world units (upper obliviously is agent height)");

        SerializedProperty crouchMod;
        static string crouchModString = "crouchMod";
        static GUIContent crouchModContent = new GUIContent("Crouch Cost Modifier", "crouch move cost modifyer, 1f = move cost equal to distance of movement. Recomend to set it in respect how much it slower than normal move cost. 2 - 2.5 probably good number");

        SerializedProperty canJump;
        static string canJumpString = "canJump";
        static GUIContent canJumpContent = new GUIContent("Can jump", "If true then Pathfinder will add aditional info about jump spots");

        SerializedProperty JumpDown;
        static string JumpDownString = "JumpDown";
        static GUIContent JumpDownContent = new GUIContent("Max Jump Down Distance", "Max distance to jump down");

        SerializedProperty jumpDownMod;
        static string jumpDownModString = "jumpDownMod";
        static GUIContent jumpDownModContent = new GUIContent("Jump Down Cost", "Cost modifyer to jump down");

        SerializedProperty JumpUp;
        static string JumpUpString = "JumpUp";
        static GUIContent JumpUpContent = new GUIContent("Max Jump Up Distance", "Max distance to jump up");

        SerializedProperty jumpUpMod;
        static string jumpUpModString = "jumpUpMod";
        static GUIContent jumpUpModContent = new GUIContent("Jump Up Cost", "Cost modifyer to jump up");
        #endregion
        
        #region cover tab
        [SerializeField] bool coverTab = true;
        static GUIContent coverTabContent = new GUIContent("Covers", "Tab where you setup values for cover generation");

        SerializedProperty canCover;
        static string canCoverString = "canCover";
        static GUIContent canCoverContent = new GUIContent("Can Cover", "If true then Pathfinder will add aditional info about covers");

        SerializedProperty coverExtraSamples;
        static string coverExtraSamplesString = "coverExtraSamples";
        static GUIContent coverExtraSamplesContent = new GUIContent("Cover Extra Samples", "Cover on diagonal can be funky. Here you can add some extra samples into it");
        
        SerializedProperty fullCover;
        static string fullCoverString = "fullCover";
        static GUIContent fullCoverContent = new GUIContent("Cover Full", "How much height are considered as cover");

        SerializedProperty canHalfCover;
        static string canHalfCoverString = "canHalfCover";
        static GUIContent canHalfCoverContent = new GUIContent("Can Half-Cover", "should we add half covers?");

        SerializedProperty halfCover;
        static string halfCoverString = "halfCover";
        static GUIContent halfCoverContent = new GUIContent("Cover Half", "How much height are considered as half cover");
        #endregion

        #region sample points
        [SerializeField] bool samplePointsTab = true;
        static GUIContent samplePointsTabContent = new GUIContent("Sample points", "Tab where you setup values for sampling points generation");

        SerializedProperty samplePoints;
        static string samplePointsString = "samplePoints";
        static GUIContent samplePointsContent = new GUIContent("Sample Points", "If true then Pathfinder will sample points from navmesh ant put it as Cell content to later usage. Userful if you want to sample points in walkable proximity");
        
        SerializedProperty samplePointsDencity;
        static string samplePointsDencityString = "samplePointsDencity";
        static GUIContent samplePointsDencityContent = new GUIContent("Sample points density", "Size of space in foxels between sampled points. every this much voxel we sample data");
        #endregion

        #region other tab
        [SerializeField] bool otherTab = false;
        static GUIContent otherTabContent = new GUIContent("Other", "Tab for rare cases");

        SerializedProperty offsetMultiplier;
        static string offsetMultiplierString = "offsetMultiplier";
        static GUIContent offsetMultiplierContent = new GUIContent("Offset multiplier", "In order chunk to be more precise pathfinder must  take into account nearby obstacles outside chunk. This value will tell how much area it should take into account. 1 = agent radius");
        #endregion

        int setThisMuch = 2;

        GUIStyle textFieldStyles;
        //List<Vector3> list = new List<Vector3>();
   

        private void Awake() {
            textFieldStyles = new GUIStyle(EditorStyles.label);
            textFieldStyles.normal.textColor = Color.green;
        }


        private void OnEnable() {
            //general
            radius = serializedObject.FindProperty(radiusString);
            height = serializedObject.FindProperty(heightString);
            maxSlope = serializedObject.FindProperty(maxSlopeString);
            maxStepHeight = serializedObject.FindProperty(maxStepHeightString);
            includedLayers = serializedObject.FindProperty(includedLayersString);
            ignoredTags = serializedObject.FindProperty(ignoredTagsString);
            checkHierarchyTag = serializedObject.FindProperty(checkHierarchyTagString);
            //voxel
            voxelBleed = serializedObject.FindProperty(voxelBleedString);
            voxelsPerChunk = serializedObject.FindProperty(voxelsPerChunkString);

            //movement
            doNavMesh = serializedObject.FindProperty(doNavMeshString);
            walkMod = serializedObject.FindProperty(walkModString);
            canCrouch = serializedObject.FindProperty(canCrouchString);
            crouchHeight = serializedObject.FindProperty(crouchHeightString);
            crouchMod = serializedObject.FindProperty(crouchModString);
            canJump = serializedObject.FindProperty(canJumpString);
            JumpDown = serializedObject.FindProperty(JumpDownString);
            jumpDownMod = serializedObject.FindProperty(jumpDownModString);
            JumpUp = serializedObject.FindProperty(JumpUpString);
            jumpUpMod = serializedObject.FindProperty(jumpUpModString);

            //cover
            canCover = serializedObject.FindProperty(canCoverString);
            coverExtraSamples = serializedObject.FindProperty(coverExtraSamplesString);
            fullCover = serializedObject.FindProperty(fullCoverString);
            canHalfCover = serializedObject.FindProperty(canHalfCoverString);
            halfCover = serializedObject.FindProperty(halfCoverString);

            //battle grid
            samplePoints = serializedObject.FindProperty(samplePointsString);
            samplePointsDencity = serializedObject.FindProperty(samplePointsDencityString);

            //other tab
            offsetMultiplier = serializedObject.FindProperty(offsetMultiplierString);
        }


        public override void OnInspectorGUI() {
            AgentProperties myTarget = (AgentProperties)target;
   
            if (myTarget == null)
                return;

            float currentLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 130;
    
            UITools.LineHorizontal();
            generalTab = EditorGUILayout.Foldout(generalTab, generalTabContent);
            if (generalTab) {
                Rect rect = EditorGUILayout.GetControlRect(false, 100);
                //GUI.Box(rect, string.Empty);

                #region nice drawing
                float rectHeight = rect.height * 0.7f;
                float rectheightHalf = rectHeight * 0.5f;
                float rectheightQuarter = rectheightHalf * 0.5f;
                Vector2 rectCenter = rect.center;

                float agentWidth = (radius.floatValue / height.floatValue) * rectHeight * 0.8f;

                Vector2 leftBottom = new Vector2(rectCenter.x + agentWidth + rectheightQuarter, rectCenter.y + rectheightHalf);
                Vector2 rightBotton = new Vector2(rectCenter.x - agentWidth - rectheightQuarter, rectCenter.y + rectheightHalf);

                Handles.color = Color.black;

                float slope = maxSlope.floatValue;
                Vector2 slopeVector = new Vector2(
                    (float)Math.Cos(slope * Math.PI / 180) * (rectHeight * 0.9f),
                    (float)Math.Sin(slope * Math.PI / 180) * (rectHeight * 0.9f));

                slopeVector = new Vector2(-slopeVector.x, slopeVector.y);

                
                GUI.Label(new Rect((leftBottom - (slopeVector * 0.15f)) - (new Vector2(0, 15)), new Vector2(100, 20)), string.Format("Slope {0}", maxSlope.floatValue));


                float stepHeight = (maxStepHeight.floatValue / height.floatValue) * rectHeight;

                Handles.DrawAAPolyLine(
                    3,
                    leftBottom - slopeVector,
                    leftBottom,
                    rightBotton, 
                    rightBotton - new Vector2(0, stepHeight), 
                    rightBotton - new Vector2(50, stepHeight));

                GUI.Label(new Rect(rightBotton - new Vector2(50, stepHeight + 20), new Vector2(100, 20)), string.Format("Step {0}", maxStepHeight.floatValue));
                
                //Vector2 leftStart = rectCenter - new Vector2(rectheightQuarter, rectheightQuarter);
                //Vector2 leftUp = leftStart + new Vector2(0, rectheightHalf);               

                Color hColor = Handles.color;
                Handles.color = Color.black;

                Color niceColor = new Color(135f / 255f, 206f / 255f, 250f / 255f, 1f);

                Handles.color = niceColor;
                Handles.DrawSolidDisc(new Vector2(rectCenter.x, rectCenter.y - rectheightHalf), new Vector3(0f, -1f, 0.5f), agentWidth);
                Handles.DrawSolidDisc(new Vector2(rectCenter.x, rectCenter.y + rectheightHalf), new Vector3(0f, 1f, 0.5f), agentWidth);
                EditorGUI.DrawRect(new Rect(
                    rectCenter.x - agentWidth, 
                    rectCenter.y - rectheightHalf, 
                    agentWidth + agentWidth, 
                    rectheightHalf * 2), niceColor);     

                Vector2 rectCenterSlightlyOffseted = new Vector2(rectCenter.x - 0.5f, rectCenter.y);

                textFieldStyles.normal.textColor = Color.red;
                GUI.Label(new Rect(rectCenter.x + agentWidth, rectCenter.y, 100, 20), string.Format("Radius {0}", radius.floatValue), textFieldStyles);

                textFieldStyles.normal.textColor = Color.blue;
                GUI.Label(new Rect(rectCenter.x + agentWidth, rectCenter.y - rectheightHalf, 100, 20), string.Format("Height {0}", height.floatValue), textFieldStyles);

                //outlines
                Handles.color = Color.black;
                Handles.DrawWireDisc(new Vector2(rectCenterSlightlyOffseted.x, rectCenterSlightlyOffseted.y - rectheightHalf), new Vector3(0f, 1f, 0.5f), agentWidth);
                Handles.DrawWireDisc(new Vector2(rectCenterSlightlyOffseted.x, rectCenterSlightlyOffseted.y - rectheightHalf), new Vector3(0f, -1f, 0.5f), agentWidth);
                Handles.DrawWireDisc(new Vector2(rectCenterSlightlyOffseted.x, rectCenterSlightlyOffseted.y + rectheightHalf), new Vector3(0f, 1f, 0.5f), agentWidth);

                Handles.DrawLine(
                    new Vector2(rectCenterSlightlyOffseted.x - agentWidth, rectCenterSlightlyOffseted.y - rectheightHalf),
                    new Vector2(rectCenterSlightlyOffseted.x - agentWidth, rectCenterSlightlyOffseted.y + rectheightHalf));

                Handles.DrawLine(
                    new Vector2(rectCenterSlightlyOffseted.x + agentWidth, rectCenterSlightlyOffseted.y - rectheightHalf),
                    new Vector2(rectCenterSlightlyOffseted.x + agentWidth, rectCenterSlightlyOffseted.y + rectheightHalf));


                Handles.color = Color.red;
                Handles.DrawWireDisc(rectCenterSlightlyOffseted, new Vector3(0f, 1f, 0.5f), agentWidth);
                Handles.DrawWireDisc(rectCenterSlightlyOffseted + Vector2.up, new Vector3(0f, 1f, 0.5f), agentWidth);
                Handles.DrawWireDisc(rectCenterSlightlyOffseted + Vector2.down, new Vector3(0f, 1f, 0.5f), agentWidth);

                Handles.color = hColor;
                #endregion
                
                EditorGUILayout.PropertyField(radius, radiusContent);
                EditorGUILayout.PropertyField(height, heightContent);
                EditorGUILayout.PropertyField(maxSlope, maxSlopeContent);
                EditorGUILayout.PropertyField(maxStepHeight, maxStepHeightContent);
                EditorGUILayout.PropertyField(includedLayers, includedLayersContent);
                EditorGUILayout.PropertyField(ignoredTags, ignoredTagsContent, true);
                if(ignoredTags.arraySize > 0)
                    EditorGUILayout.PropertyField(checkHierarchyTag, checkHierarchyTagContent);
                
                if (maxStepHeight.floatValue > height.floatValue) 
                    maxStepHeight.floatValue = height.floatValue;     
                
                if(canCrouch.boolValue && maxStepHeight.floatValue > crouchHeight.floatValue)
                    maxStepHeight.floatValue = crouchHeight.floatValue;

                if (radius.floatValue < 0.001f)
                    radius.floatValue = 0.001f;

                if (height.floatValue < 0.001f)
                    height.floatValue = 0.001f;

                if (maxStepHeight.floatValue < 0.001f)
                    maxStepHeight.floatValue = 0.001f;

                if (includedLayers.intValue == 0) {  //big warning in case no layers sellected
                    rect = EditorGUILayout.GetControlRect(false, 30);
                    Color gColor = GUI.color;
                    GUI.color = Color.red;
                    GUI.Box(rect, new GUIContent("No layers sellected", "Sellect at least something in included Layers"));
                    GUI.color = gColor;
                }

                if (maxStepHeight.floatValue < (PathFinder.gridSize / myTarget.voxelsPerChunk)) {  //big warning in case no layers sellected
                    rect = EditorGUILayout.GetControlRect(false, 30);
                    Color gColor = GUI.color;
                    GUI.color = Color.yellow;
                    GUI.Box(rect, new GUIContent("Step height is very low", "Agent probably will have incorrect navmesh generation if it that low. Recomend at least greater than voxel size"));
                    GUI.color = gColor;
                }
            }

            UITools.LineHorizontal();

            voxelTab = EditorGUILayout.Foldout(voxelTab, voxelTabContent);

            if (voxelTab) {
                EditorGUILayout.PropertyField(voxelBleed, voxelBleedContent);
                EditorGUILayout.PropertyField(voxelsPerChunk, voxelsPerChunkContent); 

                float foxelSize = PathFinder.gridSize / myTarget.voxelsPerChunk;
                EditorGUILayout.LabelField("Voxel Size", foxelSize.ToString());

                EditorGUILayout.LabelField("Voxel Per Radius", ((int)(myTarget.radius / foxelSize)).ToString());

                GUILayout.BeginHorizontal();
                setThisMuch = EditorGUILayout.IntField(setThisMuch, GUILayout.MaxWidth(100));
                if (GUILayout.Button("Set this much")) {
                    voxelsPerChunk.intValue = Mathf.CeilToInt(PathFinder.gridSize / (myTarget.radius / setThisMuch));
                }

                if (voxelsPerChunk.intValue < setThisMuch)
                    voxelsPerChunk.intValue = voxelsPerChunk.intValue + 1;

                GUILayout.EndHorizontal();

                if (voxelsPerChunk.intValue < 10)
                    voxelsPerChunk.intValue = 10;

                if ((int)(myTarget.radius / foxelSize) <= 0) {  //big warning in case no layers sellected
                    Rect rect = EditorGUILayout.GetControlRect(false, 30);
                    Color gColor = GUI.color;
                    GUI.color = Color.red;
                    GUI.Box(rect, new GUIContent("Voxel per radius is 0", "Increase amount of voxels per radius. Recomend at least 2"));
                    GUI.color = gColor;
                }
            }

            UITools.LineHorizontal();

            movementTab = EditorGUILayout.Foldout(movementTab, movementTabContent);
            if (movementTab) {
                EditorGUILayout.PropertyField(doNavMesh, doNavMeshContent);
                if (doNavMesh.boolValue) {
                    EditorGUILayout.PropertyField(walkMod, walkModContent);
                    EditorGUILayout.PropertyField(canCrouch, canCrouchContent);

                    if (canCrouch.boolValue) {
                        EditorGUILayout.PropertyField(crouchHeight, crouchHeightContent);
                        EditorGUILayout.PropertyField(crouchMod, crouchModContent);

                        if (crouchHeight.floatValue < 0)
                            crouchHeight.floatValue = 0;

                        if (crouchHeight.floatValue > height.floatValue)
                            crouchHeight.floatValue = height.floatValue;
                    }

                    EditorGUILayout.PropertyField(canJump, canJumpContent);

                    if (canJump.boolValue) {
                        EditorGUILayout.PropertyField(JumpDown, JumpDownContent);
                        EditorGUILayout.PropertyField(jumpDownMod, jumpDownModContent);
                        EditorGUILayout.PropertyField(JumpUp, JumpUpContent);
                        EditorGUILayout.PropertyField(jumpUpMod, jumpUpModContent);
                    }
                }
            }

            UITools.LineHorizontal();
            coverTab = EditorGUILayout.Foldout(coverTab, coverTabContent);
            if (coverTab) {
                EditorGUILayout.PropertyField(canCover, canCoverContent);
                if (canCover.boolValue) {
                    EditorGUILayout.PropertyField(fullCover, fullCoverContent);
                    if (fullCover.floatValue < 0)
                        fullCover.floatValue = 0;

                    EditorGUILayout.PropertyField(canHalfCover, canHalfCoverContent);

                    if (canHalfCover.boolValue) {
                        EditorGUILayout.PropertyField(halfCover, halfCoverContent);

                        halfCover.floatValue = SomeMath.Clamp(0, fullCover.floatValue, halfCover.floatValue);

                        if (fullCover.floatValue < halfCover.floatValue)
                            fullCover.floatValue = halfCover.floatValue;
                    }

                    EditorGUILayout.PropertyField(coverExtraSamples, coverExtraSamplesContent);
                    if (coverExtraSamples.intValue < 0)
                        coverExtraSamples.intValue = 0;
                }
            }

            UITools.LineHorizontal();

            samplePointsTab = EditorGUILayout.Foldout(samplePointsTab, samplePointsTabContent);
            if (samplePointsTab) {
                EditorGUILayout.PropertyField(samplePoints, samplePointsContent);
                if (samplePoints.boolValue) {
                    EditorGUILayout.PropertyField(samplePointsDencity, samplePointsDencityContent);
                }
            }

            UITools.LineHorizontal();

            otherTab = EditorGUILayout.Foldout(otherTab, otherTabContent);
            if (otherTab) {
                EditorGUILayout.PropertyField(offsetMultiplier, offsetMultiplierContent);
            }

            EditorGUIUtility.labelWidth = currentLabelWidth;
            serializedObject.ApplyModifiedProperties();


        }        
    }
}


