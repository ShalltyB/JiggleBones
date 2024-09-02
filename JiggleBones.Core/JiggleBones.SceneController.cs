using Studio;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using ExtensibleSaveFormat;
using System.Collections.Generic;
using MessagePack;
using UnityEngine;
using System.Linq;
using static JiggleBones.JiggleBones;
using ToolBox.Extensions;

namespace JiggleBones
{
    class JiggleBonesSaveData : SceneCustomFunctionController
    {
        protected override void OnSceneSave()
        {
            Dictionary<int, ObjectCtrlInfo> idObjectPairs = Studio.Studio.Instance.dicObjectCtrl;
            PluginData data = new PluginData();

            List<JiggleBoneData> jiggleBonesDataList = new List<JiggleBoneData>();
            foreach (var jiggleBone in allJiggleBones)
            {
                int idOci = idObjectPairs.FirstOrDefault(kv => kv.Value == jiggleBone.oci).Key;
                int idParent = idObjectPairs.FirstOrDefault(kv => kv.Value == jiggleBone.parent).Key;
                int idChild = idObjectPairs.FirstOrDefault(kv => kv.Value == jiggleBone.child).Key;

                PointConstraint p = jiggleBone.constraint;

                JiggleBoneData jiggleBoneData = new JiggleBoneData
                {
                    mode = (int)p.ConstraintParams.Mode,
                    radiusLimit = p.radiusLimit,
                    frequencyHz = p.ConstraintParams.FrequencyHz,
                    dampingRatio = p.ConstraintParams.DampingRatio,
                    halfLife = p.ConstraintParams.HalfLife,
                    offset = new float[3] { p.Offset.x, p.Offset.y, p.Offset.z },
                    colliderRadius = p.colliderRadius,
                    idOci = idOci,
                    idParent = idParent,
                    idChild = idChild,
                    radiusEnabled = p.useRadiusLimit,
                };

                foreach (var kvp in p.colliders)
                {
                    if (kvp.Key == null || kvp.Value == null) continue;
                    int colliderID = idObjectPairs.FirstOrDefault(kv => kv.Value == kvp.Key).Key;
                    jiggleBoneData.colliders.Add(new KeyValuePair<int, string>(colliderID, kvp.Value.transform.GetPathFrom(kvp.Key.guideObject.transformTarget)));
                }

                jiggleBonesDataList.Add(jiggleBoneData);
            }

            if (jiggleBonesDataList.Count > 0)
                data.data.Add("JiggleBonesData", MessagePackSerializer.Serialize(jiggleBonesDataList));
            

            SetExtendedData(data);
        }

