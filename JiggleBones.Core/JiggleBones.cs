using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using KKAPI.Utilities;
using Studio;
using System;
using ToolBox.Extensions;
using MessagePack;
using KKAPI.Studio.SaveLoad;
using System.Collections;
using Vectrosity;
using static NodesConstraints.NodesConstraints;
using System.IO;
using System.Text;
using System.Xml;

namespace JiggleBones 
{
	[BepInPlugin(GUID, PluginName, Version)]
    [BepInProcess(KK_Plugins.Constants.StudioProcessName)]
    [BepInDependency(KKAPI.KoikatuAPI.GUID, KKAPI.KoikatuAPI.VersionConst)]
    [BepInDependency(NodesConstraints.NodesConstraints.GUID, NodesConstraints.NodesConstraints.Version)]
    public class JiggleBones : BaseUnityPlugin 
	{
		#region CONFIG VARIABLES

        // old guid so my private scenes work again :(
        public const string GUID = "com.shallty.SpringBones";
        public const string PluginName = "Jiggle Bones";
#if KK
        public const string PluginNameInternal = "KK_JiggleBones";
#elif KKS
        public const string PluginNameInternal = "KKS_JiggleBones";
#elif HS2
        public const string PluginNameInternal = "HS2_JiggleBones";
#endif
		private int WindowsUniqueId = GUID.GetHashCode();
        public const string Version = "1.0";
        internal static new ManualLogSource Logger;

        #endregion CONFIG VARIABLES

        #region VARIABLES

        private static ConfigEntry<string> presetsPath;
        private static ConfigEntry<KeyboardShortcut> keyShortcut;
        private static ConfigEntry<Rect> windowRect;


        private RectTransform _mainWindowBackground;
        private static bool toggleUI = false;
        private static Color defColor = GUI.color;
        private static Vector2 boneSearchScroll;
        private static Vector2 jiggleBonesSearchScroll;
        private static Vector2 colliderSearchScroll;
        private static Vector2 colliderEditScroll;
        private static Vector2 presetsSearchScroll;
        private static HashSet<TreeNodeObject> selectedNodes;
        private static ObjectCtrlInfo selectedOci;
        private static GuideObject selectedGuideObject;
        private static GuideObject lastSelectedGuideObject;
        private static Action <List<Transform>> selectMulitpleBones;
        private static Action<List<JiggleBone>> selectMultipleJiggleBones;
        private static Action<List<KeyValuePair<ObjectCtrlInfo, DynamicBoneCollider>>> selectMultipleColliders;
        private static GUIStyle headStyle;
        private static GUIStyle subHeadStyle;

        public static HashSet<Transform> selectedBones = new HashSet<Transform>();
        private static readonly HashSet<GameObject> openedBones = new HashSet<GameObject>();

        public static HashSet<KeyValuePair<ObjectCtrlInfo, DynamicBoneCollider>> selectedColliders = new HashSet<KeyValuePair<ObjectCtrlInfo, DynamicBoneCollider>>();
        public static HashSet<KeyValuePair<ObjectCtrlInfo, DynamicBoneCollider>> currentColliders = new HashSet<KeyValuePair<ObjectCtrlInfo, DynamicBoneCollider>>();

        private static string presetName = "";

        private static string bonesSearch = "";
        private static string jiggleBonesSearch = "";
        private static string collidersSearch = "";
        private static string presetsSearch = "";

        private static OCIFolder rootFolder;
        private static float offsetIncrement = 0.01f;

        public static List<JiggleBone> allJiggleBones = new List<JiggleBone>();
        public static HashSet<JiggleBone> selectedJiggleBones = new HashSet<JiggleBone>();

        internal static NodesConstraints.NodesConstraints _nodeConstraints;

        public static string selectedPreset = "";
        public static List<string> presetsList = new List<string>();

        public static List<JiggleBonePreset> loadedPresetData = new List<JiggleBonePreset>();

        public static bool isLoadingPreset = false;
        public static bool isCreatingPreset = false;
        public static bool showAllJiggleBones = false;
        public static WindowTab windowTab = WindowTab.Bones;

        private static VectorLine _selectedCircle;
        private static Texture centerTexture;

        #endregion

        public enum WindowTab
        {
            Bones = 0,
            JiggleBones = 1,
            Colliders = 2,
            Presets = 3
        }

        public class JiggleBonePreset
        {
            public ObjectCtrlInfo oci;

            public int ociId;
            public int ociKind;
            public string ociName;
            public List<KeyValuePair<string, JiggleBoneData>> jiggleBones;
        }


        [MessagePackObject]
        public class JiggleBoneData
        {
            [Key(0)]
            public int mode;
            [Key(1)]
            public float radiusLimit;
            [Key(2)]
            public float frequencyHz;
            [Key(3)]
            public float dampingRatio;
            [Key(4)]
            public float halfLife;
            [Key(5)]
            public float[] offset;
            [Key(6)]
            public float? colliderRadius;
            [Key(7)]
            public List<KeyValuePair<int, string>> colliders = new List<KeyValuePair<int, string>>();
            [Key(8)]
            public int? idOci;
            [Key(9)]
            public int? idParent;
            [Key(10)]
            public int? idChild;
            [Key(11)]
            public bool? radiusEnabled;
        }

        public class JiggleBone
        {
            public PointConstraint constraint;

            public ObjectCtrlInfo oci;

            public OCIFolder parent;
            public OCIItem child;

            public Constraint parentConstraint;
            public Constraint childConstraint;

            public JiggleBone (PointConstraint constraint, OCIFolder parent, OCIItem child, Constraint parentConstraint, Constraint childConstraint, ObjectCtrlInfo oci)
            {
                this.constraint = constraint;
                this.parent = parent;
                this.child = child;
                this.parentConstraint = parentConstraint;
                this.childConstraint = childConstraint;
                this.oci = oci;
            }

            public JiggleBone (PointConstraint constraint, OCIFolder parent, OCIItem child, ObjectCtrlInfo oci)
            {
                this.constraint = constraint;
                this.parent = parent;
                this.child = child;
                this.oci = oci;
            }

            public JiggleBone() { }

            public bool IsValid()
            {
                return !(constraint == null || parent == null || child == null || parentConstraint == null || childConstraint == null);
            }

        }

        private void Awake() 
		{
			#region CONFIG
            Logger = base.Logger;

            KeyboardShortcut _keyShortcut = new KeyboardShortcut(KeyCode.S, KeyCode.LeftShift);
            keyShortcut = Config.Bind("GENERAL", "Open UI shortcut", _keyShortcut, "Press this button to launch the UI.");

            windowRect = Config.Bind("GENERAL", "Window Dimensions", new Rect(200, 220, 530, 480), "Window position and size.");

            var default_path = Path.Combine(Path.GetFullPath(UserData.Path), "JiggleBones Presets");
            presetsPath = Config.Bind("GENERAL", "Presets Path", default_path, "Path to the JiggleBones presets folder.");

            ReloadPresetsList();

            #endregion CONFIG

            StudioSaveLoadApi.RegisterExtraBehaviour<JiggleBonesSaveData>(GUID);
            KKAPI.Studio.StudioAPI.StudioLoadedChanged += OnStudioLoaded;

            headStyle = new GUIStyle();
            headStyle.fontSize = 30;
            headStyle.alignment = TextAnchor.MiddleCenter;
            headStyle.fontStyle = FontStyle.Bold;
            headStyle.fixedHeight = 200f;
            headStyle.fixedWidth = 200f;
            headStyle.wordWrap = true;

            subHeadStyle = new GUIStyle();
            subHeadStyle.fontSize = 0;
            subHeadStyle.alignment = TextAnchor.MiddleCenter;
            subHeadStyle.fontStyle = FontStyle.Bold;
            subHeadStyle.normal.textColor = Color.white;
            subHeadStyle.hover.textColor = new Color(0.9f, 0.9f, 0.9f, 0.6f);
            subHeadStyle.wordWrap = true;
            subHeadStyle.clipping = TextClipping.Clip;
        }

