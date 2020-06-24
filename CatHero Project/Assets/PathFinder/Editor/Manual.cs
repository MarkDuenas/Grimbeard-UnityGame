using K_PathFinder.Settings;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace K_PathFinder.Instruction {
    public class Manual : EditorWindow {    
        //int sellected = 0;
        //string[] s = new string[]{"General", "Agent and Properties", "Results", "Main menu", "Local Avoidance", "Features and Limitations"};
        //Vector2 generalScroll, agentAndPropertiesScroll, pathScroll, settingsScroll, dynamicObstaclesScroll, featuresAndLimitationsScroll;

        const float leftTab = 0.2f;
        ManualData data;

        Vector2 leftScroll;

        string closedCathegoryFormat = "→ {0}";
        string openCathegoryFormat = "↓ {0}";
        string removeString = "Remove";
        string bigHeaderLayout = "<b><size=20>{0}</size></b>";

        //edit stuff
        bool editMode = false;
        ManualData.ManualSmallDataCathegory editCathegory = null;
        //edit stuff
        ManualData.ManualSmallData curData = null;


        void OnEnable() {
            data = ManualData.LoadData();
        }

        private void OnDestroy() {
            if(data != null)
                EditorUtility.SetDirty(data);
        }

        private void OnDisable() {
            if (data != null)
                EditorUtility.SetDirty(data);
        }

        [MenuItem(PathFinderSettings.UNITY_TOP_MENU_FOLDER + "/Info/Manual", false, 5)]
        public static void OpenWindow() {
            GetWindow<Manual>("PF Manual").Show();
        }
        
        void OnGUI() {
            if(data == null) {
                GUILayout.Label("For some reason PathFinder could not find it's manual. Please find manual asset and put reference here");
                data = (ManualData)EditorGUILayout.ObjectField(data, typeof(ManualData), true);
                return;
            }

            float screenWidth = Screen.width;
            float screenHeight = Screen.height - 20;

            if (data.allowEditMode) {
                editMode = GUI.Toggle(new Rect(screenWidth - 200, 0, 200, 20), editMode, "Edit Mode");    
            }

            float leftTabWidth = screenWidth * leftTab;    

            GUISkin curSkin = GUI.skin;
            GUI.skin = data.skin;
            
            Rect rectLeft = new Rect(0, 0, leftTabWidth, screenHeight);
            Rect rectRight = new Rect(leftTabWidth, 0, screenWidth - leftTabWidth, screenHeight);
            EditorGUI.DrawRect(new Rect(leftTabWidth - 1, 0, 2, Screen.height), new Color(.25f, .25f, .25f, 1));
            
            string someString;

            GUILayout.BeginArea(rectLeft);
            leftScroll = GUILayout.BeginScrollView(leftScroll);

            var dataOfData = data.data;

            Color guiColor = GUI.color;

            for (int groupID = 0; groupID < dataOfData.Count; groupID++) {
                var curGroup = dataOfData[groupID];
                GUI.color = Color.white;
                data.skin.button.fontStyle = FontStyle.Bold;
                if (GUILayout.Button(string.Format(curGroup.isOpen ? openCathegoryFormat : closedCathegoryFormat, curGroup.header))) 
                    curGroup.isOpen = !curGroup.isOpen;

                if (editMode) 
                    curGroup.lastRect = GUILayoutUtility.GetLastRect();
                data.skin.button.fontStyle = FontStyle.Normal;
                
                GUI.color = guiColor;
                if (curGroup.isOpen) {
                    //var curGroupData = curGroup.data;

                    int curSellection = curGroup.sellection;
                    EditorGUI.BeginChangeCheck();
                    curSellection = GUILayout.SelectionGrid(curGroup.sellection, curGroup.names, 1);
                    if (EditorGUI.EndChangeCheck()) {
                        foreach (var item in dataOfData) {
                            item.sellection = -1;
                        }
                        curGroup.sellection = curSellection;
                        curData = curGroup.data[curSellection];
                        editCathegory = null;
                    }
                }
            }
            GUI.color = guiColor;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
          
            if (editMode) {
                int addAfter = -1;
                int removeTarget = -1;
                for (int groupID = 0; groupID < dataOfData.Count; groupID++) {
                    var curGroup = dataOfData[groupID];
                    Rect lastRect = curGroup.lastRect;
                    if (GUI.Button(new Rect(lastRect.x + leftTabWidth, lastRect.y - leftScroll.y, leftTabWidth * 0.2f, lastRect.height), "Edit")) {
                        editCathegory = curGroup;
                    }
                    //if (GUI.Button(new Rect(lastRect.x + leftTabWidth + (leftTabWidth * 0.15f), lastRect.y, leftTabWidth * 0.1f, lastRect.height), "+")) {
                    //    addAfter = groupID;
                    //}
                    //if (GUI.Button(new Rect(lastRect.x + leftTabWidth + (leftTabWidth * 0.15f) + (leftTabWidth * 0.1f), lastRect.y, leftTabWidth * 0.1f, lastRect.height), "-")) {
                    //    removeTarget = groupID;
                    //}
                }
                if(addAfter != -1) {
                    dataOfData.Insert(addAfter, new ManualData.ManualSmallDataCathegory());
                }
                if(removeTarget != -1) {
                    dataOfData.RemoveAt(removeTarget);
                }
            }

            if (editMode) {
                rectRight.x += leftTabWidth * 0.3f;
                rectRight.width -= leftTabWidth * 0.3f;
            }
            else {
                editCathegory = null;
            }

            GUILayout.BeginArea(rectRight);
            if (editCathegory != null) {
                GUILayout.Label("Header");

                someString = editCathegory.header;
                EditorGUI.BeginChangeCheck();
                someString = GUILayout.TextField(someString);
                if (EditorGUI.EndChangeCheck()) 
                    editCathegory.header = someString;

                if (GUILayout.Button("Add Content")) {
                    editCathegory.data.Add(new ManualData.ManualSmallData());
                    editCathegory.ResetNames();
                }

                int remove = -1;
                for (int i = 0; i < editCathegory.data.Count; i++) {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(removeString))
                        remove = i;

                    someString = editCathegory.data[i].header;
                    EditorGUI.BeginChangeCheck();
                    someString = GUILayout.TextField(someString);
                    if (EditorGUI.EndChangeCheck()) {
                        editCathegory.data[i].header = someString;
                        editCathegory.ResetNames();
                    }

                    GUILayout.EndHorizontal();
                }
                if(remove != -1) {
                    foreach (var item in editCathegory.data[remove].contents) {
                        switch (item.dataType) {
                            case ManualData.SmallDataType.Text:
                                data.ReturnFreeStringIndex(item.value);
                                break;
                            case ManualData.SmallDataType.Picture:
                                data.ReturnFreeTextureIndex(item.value);
                                break;
                        }
                    }
                    editCathegory.data.RemoveAt(remove);
                    editCathegory.ResetNames();         
                    Repaint();
                }
            }
            else {
                if (curData == null) {
                    dataOfData[0].sellection = 0;
                    curData = dataOfData[0].data[0];
                }

                int removeContent = -1;
                int insertAt = -1;
                ManualData.SmallDataType insertType = ManualData.SmallDataType.Text;
           

                curData.scroll = GUILayout.BeginScrollView(curData.scroll);
                GUILayout.Label(string.Format(bigHeaderLayout, curData.header));

                var contents = curData.contents;
                for (int i = 0; i < curData.contents.Count; i++) {
                    var curContent = contents[i];

                    if (editMode) {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("Add Label")) {
                            insertAt = i;
                            insertType = ManualData.SmallDataType.Text;
                        }
                        if (GUILayout.Button("Add Box")) {
                            insertAt = i;
                            insertType = ManualData.SmallDataType.Box;
                        }
                        if (GUILayout.Button("Add Picture")) {
                            insertAt = i;
                            insertType = ManualData.SmallDataType.Picture;
                        }

                        if (GUILayout.Button("Remove"))
                            removeContent = i;
                        GUILayout.EndHorizontal();
                    }    

                    if(curContent.dataType == ManualData.SmallDataType.Text | curContent.dataType == ManualData.SmallDataType.Box) {
                        if (editMode) {
                            someString = data.stringArray[curContent.value];
                            EditorGUI.BeginChangeCheck();
                            someString = GUILayout.TextArea(someString);
                            if (EditorGUI.EndChangeCheck())
                                data.stringArray[curContent.value] = someString;
                        }
                        else {
                            string val = data.stringArray[curContent.value];

                            if(curContent.dataType == ManualData.SmallDataType.Text)
                                GUILayout.Label(val);
                            else
                                GUILayout.Box(val);           
                        }
                    }
                    else if(curContent.dataType == ManualData.SmallDataType.Picture) {
                        if (editMode) {
                            data.textureArray[curContent.value] = (Texture2D)EditorGUILayout.ObjectField(data.textureArray[curContent.value], typeof(Texture2D), false);
                        }

                        GUILayout.Label(data.textureArray[curContent.value], GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
                    }
                    else {
                        Debug.LogError("i dont know that type");
                    } 
                }

                if (removeContent != -1) {
                    var curContent = contents[removeContent];
                    if(curContent.dataType == ManualData.SmallDataType.Box | curContent.dataType == ManualData.SmallDataType.Text) {
                        data.ReturnFreeStringIndex(curContent.value);
                    }
                    else if (curContent.dataType == ManualData.SmallDataType.Picture) {
                        data.ReturnFreeTextureIndex(curContent.value);
                    }
                    else {
                        Debug.LogError("there is nothing of this type");
                    }
                       
                    curData.contents.RemoveAt(removeContent);
                }

                if(insertAt != -1) {
                    int valueIndex = -1;
                    if (insertType == ManualData.SmallDataType.Box | insertType == ManualData.SmallDataType.Text) {
                        valueIndex = data.GetFreeStringIndex();
                    }
                    else if (insertType == ManualData.SmallDataType.Picture) {
                        valueIndex = data.GetFreeTextureIndex();
                    }
                    else {
                        Debug.LogError("there is nothing of this type");
                    }
                    curData.contents.Insert(insertAt, new ManualData.ManualSmallDataContent(insertType, valueIndex));
                }

                if (editMode) {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Add Label"))
                        curData.contents.Add(new ManualData.ManualSmallDataContent(ManualData.SmallDataType.Text, data.GetFreeStringIndex()));
                    if (GUILayout.Button("Add Box"))
                        curData.contents.Add(new ManualData.ManualSmallDataContent(ManualData.SmallDataType.Box, data.GetFreeStringIndex()));
                    if (GUILayout.Button("Add Picture"))
                        curData.contents.Add(new ManualData.ManualSmallDataContent(ManualData.SmallDataType.Picture, data.GetFreeTextureIndex()));
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }

            GUILayout.EndArea();
            GUI.skin = curSkin;

            if (GUI.changed) {
                EditorUtility.SetDirty(data);
            }
        }
    }
}