        protected override void OnSceneLoad(SceneOperationKind operation, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
        {
            if (operation == SceneOperationKind.Clear || operation == SceneOperationKind.Load)
            {
                allJiggleBones.Clear();
                selectedJiggleBones.Clear();
                selectedColliders.Clear();
                selectedBones.Clear();
                currentColliders.Clear();
                if (operation == SceneOperationKind.Clear) return;
            }

            var data = GetExtendedData();
            if (data == null) return;

            List<KeyValuePair<int, int>> IDs = new List<KeyValuePair<int, int>>();
            List<JiggleBoneData> jiggleBonesDataList = new List<JiggleBoneData>();

            if (data.data.TryGetValue("springBonesDataList", out var bytes) && bytes != null)
            {
                // old name
                jiggleBonesDataList = MessagePackSerializer.Deserialize<List<JiggleBoneData>>((byte[])bytes);
            }
            else if (data.data.TryGetValue("JiggleBonesData", out bytes) && bytes != null)
            {
                jiggleBonesDataList = MessagePackSerializer.Deserialize<List<JiggleBoneData>>((byte[])bytes);
            }

            if (data.data.TryGetValue("ids", out var ids) && ids != null)
            {
                IDs = MessagePackSerializer.Deserialize<List<KeyValuePair<int, int>>>((byte[])ids);
            }

            List<JiggleBone> loadedJiggleBones = new List<JiggleBone>();

            int i = 0;
            foreach (var jiggleBoneData in jiggleBonesDataList)
            {
                ObjectCtrlInfo oci = null;
                OCIFolder parent = null;
                OCIItem child = null;

                // Old data
                if (IDs.Count > 0)
                {
                    var id = IDs[i];

                    parent = loadedItems[id.Key] as OCIFolder;
                    child = loadedItems[id.Value] as OCIItem;
                }
                else
                {
                    int ociID = jiggleBoneData.idOci ?? -1;
                    if (ociID != -1)
                        oci = loadedItems[ociID];

                    int parentID = jiggleBoneData.idParent ?? -1;
                    if (parentID != -1)
                        parent = loadedItems[parentID] as OCIFolder;

                    int childID = jiggleBoneData.idChild ?? -1;
                    if (childID != -1)
                        child = loadedItems[childID] as OCIItem;
                }

                Transform parentTransform = parent.guideObject.transformTarget;
                Transform childTransform = child.guideObject.transformTarget;

                var phyB = childTransform.gameObject.GetOrAddComponent<PhysicsBody>();
                phyB.guideObject = child.guideObject;

                var phyC = childTransform.gameObject.GetOrAddComponent<PointConstraint>();
                phyB.pointConstraint = phyC;
                phyC.Parent = parentTransform;

                phyC.ConstraintParams.Mode = (ConstraintParams.ParameterMode)jiggleBoneData.mode;
                phyC.ConstraintParams.HalfLife = jiggleBoneData.halfLife;
                phyC.ConstraintParams.FrequencyHz = jiggleBoneData.frequencyHz;
                phyC.ConstraintParams.DampingRatio = jiggleBoneData.dampingRatio;
                phyC.radiusLimit = jiggleBoneData.radiusLimit;
                phyC.Offset = new Vector3(jiggleBoneData.offset[0], jiggleBoneData.offset[1], jiggleBoneData.offset[2]);
                phyC.useRadiusLimit = jiggleBoneData.radiusEnabled ?? false;

                phyC.enabled = true;
                
                phyC.colliderRadius = jiggleBoneData.colliderRadius ?? 0.05f;

                if (jiggleBoneData.colliders != null)
                {
                    foreach (var kvp in jiggleBoneData.colliders)
                    {
                        var colliderOci = loadedItems[kvp.Key];
                        if (colliderOci != null)
                        {
                            Transform t = colliderOci.guideObject.transformTarget.Find(kvp.Value);
                            if (t == null) continue;

                            var collider = t.gameObject.GetComponent<DynamicBoneCollider>();

                            if (collider != null)
                                phyC.colliders.Add(new KeyValuePair<ObjectCtrlInfo, DynamicBoneCollider>(colliderOci, collider));
                        }
                    }
                }

                var jiggleBone = new JiggleBone(phyC, parent, child, oci);
                loadedJiggleBones.Add(jiggleBone);
                i++;
            }

            this.ExecuteDelayed2(() =>
            {
                if (loadedJiggleBones.Count > 0 && _nodeConstraints != null && _nodeConstraints._constraints.Count > 0)
                {
                    int badBones = 0;
                    List<KeyValuePair<int, ObjectCtrlInfo>> dic = new SortedDictionary<int, ObjectCtrlInfo>(Studio.Studio.Instance.dicObjectCtrl).ToList();
                    foreach (var jiggleBone in loadedJiggleBones)
                    {
                        jiggleBone.parentConstraint = _nodeConstraints._constraints.FirstOrDefault(c => c.childTransform == jiggleBone.parent.guideObject.transformTarget);
                        jiggleBone.childConstraint = _nodeConstraints._constraints.FirstOrDefault(c => c.parentTransform == jiggleBone.child.guideObject.transformTarget);

                        if (jiggleBone.parentConstraint == null || jiggleBone.childConstraint == null)
                        {
                            badBones++;
                            continue;
                        }


                        if (jiggleBone.oci == null)
                        {
                            int childObjectIndex = -1;
                            Transform childT = jiggleBone.childConstraint.childTransform;
                            while ((childObjectIndex = dic.FindIndex(e => e.Value.guideObject.transformTarget == childT)) == -1)
                                childT = childT.parent;

                            if (childObjectIndex != -1)
                                jiggleBone.oci = dic[childObjectIndex].Value;
                            else
                            {
                                badBones++;
                                continue;
                            }
                        }

                        allJiggleBones.Add(jiggleBone);
                    }

                    if (badBones > 0)
                        JiggleBones.Logger.LogWarning($"{badBones} JiggleBones could not be created.");
                }
            }, 30);
        }

        protected override void OnObjectDeleted(ObjectCtrlInfo oci)
        {
            if (allJiggleBones.Any(kvp => kvp.child == oci || kvp.parent == oci))
                allJiggleBones.Where(kvp => kvp.child == oci || kvp.parent == oci).ToList().ForEach(x => allJiggleBones.Remove(x));
        }
    }
}