        private void OnStudioLoaded(object sender, EventArgs e)
        {
            selectedNodes = Studio.Studio.Instance.treeNodeCtrl.hashSelectNode;

            _nodeConstraints = Singleton<NodesConstraints.NodesConstraints>.Instance;
            if (_nodeConstraints == null) Logger.LogError("NodeConstraints isn't instantiated!");

            _mainWindowBackground = IMGUIExtensions.CreateUGUIPanelForIMGUI();

            centerTexture = ResourceUtils.GetEmbeddedResource("centerCircle.png").LoadTexture();

            _selectedCircle = new VectorLine("JiggleBoneGizmos", new List<Vector3> { Vector3.zero }, 25f, LineType.Points);
            _selectedCircle.color = Color.cyan;
            _selectedCircle.texture = centerTexture;
            _selectedCircle.active = false;
        }

        private void Update() 
		{
            selectedOci = null;
            selectedGuideObject = null;
            TreeNodeObject treeNode = selectedNodes?.FirstOrDefault();
            if (treeNode != null)
            {
                if (Studio.Studio.Instance.dicInfo.TryGetValue(treeNode, out selectedOci))
                {
                    var lastSelectedJiggleBone = allJiggleBones.FirstOrDefault(x => x.parent.treeNodeObject == treeNode || x.child.treeNodeObject == treeNode);
                    if (lastSelectedJiggleBone != null)
                    {
                        selectedJiggleBones.Clear();
                        selectedJiggleBones.Add(lastSelectedJiggleBone);
                    }

                    selectedGuideObject = selectedOci.guideObject;

                    if (selectedGuideObject != null && selectedGuideObject != lastSelectedGuideObject)
                    {
                        selectedBones.Clear();
                        selectedColliders.Clear();
                        currentColliders.Clear();

                        if (!showAllJiggleBones)
                            selectedJiggleBones.Clear();

                        foreach (var c in selectedGuideObject.transformTarget.GetComponentsInChildren<DynamicBoneCollider>(true))
                            currentColliders.Add(new KeyValuePair<ObjectCtrlInfo, DynamicBoneCollider>(selectedOci, c));
                    }
                }
            }

            lastSelectedGuideObject = selectedGuideObject;

            if (keyShortcut.Value.IsDown())
            {
                toggleUI = !toggleUI;
            }

            if (toggleUI)
            {
                IMGUIExtensions.UpdateRepeat();
                if (_selectedCircle != null)
                {
                    if (windowTab == WindowTab.Bones)
                    {
                        _selectedCircle.points3 = selectedBones.Select(x => x.position).ToList();
                    }
                    else if (windowTab == WindowTab.JiggleBones || (windowTab == WindowTab.Presets && isCreatingPreset))
                    {
                        _selectedCircle.points3 = selectedJiggleBones.Select(x => x.child.guideObject.transformTarget.position).ToList();
                    }
                    else if (windowTab == WindowTab.Colliders)
                    {
                        _selectedCircle.points3 = selectedColliders.Select(x => x.Value.transform.position).ToList();
                    }
                    else
                    {
                        _selectedCircle.points3.Clear();
                    }

                    _selectedCircle.active = _selectedCircle.points3.Count > 0;
                    if (_selectedCircle.active)
                        _selectedCircle.Draw();        
                }
            }
            else
            {
                if (_selectedCircle != null)
                    _selectedCircle.active = false;
            }
        }

		private void OnGUI()
        {
            var skin = GUI.skin;
            GUI.skin = IMGUIUtils.SolidBackgroundGuiSkin;

            if (toggleUI)
            {
                if (_mainWindowBackground != null)
                {
                    IMGUIExtensions.DrawBackground(windowRect.Value);
                    _mainWindowBackground.gameObject.SetActive(true);
                    IMGUIExtensions.FitRectTransformToRect(_mainWindowBackground, windowRect.Value);
                }
                windowRect.Value = GUILayout.Window(WindowsUniqueId, windowRect.Value, WindowFunction, PluginName + "  " + Version);
            }
            else
            {
                if (_mainWindowBackground != null && _mainWindowBackground.gameObject.activeSelf)
                    _mainWindowBackground.gameObject.SetActive(false);
            }

            GUI.skin = skin;
        }

        private void WindowFunction(int WindowID)
        {
            GUI.color = defColor;
            GUI.enabled = true;

            if (GUI.Button(new Rect(windowRect.Value.width - 18, 0, 18, 18), "X")) toggleUI = false;

            GUILayout.BeginVertical(GUI.skin.box);

            #region WINDOW TABS

            GUILayout.BeginHorizontal(GUI.skin.box);

            GUI.color = windowTab == WindowTab.Bones ? Color.cyan : defColor;
            GUI.enabled = windowTab != WindowTab.Bones;
            if (GUILayout.Button("Bones"))
            {
                windowTab = WindowTab.Bones;
            }

            GUI.color = windowTab == WindowTab.JiggleBones ? Color.cyan : defColor;
            GUI.enabled = windowTab != WindowTab.JiggleBones;
            if (GUILayout.Button("JiggleBones"))
            {
                windowTab = WindowTab.JiggleBones;
            }

            GUI.color = windowTab == WindowTab.Colliders ? Color.cyan : defColor;
            GUI.enabled = windowTab != WindowTab.Colliders;
            if (GUILayout.Button("Colliders"))
            {
                windowTab = WindowTab.Colliders;
            }

            GUI.color = windowTab == WindowTab.Presets ? Color.cyan : defColor;
            GUI.enabled = windowTab != WindowTab.Presets;
            if (GUILayout.Button("Presets"))
            {
                windowTab = WindowTab.Presets;
                ReloadPresetsList();
            }

            GUI.enabled = true;
            GUI.color = defColor;

            GUILayout.EndHorizontal();

            #endregion

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUI.skin.box);

            #region RIGHT SIDE PANEL CONTENT

            #region TOP SEARCH BAR

            GUILayout.BeginHorizontal();

            string searchField = "";
            
            if (windowTab == WindowTab.Bones)
                searchField = bonesSearch;
            else if (windowTab == WindowTab.JiggleBones)
                searchField = jiggleBonesSearch;
            else if (windowTab == WindowTab.Colliders)
                searchField = collidersSearch;
            else if (windowTab == WindowTab.Presets)
                searchField = presetsSearch;

