using K_PathFinder.Settings;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace K_PathFinder.Instruction {
    public class ManualData : ScriptableObject {
        public GUISkin skin;

        public enum SmallDataType {
            Text, Picture, Box
        }

        const string namesArrayFormat = "•   {0}";


        public bool allowEditMode = true;

        [SerializeField] public string[] stringArray = new string[16];
        [SerializeField] public int stringArrayFilled = 0;
        [SerializeField] public int[] stringArrayFreeStack = new int[16];
        [SerializeField] public int stringArrayFreeStackCount = 0;

        [SerializeField] public Texture2D[] textureArray = new Texture2D[16];
        [SerializeField] public int textureArrayFilled = 0;
        [SerializeField] public int[] textureArrayFreeStack = new int[16];
        [SerializeField] public int textureArrayFreeStackCount = 0;
        
        [SerializeField]
        public List<ManualSmallDataCathegory> data;
        
        [Serializable]
        public class ManualSmallDataCathegory : ISerializationCallbackReceiver {
            [SerializeField] public string header;
            [SerializeField] public bool isOpen;
            [SerializeField] public int sellection = -1;
            [SerializeField] public List<ManualSmallData> data;
            [NonSerialized] public string[] names;
            [NonSerialized] public Rect lastRect;

            public void OnAfterDeserialize() {
                if (data == null)
                    data = new List<ManualSmallData>();
                ResetNames();
            }

            public void OnBeforeSerialize() {
                //do nothing
            }

            public void ResetNames() {
                names = new string[data.Count];
                for (int i = 0; i < data.Count; i++) {
                    names[i] = string.Format(namesArrayFormat, data[i].header)  ;
                }
            }
        }
        
        [Serializable]
        public class ManualSmallData : ISerializationCallbackReceiver {
            [SerializeField] public string header;
            [SerializeField] public Vector2 scroll;
            [SerializeField] public List<ManualSmallDataContent> contents = new List<ManualSmallDataContent>();

            public ManualSmallData() {    
                contents = new List<ManualSmallDataContent>();
            }

            public void OnAfterDeserialize() {
                if (contents == null)
                    contents = new List<ManualSmallDataContent>();           
            }

            public void OnBeforeSerialize() {
                //do nothing
            }
        }      
        
        [Serializable]
        public struct ManualSmallDataContent {
            [SerializeField] public SmallDataType dataType;
            [SerializeField] public int value;

            public ManualSmallDataContent(SmallDataType dataType, int value) {
                this.dataType = dataType;
                this.value = value;
            }
        }

        public static ManualData LoadData() {
            string manualPath = string.Format("{0}/{1}/{2}.asset", new string[] {
                    PathFinderSettings.FindProjectPath(),
                    PathFinderSettings.EDITOR_FOLDER,
                    PathFinderSettings.MANUAL_ASSET_NAME });

            return AssetDatabase.LoadAssetAtPath<ManualData>(manualPath);
        }
        
        public int GetFreeStringIndex() {
            int result = stringArrayFreeStackCount > 0 ? stringArrayFreeStack[--stringArrayFreeStackCount] : stringArrayFilled++;
            if (result == stringArray.Length) {
                Array.Resize(ref stringArray, stringArray.Length * 2);
            }    
            return result;
        }
        public void ReturnFreeStringIndex(int index) {
            if (stringArrayFreeStack.Length == stringArrayFreeStackCount)
                Array.Resize(ref stringArrayFreeStack, stringArrayFreeStack.Length * 2);
            stringArrayFreeStack[stringArrayFreeStackCount++] = index;
            stringArray[index] = "";
        }

        public int GetFreeTextureIndex() {
            int result = textureArrayFreeStackCount > 0 ? textureArrayFreeStack[--textureArrayFreeStackCount] : textureArrayFilled++;
            if (result == textureArray.Length) {
                Array.Resize(ref textureArray, textureArray.Length * 2);
            }
            return result;
        }
        public void ReturnFreeTextureIndex(int index) {
            if (textureArrayFreeStack.Length == textureArrayFreeStackCount)
                Array.Resize(ref textureArrayFreeStack, textureArrayFreeStack.Length * 2);
            textureArrayFreeStack[textureArrayFreeStackCount++] = index;
            textureArray[index] = null;
        }
    }
}