            string oldSearch = searchField;
            GUI.SetNextControlName("searchbar");
            GUILayout.Label("Search", GUILayout.ExpandWidth(false));
            searchField = GUILayout.TextField(searchField);
            if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                searchField = "";

            GUILayout.EndHorizontal();

            #endregion

            #region MIDDLE BOX CONTENTS

            if (windowTab == WindowTab.Bones)
            {
                if (oldSearch.Length != 0 && selectedBones.Count != 0 && (searchField.Length == 0 || (searchField.Length < oldSearch.Length && oldSearch.StartsWith(searchField))))
                {
                    foreach (Transform selectedBone in selectedBones)
                        if (selectedBone.name.IndexOf(oldSearch, StringComparison.OrdinalIgnoreCase) != -1)
                            OpenParents(selectedBone, selectedGuideObject.transformTarget);
                }

                bonesSearch = searchField;
                boneSearchScroll = GUILayout.BeginScrollView(boneSearchScroll, false, false, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.box);
                
                List<Transform> displayedBones = new List<Transform>();
                if (selectedGuideObject != null)
                    foreach (Transform t in selectedGuideObject.transformTarget)
                        DisplayObjectTree(t.gameObject, 0, ref displayedBones);

                selectMulitpleBones?.Invoke(displayedBones);

                GUILayout.EndScrollView();
            }
            else if (windowTab == WindowTab.JiggleBones)
            {
                jiggleBonesSearch = searchField;
                jiggleBonesSearchScroll = GUILayout.BeginScrollView(jiggleBonesSearchScroll, false, false, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.box);
                List<JiggleBone> displayedBones = new List<JiggleBone>();
                foreach (var jiggleBone in allJiggleBones)
                {
                    if (jiggleBone == null || jiggleBone.IsValid() == false) continue;

                    if (showAllJiggleBones == false && selectedOci != jiggleBone.oci) continue;

                    string name = showAllJiggleBones ? $"({jiggleBone.oci.treeNodeObject.textName}) {jiggleBone.childConstraint.childTransform.name}" : $"{jiggleBone.childConstraint.childTransform.name}";
                    if (name.IndexOf(jiggleBonesSearch, StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        displayedBones.Add(jiggleBone);
                        GUI.color = selectedJiggleBones.Contains(jiggleBone) ? Color.cyan : defColor;
                        if (GUILayout.Button(name))
                        {
                            if (Input.GetKey(KeyCode.LeftControl))
                            {
                                if (selectedJiggleBones.Contains(jiggleBone))
                                    selectedJiggleBones.Remove(jiggleBone);
                                else
                                    selectedJiggleBones.Add(jiggleBone);
                            }
                            else if (Input.GetKey(KeyCode.LeftShift) && selectedJiggleBones.Count > 0)
                            {
                                selectMultipleJiggleBones = (bones) =>
                                {
                                    int firstIndex = bones.IndexOf(selectedJiggleBones.First());
                                    int lastIndex = bones.IndexOf(jiggleBone);
                                    if (firstIndex != lastIndex)
                                    {
                                        int inc = firstIndex < lastIndex ? 1 : -1;
                                        for (var i = firstIndex; i != lastIndex; i += inc)
                                        {
                                            if (selectedJiggleBones.Contains(bones[i]) == false)
                                                selectedJiggleBones.Add(bones[i]);
                                        }
                                        if (selectedJiggleBones.Contains(jiggleBone) == false)
                                            selectedJiggleBones.Add(jiggleBone);
                                    }

                                    selectMultipleJiggleBones = null;
                                };
                            }
                            else
                            {
                                selectedJiggleBones.Clear();
                                selectedJiggleBones.Add(jiggleBone);
                            }

                            if (GUI.GetNameOfFocusedControl() == "searchbar")
                                GUI.FocusControl("");
                        }

                        GUI.color = defColor;
                    }
                }
                selectMultipleJiggleBones?.Invoke(displayedBones);

                GUILayout.EndScrollView();
            }
            else if (windowTab == WindowTab.Colliders)
            {
                collidersSearch = searchField;
                colliderSearchScroll = GUILayout.BeginScrollView(colliderSearchScroll, false, false, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.box);
                List<KeyValuePair<ObjectCtrlInfo, DynamicBoneCollider>> displayedColliders = new List<KeyValuePair<ObjectCtrlInfo, DynamicBoneCollider>>();
                foreach (var pair in currentColliders)
                {
                    var c = pair.Value;
                    if (c.gameObject.name.IndexOf(collidersSearch, StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        if (selectedColliders.Contains(pair)) GUI.color = Color.cyan;
                        displayedColliders.Add(pair);
                        if (GUILayout.Button(c.gameObject.name))
                        {
                            if (Input.GetKey(KeyCode.LeftControl))
                            {
                                if (selectedColliders.Contains(pair))
                                    selectedColliders.Remove(pair);
                                else
                                    selectedColliders.Add(pair);
                            }
                            else if (Input.GetKey(KeyCode.LeftShift) && selectedColliders.Count > 0)
                            {
                                selectMultipleColliders = (bones) =>
                                {
                                    int firstIndex = bones.IndexOf(selectedColliders.First());
                                    int lastIndex = bones.IndexOf(pair);
                                    if (firstIndex != lastIndex)
                                    {
                                        int inc = firstIndex < lastIndex ? 1 : -1;
                                        for (var i = firstIndex; i != lastIndex; i += inc)
                                        {
                                            if (selectedColliders.Contains(bones[i]) == false)
                                                selectedColliders.Add(bones[i]);
                                        }
                                        if (selectedColliders.Contains(pair) == false)
                                            selectedColliders.Add(pair);
                                    }

                                    selectMultipleColliders = null;
                                };
                            }
                            else
                            {
                                selectedColliders.Clear();
                                selectedColliders.Add(pair);
                            }

                            if (GUI.GetNameOfFocusedControl() == "searchbar")
                                GUI.FocusControl("");
                        }
                        GUI.color = defColor;
                    }
                }

                selectMultipleColliders?.Invoke(displayedColliders);

                GUILayout.EndScrollView();
            }
            else if (windowTab == WindowTab.Presets)
            {
                if (isCreatingPreset)
                {
                    GUILayout.BeginVertical(GUI.skin.box);

                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginVertical();
                    GUILayout.Label("Creating Preset:", subHeadStyle);
                    GUILayout.Label("Select which JiggleBones you want to save in the preset.", subHeadStyle);
                    GUILayout.EndVertical();
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.Space(10);

                    #region JiggleBones List

                    presetsSearch = searchField;
                    jiggleBonesSearchScroll = GUILayout.BeginScrollView(jiggleBonesSearchScroll, false, false, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.box);
                    List<JiggleBone> displayedBones = new List<JiggleBone>();
                    foreach (var jiggleBone in allJiggleBones)
                    {
                        if (jiggleBone == null || jiggleBone.IsValid() == false) continue;

                        string name = $"({jiggleBone.oci.treeNodeObject.textName}) {jiggleBone.childConstraint.childTransform.name}";
                        if (name.IndexOf(presetsSearch, StringComparison.OrdinalIgnoreCase) != -1)
                        {
                            displayedBones.Add(jiggleBone);
                            GUI.color = selectedJiggleBones.Contains(jiggleBone) ? Color.cyan : defColor;
                            if (GUILayout.Button(name))
                            {
                                if (Input.GetKey(KeyCode.LeftControl))
                                {
                                    if (selectedJiggleBones.Contains(jiggleBone))
                                        selectedJiggleBones.Remove(jiggleBone);
                                    else
                                        selectedJiggleBones.Add(jiggleBone);
                                }
                                else if (Input.GetKey(KeyCode.LeftShift) && selectedJiggleBones.Count > 0)
                                {
                                    selectMultipleJiggleBones = (bones) =>
                                    {
                                        int firstIndex = bones.IndexOf(selectedJiggleBones.First());
                                        int lastIndex = bones.IndexOf(jiggleBone);
                                        if (firstIndex != lastIndex)
                                        {
                                            int inc = firstIndex < lastIndex ? 1 : -1;
                                            for (var i = firstIndex; i != lastIndex; i += inc)
                                            {
                                                if (selectedJiggleBones.Contains(bones[i]) == false)
                                                    selectedJiggleBones.Add(bones[i]);
                                            }
                                            if (selectedJiggleBones.Contains(jiggleBone) == false)
                                                selectedJiggleBones.Add(jiggleBone);
                                        }

                                        selectMultipleJiggleBones = null;
                                    };
                                }
                                else
                                {
                                    selectedJiggleBones.Clear();
                                    selectedJiggleBones.Add(jiggleBone);
                                }

                                if (GUI.GetNameOfFocusedControl() == "searchbar")
                                    GUI.FocusControl("");
                            }

                            GUI.color = defColor;
                        }
                    }
                    selectMultipleJiggleBones?.Invoke(displayedBones);

                    GUILayout.EndScrollView();

                    #endregion

                    GUILayout.Space(10);

                    Dictionary<ObjectCtrlInfo, int> jiggleBoneCount = new Dictionary<ObjectCtrlInfo, int>();
                    foreach (var jiggleBone in selectedJiggleBones)
                    {
                        if (jiggleBoneCount.ContainsKey(jiggleBone.oci) == false)
                            jiggleBoneCount[jiggleBone.oci] = 1;
                        else
                            jiggleBoneCount[jiggleBone.oci] += 1;
                    }

                    GUILayout.Label("Selected JiggleBones:");
                    GUILayout.BeginVertical(GUI.skin.box);
                    foreach (var kvp in jiggleBoneCount)
                        GUILayout.Label($"{kvp.Key.treeNodeObject.textName} - {kvp.Value} bones");      
                    GUILayout.EndVertical();

                    GUILayout.Label("Preset Name:");
                    presetName = GUILayout.TextField(presetName);

                    GUI.enabled = presetName.IsNullOrWhiteSpace() == false && selectedJiggleBones.Count > 0;
                    if (GUILayout.Button("Save Preset"))
                    {
                        var path = Path.Combine(presetsPath.Value, $"{presetName}.xml");
                        using (XmlTextWriter writer = new XmlTextWriter(path, Encoding.UTF8))
                        {
                            writer.WriteStartElement("root");

                            List<JiggleBone> jiggleBones = new List<JiggleBone>(selectedJiggleBones);

                            Dictionary<ObjectCtrlInfo, List<JiggleBone>> jiggleBoneDic = new Dictionary<ObjectCtrlInfo, List<JiggleBone>>();
                            foreach (var jiggleBone in selectedJiggleBones)
                            {
                                if (jiggleBoneDic.ContainsKey(jiggleBone.oci) == false)
                                    jiggleBoneDic[jiggleBone.oci] = new List<JiggleBone>() { jiggleBone };
                                else
                                    jiggleBoneDic[jiggleBone.oci].Add(jiggleBone);
                            }

                            int id = 0;
                            foreach (var kvp in jiggleBoneDic)
                            {
                                writer.WriteStartElement("oci");

                                writer.WriteValue("id", id);
                                writer.WriteValue("ociKind", kvp.Key.kind);
                                writer.WriteAttributeString("ociName", kvp.Key.treeNodeObject.textName);

                                foreach (var jiggleBone in kvp.Value)
                                {
                                    PointConstraint p = jiggleBone.constraint;
                                    
                                    writer.WriteStartElement("jigglebone");

                                    writer.WriteAttributeString("transformPath", jiggleBone.childConstraint.childTransform.GetPathFrom(kvp.Key.guideObject.transformTarget));
                                    writer.WriteValue("mode", (int)p.ConstraintParams.Mode);
                                    writer.WriteValue("radiusLimit", p.radiusLimit);
                                    writer.WriteValue("frequencyHz", p.ConstraintParams.FrequencyHz);
                                    writer.WriteValue("dampingRatio", p.ConstraintParams.DampingRatio);
                                    writer.WriteValue("halfLife", p.ConstraintParams.HalfLife);
                                    writer.WriteValue("offset", p.Offset);
                                    writer.WriteValue("colliderRadius", p.colliderRadius);
                                    writer.WriteValue("radiusEnabled", p.useRadiusLimit);

                                    writer.WriteEndElement();
                                }
                                writer.WriteEndElement();

                                id++;
                            }
   
                            writer.WriteEndElement();
                        }

                        isCreatingPreset = false;
                        ReloadPresetsList();
                    }

                    GUILayout.EndVertical();
                }
                else if (isLoadingPreset)
                {
                    GUILayout.Space(10);

                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"Loading Preset: {Path.GetFileName(selectedPreset)}", subHeadStyle);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.Space(10);

                    GUILayout.Label("Objects Count: " + loadedPresetData.Count);
                    GUILayout.Label("JiggleBones Count: " + loadedPresetData.Sum(x => x.jiggleBones.Count));

                    GUILayout.BeginVertical(GUI.skin.box);

                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Objects: ");
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    presetsSearchScroll = GUILayout.BeginScrollView(presetsSearchScroll, false, true, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.box);

                    for (int x = 0; x < loadedPresetData.Count; x++)
                    {
                        JiggleBonePreset preset = loadedPresetData[x];


                        string kind = "Unknown";

                        switch (preset.ociKind)
                        {
                            case 0:
                                kind = "Character";
                                break;
                            case 1:
                                kind = "Item";
                                break;
                            case 2:
                                kind = "Light";
                                break;
                            case 3:
                                kind = "Folder";
                                break;
                            case 4:
                                kind = "Route";
                                break;
                            case 5:
                                kind = "Camera";
                                break;
                        }

                        GUILayout.BeginVertical(GUI.skin.box);

                        GUILayout.Label("Original Name: " + preset.ociName);

                        GUILayout.Label("Type: " + kind);

                        GUI.color = preset.oci == null ? Color.red : Color.green;
                        string linkedItem = "(None)";
                        if (preset.oci != null)
                            linkedItem = preset.oci.treeNodeObject.textName;
                        GUILayout.Label("Linked Item: " + linkedItem);
                        GUI.color = defColor;

                        string selectedOciName = "(Nothing)";
                        if (selectedOci?.guideObject != null)
                            selectedOciName = selectedOci.treeNodeObject.textName;
                        if (GUILayout.Button("Link to: " + selectedOciName))
                        {
                            if (selectedOci?.guideObject != null)
                                preset.oci = selectedOci;
                            else
                                preset.oci = null;
                        }

                        GUILayout.EndVertical();
                    }

                    GUILayout.FlexibleSpace();

                    GUILayout.EndScrollView();

                    GUILayout.EndVertical();

                    GUILayout.FlexibleSpace();

                    GUI.enabled = loadedPresetData.Any(x => x.oci != null);
                    if (GUILayout.Button("Load Preset"))
                    {
                        selectedJiggleBones.Clear();

                        HashSet<Constraint> constraints = new HashSet<Constraint>();

                        List<Transform> bones = new List<Transform>();

                        foreach (var preset in loadedPresetData)
                        {
                            if (preset.oci == null) continue;
                            
                            foreach (var kvp in preset.jiggleBones)
                            {
                                JiggleBoneData jiggleBoneData = kvp.Value;
                                Transform bone = preset.oci.guideObject.transformTarget.Find(kvp.Key);
                                if (bone == null || bone.parent == null || bone.parent.name == "CommonSpace") continue;

                                JiggleBone jiggleBone = AddJiggleBone(preset.oci, jiggleBoneData, bone);

                                selectedJiggleBones.Add(jiggleBone);
                                constraints.Add(jiggleBone.childConstraint);
                            }
                        }

                        StartCoroutine(RefreshNodeConstraints(constraints, 1f));

                        loadedPresetData.Clear();
                        isLoadingPreset = false;
                    }

                }
                else
                {
                    presetsSearch = searchField;
                    presetsSearchScroll = GUILayout.BeginScrollView(presetsSearchScroll, false, false, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.box);

                    var fileListCopy = new List<string>(presetsList);
                    foreach (string file in fileListCopy)
                    {
                        if (!File.Exists(file))
                        {
                            fileListCopy.Remove(file);
                            continue;
                        }

                        string _fileName = Path.GetFileNameWithoutExtension(file);
                        if (_fileName.IndexOf(presetsSearch, StringComparison.CurrentCultureIgnoreCase) != -1)
                        {
                            GUILayout.BeginHorizontal();
                            GUI.color = selectedPreset == file ? Color.cyan : defColor;

                            // LOAD FILE
                            if (GUILayout.Button(_fileName))
                            {
                                if (selectedPreset != file)
                                    selectedPreset = file;
                                else
                                    selectedPreset = "";
                            }

                            // DELETE FILE
                            GUI.color = Color.red;
                            if (GUILayout.Button("Delete", GUILayout.ExpandWidth(false)))
                            {
                                if (File.Exists(file))
                                    File.Delete(file);
                                ReloadPresetsList();
                            }

                            GUI.color = defColor;
                            GUILayout.EndHorizontal();
                        }
                    }

                    GUILayout.EndScrollView();
                }
            }

            #endregion

            GUILayout.Space(10f);

            #region MIDDLE-BOTTOM BOX CONTENTS

            if (windowTab == WindowTab.JiggleBones)
            {
                GUI.enabled = true;
                GUI.color = defColor;

                if (GUILayout.Button(showAllJiggleBones ? "<size=18>☑️</size>   Show All" : "<size=18>☐</size>   Show All"))
                {
                    showAllJiggleBones = !showAllJiggleBones;
                    if (!showAllJiggleBones)
                    {
                        selectedJiggleBones.Clear();
                    }
                }

                GUI.enabled = selectedJiggleBones.Count > 0;
                if (GUILayout.Button("Refresh NC selected"))
                {
                    HashSet<Constraint> constraints = new HashSet<Constraint>();

                    foreach (var bone in selectedJiggleBones)
                        if (bone != null && bone.IsValid())
                            foreach (var c in _nodeConstraints._constraints)
                                if (c.parentTransform == bone.child.guideObject.transformTarget)
                                    constraints.Add(c);

                    StartCoroutine(RefreshNodeConstraints(constraints));
                }

                GUI.color = Color.red;
                if (GUILayout.Button("Delete selected"))
                {
                    foreach (var bone in selectedJiggleBones)
                    {
                        if (bone != null && bone.child != null && bone.parent != null)
                        {
                            Singleton<TreeNodeCtrl>.Instance.DeleteNode(bone.parent.treeNodeObject);
                            Singleton<TreeNodeCtrl>.Instance.DeleteNode(bone.child.treeNodeObject);
                        }
                    }
                    Singleton<TreeNodeCtrl>.Instance.SelectSingle(null, true);
                    Singleton<UndoRedoManager>.Instance.Clear();
                    selectedJiggleBones.Clear();
                }

                GUI.enabled = true;
                GUI.color = defColor;
            }
            else if (windowTab == WindowTab.Bones)
            {
                // ADD JIGGLE BONES
                GUI.color = Color.green;
                GUI.enabled = selectedBones.Count > 0;
                if (GUILayout.Button("Add JiggleBones to selected"))
                {
                    if (selectedBones.Count == 0) return;

                    selectedJiggleBones.Clear();

                    HashSet<Constraint> constraints = new HashSet<Constraint>();

                    foreach (var bone in selectedBones)
                    {
                        if (bone.parent == null || bone.parent.name == "CommonSpace") return;

                        JiggleBoneData jiggleBoneData = new JiggleBoneData
                        {
                            mode = 2,
                            radiusLimit = 0.02f,
                            frequencyHz = 3f,
                            dampingRatio = 0.5f,
                            halfLife = 10f,
                            offset = new float[3] { 0f, 0f, 0f },
                            colliderRadius = 0.05f,
                            radiusEnabled = true
                        };

                        JiggleBone jiggleBone = AddJiggleBone(selectedOci, jiggleBoneData, bone);

                        constraints.Add(jiggleBone.childConstraint);
                        selectedJiggleBones.Add(jiggleBone);
                    }

                    StartCoroutine(RefreshNodeConstraints(constraints, 1f));
                    selectedBones.Clear();
                }
                GUI.color = defColor;
                GUI.enabled = true;
            }
            else if (windowTab == WindowTab.Colliders)
            {
                GUI.color = defColor;
                GUI.enabled = true;

                if (GUILayout.Button("Refresh colliders"))
                {
                    selectedColliders.Clear();
                    currentColliders.Clear();

                    TreeNodeObject treeNode = selectedNodes?.FirstOrDefault();
                    if (treeNode != null)
                    {
                        if (Studio.Studio.Instance.dicInfo.TryGetValue(treeNode, out ObjectCtrlInfo info))
                        {
                            foreach (var c in selectedGuideObject.transformTarget.GetComponentsInChildren<DynamicBoneCollider>(true))
                                currentColliders.Add(new KeyValuePair<ObjectCtrlInfo, DynamicBoneCollider>(info, c));
                        }
                    }
                }
            }
            else if (windowTab == WindowTab.Presets)
            {
                GUI.color = isCreatingPreset ? Color.red : defColor;
                GUI.enabled = isLoadingPreset == false && allJiggleBones.Count > 0;
                if (GUILayout.Button(isCreatingPreset ? "Cancel" : "Create new preset"))
                {
                    isCreatingPreset = !isCreatingPreset;
                    ReloadPresetsList();
                    selectedJiggleBones.Clear();
                    presetName = "";
                    presetsSearch = "";
                }

                GUI.color = isLoadingPreset ? Color.red : defColor;
                GUI.enabled = selectedPreset.IsNullOrWhiteSpace() == false && File.Exists(selectedPreset) && !isCreatingPreset;
                if (GUILayout.Button(isLoadingPreset ? "Cancel" : "Load selected preset"))
                {
                    loadedPresetData.Clear();

                    if (isLoadingPreset)
                    {
                        isLoadingPreset = false;
                        ReloadPresetsList();
                    }
                    else
                    {
                        Dictionary<int, ObjectCtrlInfo> idObjectPairs = Studio.Studio.Instance.dicObjectCtrl;
                        XmlDocument document = new XmlDocument();
                        try
                        {
                            document.Load(selectedPreset);

                            foreach (XmlNode ociNode in document.FirstChild.ChildNodes)
                            {
                                if (ociNode.Name == "oci")
                                {
                                    int id = ociNode.ReadInt("id");
                                    int kind = ociNode.ReadInt("ociKind");
                                    string name = ociNode.Attributes["ociName"].Value;

                                    List<KeyValuePair<string, JiggleBoneData>> jiggleBonesDataList = new List<KeyValuePair<string, JiggleBoneData>>();

                                    foreach (XmlNode node in ociNode.ChildNodes)
                                    {
                                        if (node.Name == "jigglebone")
                                        {
                                            string transformPath = node.Attributes["transformPath"].Value;
                                            int mode = node.ReadInt("mode");
                                            float radiusLimit = node.ReadFloat("radiusLimit");
                                            float frequencyHz = node.ReadFloat("frequencyHz");
                                            float dampingRatio = node.ReadFloat("dampingRatio");
                                            float halfLife = node.ReadFloat("halfLife");
                                            Vector3 offset = node.ReadVector3("offset");
                                            float colliderRadius = node.ReadFloat("colliderRadius");
                                            bool radiusEnabled = node.ReadBool("radiusEnabled");

                                            var data = new JiggleBoneData
                                            {
                                                mode = mode,
                                                radiusLimit = radiusLimit,
                                                frequencyHz = frequencyHz,
                                                dampingRatio = dampingRatio,
                                                halfLife = halfLife,
                                                offset = new float[3] { offset.x, offset.y, offset.z },
                                                colliderRadius = colliderRadius,
                                                radiusEnabled = radiusEnabled
                                            };

                                            jiggleBonesDataList.Add(new KeyValuePair<string, JiggleBoneData>(transformPath, data));
                                        }
                                    }

                                    JiggleBonePreset preset = new JiggleBonePreset
                                    {
                                        ociId = id,
                                        ociKind = kind,
                                        ociName = name,
                                        jiggleBones = jiggleBonesDataList
                                    };

                                    loadedPresetData.Add(preset);
                                }
                            }

                            if (loadedPresetData.Count > 0)
                                isLoadingPreset = true;

                        }
                        catch (Exception e)
                        {
                            Logger.LogMessage("There was an error loading the preset.");
                            Logger.LogError(e);
                        }
                    }
                }
            }

            #endregion

            #region BOTTOM BOX CONTENTS

            if (windowTab != WindowTab.Presets)
            {
                bool enableClear = false;

                if (windowTab == WindowTab.Bones)
                    enableClear = selectedBones.Count > 0;
                else if (windowTab == WindowTab.JiggleBones)
                    enableClear = selectedJiggleBones.Count > 0;
                else if (windowTab == WindowTab.Colliders)
                    enableClear = selectedColliders.Count > 0;

                GUI.enabled = enableClear;
                if (GUILayout.Button("Clear selection"))
                {
                    if (windowTab == WindowTab.Bones)
                        selectedBones.Clear();
                    else if (windowTab == WindowTab.JiggleBones)
                        selectedJiggleBones.Clear();
                    else if (windowTab == WindowTab.Colliders)
                        selectedColliders.Clear();
                }
            }

            #endregion

            #endregion

            GUILayout.EndVertical();


            #region LEFT SIDE PANEL CONTENT

            if (windowTab != WindowTab.Presets && windowTab != WindowTab.Bones)
            {
                GUI.enabled = true;
                GUI.color = defColor;

                GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(220f));

                if (windowTab == WindowTab.JiggleBones)
                {
                    GUI.color = defColor;
                    GUILayout.Label("Edit JiggleBones", subHeadStyle);

                    if (selectedJiggleBones.Count > 0)
                    {
                        GUILayout.BeginHorizontal(GUI.skin.box);
                        GUILayout.BeginVertical();

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(selectedJiggleBones.Count == 1 ? $"{selectedJiggleBones.First().child.treeNodeObject.textName}" : $"({selectedJiggleBones.Count}) JiggleBones Selected", subHeadStyle);
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        var lastJiggleBone = selectedJiggleBones.Last();

                        GUILayout.Space(10f);

                        var isEnabled = lastJiggleBone.parentConstraint.enabled && lastJiggleBone.childConstraint.enabled;
                        IMGUIExtensions.BoolValue("Enabled", isEnabled, (b) =>
                        {
                            foreach (var jiggleBone in selectedJiggleBones)
                            {
                                if (jiggleBone == null || jiggleBone.IsValid() == false) continue;

                                jiggleBone.parentConstraint.enabled = b;
                                jiggleBone.childConstraint.enabled = b;
                            }
                        });

                        GUILayout.Space(5f);

                        GUILayout.BeginVertical(GUI.skin.box);
                        GUILayout.Label("Mode:");
                        foreach (ConstraintParams.ParameterMode mode in Enum.GetValues(typeof(ConstraintParams.ParameterMode)))
                        {
                            string buttonText = mode.ToString();

                            if (lastJiggleBone.constraint.ConstraintParams.Mode == mode)
                            {
                                GUI.color = Color.cyan;
                                buttonText = "> " + buttonText;
                            }

                            if (GUILayout.Button(buttonText))
                            {
                                foreach (var jiggleBone in selectedJiggleBones)
                                    jiggleBone.constraint.ConstraintParams.Mode = mode;
                            }
                            GUI.color = defColor;
                        }

                        switch (lastJiggleBone.constraint.ConstraintParams.Mode)
                        {
                            case ConstraintParams.ParameterMode.SoftHalfLife:
                                IMGUIExtensions.FloatValue("Frequency HZ", lastJiggleBone.constraint.ConstraintParams.FrequencyHz, 0.0001f, 10f, "0.0000", (value) => { foreach (var jiggleBone in selectedJiggleBones) jiggleBone.constraint.ConstraintParams.FrequencyHz = value; }, true);
                                IMGUIExtensions.FloatValue("Half Life", lastJiggleBone.constraint.ConstraintParams.HalfLife, 0.0001f, 10f, "0.0000", (value) => { foreach (var jiggleBone in selectedJiggleBones) jiggleBone.constraint.ConstraintParams.HalfLife = value; }, true);
                                break;

                            case ConstraintParams.ParameterMode.SoftDampingRatio:
                                IMGUIExtensions.FloatValue("Frequency HZ", lastJiggleBone.constraint.ConstraintParams.FrequencyHz, 0.0001f, 10f, "0.0000", (value) => { foreach (var jiggleBone in selectedJiggleBones) jiggleBone.constraint.ConstraintParams.FrequencyHz = value; }, true);
                                IMGUIExtensions.FloatValue("Damping Ratio", lastJiggleBone.constraint.ConstraintParams.DampingRatio, 0f, 10f, "0.0000", (value) => { foreach (var jiggleBone in selectedJiggleBones) jiggleBone.constraint.ConstraintParams.DampingRatio = value; }, true);
                                break;

                            case ConstraintParams.ParameterMode.SoftExponential:
                                IMGUIExtensions.FloatValue("Half Life", lastJiggleBone.constraint.ConstraintParams.HalfLife, 0.0001f, 10f, "0.0000", (value) => { foreach (var jiggleBone in selectedJiggleBones) jiggleBone.constraint.ConstraintParams.HalfLife = value; }, true);
                                break;

                            case ConstraintParams.ParameterMode.Hard:
                                break;

                        }

                        GUILayout.EndVertical();

                        // VECTOR 3 OFFSET
                        GUILayout.BeginVertical(GUI.skin.box);
                        GUILayout.Label("Offset:");
                        IMGUIExtensions.FloatValue("Increment", offsetIncrement, 0.001f, 1f, "0.000", (value) => { offsetIncrement = value; }, true);
                        lastJiggleBone.constraint.Offset = IMGUIExtensions.Vector3Editor(lastJiggleBone.constraint.Offset, offsetIncrement);
                        GUILayout.EndVertical();


                        // FLOAT RADIUS LIMIT
                        GUILayout.BeginHorizontal(GUI.skin.box);
                        IMGUIExtensions.BoolValue("", lastJiggleBone.constraint.useRadiusLimit, (value) => { foreach (var jiggleBone in selectedJiggleBones) jiggleBone.constraint.useRadiusLimit = value; });
                        GUI.enabled = lastJiggleBone.constraint.useRadiusLimit;
                        IMGUIExtensions.FloatValue("Radius Limit", lastJiggleBone.constraint.radiusLimit, 0.0001f, 0.1f, "0.0000", (value) => { foreach (var jiggleBone in selectedJiggleBones) jiggleBone.constraint.radiusLimit = value; }, true);
                        GUILayout.EndHorizontal();
                        GUI.enabled = true;

                        // FLOAT COLLIDER RADIUS
                        IMGUIExtensions.FloatValue("Collider Radius", lastJiggleBone.constraint.colliderRadius, 0f, 2f, "0.00", (value) => { foreach (var jiggleBone in selectedJiggleBones) jiggleBone.constraint.colliderRadius = value; }, true);

                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();

                        GUILayout.FlexibleSpace();
                    }
                    else
                    {
                        GUILayout.FlexibleSpace();
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.Label("Select at least one JiggleBone!");
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                        GUILayout.FlexibleSpace();
                    }
                }
                else if (windowTab == WindowTab.Colliders)
                {
                    GUI.color = defColor;
                    GUILayout.Label("Edit Colliders", subHeadStyle);

                    if (selectedColliders.Count > 0)
                    {
                        GUI.enabled = true;

                        GUILayout.BeginHorizontal(GUI.skin.box);
                        GUILayout.BeginVertical();

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(selectedColliders.Count == 1 ? $"{selectedColliders.First().Value.gameObject.name}" : $"{selectedColliders.Count} Colliders selected", subHeadStyle);
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        var lastCollider = selectedColliders.Last();

                        GUILayout.Space(10f);

                        colliderEditScroll = GUILayout.BeginScrollView(colliderEditScroll, false, true, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar);

                        foreach (var bone in allJiggleBones)
                        {
                            GUILayout.BeginHorizontal(GUI.skin.box);

                            IMGUIExtensions.BoolValue("", bone.constraint.colliders.Contains(lastCollider), (v) =>
                            {
                                if (v)
                                {
                                    foreach (var col in selectedColliders)
                                    {
                                        if (!bone.constraint.colliders.Contains(col))
                                            bone.constraint.colliders.Add(col);
                                    }
                                }
                                else
                                {
                                    foreach (var col in selectedColliders)
                                    {
                                        bone.constraint.colliders.Remove(col);
                                    }
                                }
                            });

                            if (GUILayout.Button(bone.child.treeNodeObject.textName))
                            {
                                Studio.Studio.Instance.cameraCtrl.targetPos = bone.child.guideObject.transformTarget.position;
                            }


                            GUILayout.EndHorizontal();
                        }

                        GUILayout.EndScrollView();

                        GUILayout.Space(10f);

                        GUI.enabled = true;
                        if (GUILayout.Button("Enable All"))
                        {
                            foreach (var bone in allJiggleBones)
                            {
                                foreach (var col in selectedColliders)
                                {
                                    if (!bone.constraint.colliders.Contains(col))
                                        bone.constraint.colliders.Add(col);
                                }
                            }
                        }
                        if (GUILayout.Button("Disable All"))
                        {
                            foreach (var bone in allJiggleBones)
                            {
                                foreach (var col in selectedColliders)
                                {
                                    bone.constraint.colliders.Remove(col);
                                }
                            }
                        }
                        if (GUILayout.Button("Invert All"))
                        {
                            foreach (var bone in allJiggleBones)
                            {
                                foreach (var col in selectedColliders)
                                {
                                    if (bone.constraint.colliders.Contains(col))
                                        bone.constraint.colliders.Remove(col);
                                    else
                                        bone.constraint.colliders.Add(col);
                                }
                            }
                        }


                        GUI.enabled = true;

                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        GUILayout.FlexibleSpace();
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.Label("Select at least one Collider!");
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                        GUILayout.FlexibleSpace();
                    }
                }

                GUILayout.EndVertical();
            }

            #endregion


            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUI.color = defColor;
            GUI.enabled = true;
            windowRect.Value = IMGUIUtils.DragResizeWindow(WindowsUniqueId, windowRect.Value);
        }

        private static JiggleBone AddJiggleBone(ObjectCtrlInfo oci, JiggleBoneData jiggleBoneData, Transform bone)
        {
            if (rootFolder == null || rootFolder.objectItem == null)
            {
                if (Singleton<TreeNodeCtrl>.Instance.m_TreeNodeObject.Any(x => x.textName == "JiggleBones"))
                {
                    rootFolder = Singleton<Studio.Studio>.Instance.dicInfo[Singleton<TreeNodeCtrl>.Instance.m_TreeNodeObject.First(x => x.textName == "JiggleBones")] as OCIFolder;
                }
                else
                {
                    rootFolder = AddObjectFolder.Add();
                    rootFolder.objectItem.name = "JiggleBones";
                    rootFolder.name = "JiggleBones";
                    rootFolder.guideObject.changeAmount.pos = Vector3.zero;
                    rootFolder.guideObject.changeAmount.rot = Vector3.zero;
                    rootFolder.guideObject.changeAmount.scale = Vector3.one;
                }
            }

            TreeNodeObject treeNode = oci.treeNodeObject;
            string name = treeNode?.textName ?? "";

            var folder = AddObjectFolder.Add();
            var sphere = AddObjectItem.Add(0, 0, 0);

            folder.objectItem.name = $"(JiggleBone Parent) {name} | {bone.parent.name}";
            folder.name = $"(JiggleBone Parent) {name} | {bone.parent.name}";

            sphere.treeNodeObject.textName = $"(JiggleBone) {name} | {bone.name}";

            Transform parent = folder.guideObject.transformTarget;
            Transform child = sphere.guideObject.transformTarget;

            var phyB = child.gameObject.GetOrAddComponent<PhysicsBody>();
            phyB.guideObject = sphere.guideObject;

            var phyC = child.gameObject.GetOrAddComponent<PointConstraint>();
            phyB.pointConstraint = phyC;
            phyC.Parent = parent;

            phyC.ConstraintParams.Mode = (ConstraintParams.ParameterMode)jiggleBoneData.mode;
            phyC.ConstraintParams.HalfLife = jiggleBoneData.halfLife;
            phyC.ConstraintParams.FrequencyHz = jiggleBoneData.frequencyHz;
            phyC.ConstraintParams.DampingRatio = jiggleBoneData.dampingRatio;
            phyC.radiusLimit = jiggleBoneData.radiusLimit;
            phyC.Offset = new Vector3(jiggleBoneData.offset[0], jiggleBoneData.offset[1], jiggleBoneData.offset[2]);
            phyC.colliderRadius = jiggleBoneData.colliderRadius ?? 0.05f;

            phyC.enabled = true;

            folder.guideObject.changeAmount.pos = bone.position;
            folder.guideObject.changeAmount.rot = bone.rotation.eulerAngles;
            sphere.treeNodeObject.SetVisible(false);

            Singleton<TreeNodeCtrl>.Instance.SetParent(sphere.treeNodeObject, rootFolder.treeNodeObject);
            Singleton<TreeNodeCtrl>.Instance.SetParent(folder.treeNodeObject, rootFolder.treeNodeObject);

            var parentConstraint = _nodeConstraints.AddConstraint(true, bone.parent, parent, true, Vector3.zero, false, Quaternion.identity, false, Vector3.one, "(JiggleBone Parent) - " + bone.parent.name);
            var childConstraint = _nodeConstraints.AddConstraint(true, child, bone, true, Vector3.zero, false, Quaternion.identity, false, Vector3.one, "(JiggleBone) - " + bone.name);

            var jiggleBone = new JiggleBone(phyC, folder, sphere, parentConstraint, childConstraint, selectedOci);
            allJiggleBones.Add(jiggleBone);
            return jiggleBone;
        }

        IEnumerator RefreshNodeConstraints(HashSet<NodesConstraints.NodesConstraints.Constraint> constraints, float delay = 0)
        {
            if (constraints == null || constraints.Count == 0) yield break;

            if (delay > 0)
                yield return new WaitForSecondsRealtime(delay);

            foreach (var c in constraints)
                if (c != null)
                    c.enabled = false;

            yield return null;

            _nodeConstraints._onPreCullAction = () =>
            {
                foreach (var c in constraints)
                    if (c != null)
                    {
                        c.childTransform.localPosition = c.originalChildPosition;
                        if (c.child != null)
                            c.child.changeAmount.pos = c.originalChildPosition;


                        c.childTransform.localRotation = c.originalChildRotation;
                        if (c.child != null)
                            c.child.changeAmount.rot = c.originalChildRotation.eulerAngles;

                        c.positionOffset = c.parentTransform.InverseTransformPoint(c.childTransform.position);
                        c.rotationOffset = Quaternion.Inverse(c.parentTransform.rotation) * c.childTransform.rotation;

                        c.originalChildPosition = c.childTransform.localPosition;
                        c.originalChildRotation = c.childTransform.localRotation;
                    }
            };

            yield return null;

            foreach (var c in constraints)
                if (c != null)
                    c.enabled = true;
        }

        private void DisplayObjectTree(GameObject go, int indent, ref List<Transform> displayedBones)
        {
            string displayedName = go.name;
            if (bonesSearch.Length == 0 || go.name.IndexOf(bonesSearch, StringComparison.OrdinalIgnoreCase) != -1)
            {
                if (selectedBones.Contains(go.transform))
                    GUI.color = Color.cyan;
                GUILayout.BeginHorizontal();
                if (bonesSearch.Length == 0)
                {
                    GUILayout.Space(indent * 20f);
                    if (go.transform.childCount != 0)
                    {
                        if (GUILayout.Toggle(openedBones.Contains(go), "", GUILayout.ExpandWidth(false)))
                        {
                            if (openedBones.Contains(go) == false)
                                openedBones.Add(go);
                        }
                        else
                        {
                            if (openedBones.Contains(go))
                                openedBones.Remove(go);
                        }
                    }
                    else
                        GUILayout.Space(20f);
                }
                displayedBones.Add(go.transform);
                if (GUILayout.Button(displayedName, GUILayout.ExpandWidth(false)))
                {
                    if (Input.GetKey(KeyCode.LeftControl))
                    {
                        if (selectedBones.Contains(go.transform))
                            selectedBones.Remove(go.transform);
                        else
                            selectedBones.Add(go.transform);
                    }
                    else if (Input.GetKey(KeyCode.LeftShift) && selectedBones.Count > 0)
                    {
                        selectMulitpleBones = (bones) =>
                        {
                            int firstIndex = bones.IndexOf(selectedBones.First());
                            int lastIndex = bones.IndexOf(go.transform);
                            if (firstIndex != lastIndex)
                            {
                                int inc;
                                if (firstIndex < lastIndex)
                                    inc = 1;
                                else
                                    inc = -1;
                                for (var i = firstIndex; i != lastIndex; i += inc)
                                {
                                    if (selectedBones.Contains(bones[i]) == false)
                                        selectedBones.Add(bones[i]);
                                }
                                if (selectedBones.Contains(go.transform) == false)
                                    selectedBones.Add(go.transform);
                            }

                            selectMulitpleBones = null;
                        }; 
                    }
                    else
                    {
                        selectedBones.Clear();
                        selectedBones.Add(go.transform);
                    }

                    if (GUI.GetNameOfFocusedControl() == "searchbar")
                        GUI.FocusControl("");
                }

                GUI.color = defColor;
                GUILayout.EndHorizontal();
            }
            if (bonesSearch.Length != 0 || openedBones.Contains(go))
                for (int i = 0; i < go.transform.childCount; ++i)
                    DisplayObjectTree(go.transform.GetChild(i).gameObject, indent + 1, ref displayedBones);
        }

        private void OpenParents(Transform child, Transform limit)
        {
            if (child == limit)
                return;
            child = child.parent;
            while (child.parent != null && child != limit)
            {
                openedBones.Add(child.gameObject);
                child = child.parent;
            }
            openedBones.Add(child.gameObject);
        }

        public static void ReloadPresetsList()
        {
            if (!Directory.Exists(presetsPath.Value))
                Directory.CreateDirectory(presetsPath.Value);

            string[] files = Directory.GetFiles(presetsPath.Value, "*.xml");
            presetsList.Clear();
            presetsList = files.ToList();
        }

    }

}
