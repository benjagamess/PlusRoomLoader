using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using System.Linq;
using System;
using PlusStudioLevelLoader;
using PlusLevelStudio.Editor;

namespace RewriteRoomLoader
{
    [HarmonyPatch(typeof(LevelGenerator), "StartGenerate")]
    class CustomNPCRoomAssetsPatch
    {
        static void Prefix()
        {
            SceneObject scene = Singleton<CoreGameManager>.Instance.sceneObject;
            List<NPC> npcs = new List<NPC>();

            npcs.AddRange(scene.forcedNpcs);
            foreach (WeightedNPC npc in scene.potentialNPCs)
            {
                npcs.Add(npc.selection);
            }

            foreach (NPC npc in npcs)
            {
                if (npc.Character == Character.Sweep || npc.Character == Character.DrReflex)
                {
                    if (npc.potentialRoomAssets.Length > 1)
                    {
                        foreach (WeightedRoomAsset room in npc.potentialRoomAssets)
                        {
                            if (room.selection.name.StartsWith("CUSTOM_"))
                            {
                                List<WeightedRoomAsset> list = npc.potentialRoomAssets.ToList();
                                list.Remove(room);
                                npc.potentialRoomAssets = list.ToArray();

                                Debug.Log("Room from " + npc.Character.ToString() + " removed");
                            }
                        }
                    }
                }

                foreach (KeyValuePair<ExtendedRoomAsset, RoomJsonData> room in RewriteRoomLoader.Instance.loadedRooms)
                {
                    if ((npc.Character == Character.Sweep && room.Value.roomType == 4) || (npc.Character == Character.DrReflex && room.Value.roomType == 5))
                    {
                        if (room.Value.floorSpawns.Contains(scene.levelNo) && scene.levelTitle.StartsWith("F") && RewriteRoomLoader.Instance.DoesRoomFitWithLevelType(room, scene.levelObject))
                        {
                            npc.potentialRoomAssets = npc.potentialRoomAssets.AddToArray(new WeightedRoomAsset()
                            {
                                selection = room.Key,
                                weight = room.Value.spawnWeights[Array.IndexOf(room.Value.floorSpawns, scene.levelNo)]
                            });
                            Debug.Log("Potential room asset added to " + npc.Character.ToString());
                            break;
                        }
                        if (room.Value.inEndless && scene.levelTitle == "END" && RewriteRoomLoader.Instance.DoesRoomFitWithLevelType(room, scene.levelObject) && RewriteRoomLoader.Instance.DoesRoomFitWithEndlessSize(room, scene))
                        {
                            npc.potentialRoomAssets = npc.potentialRoomAssets.AddToArray(new WeightedRoomAsset()
                            {
                                selection = room.Key,
                                weight = RewriteRoomLoader.Instance.GetWeightFromEndlessSize(room, scene)
                            });
                            Debug.Log("Potential room asset added to " + npc.Character.ToString() + " (Endless)");
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(BaseGameManager), "Update")]
    class TemporaryTestPatch
    {
        static void Postfix()
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                Debug.Log("--- ROOM DATA LOGS ---");
                foreach (KeyValuePair<ExtendedRoomAsset, RoomJsonData> test in RewriteRoomLoader.Instance.loadedRooms)
                {
                    Debug.Log("NAME: " + test.Key.name);
                    Debug.Log("TYPE: " + test.Value.roomType);
                    Debug.Log("FLOOR SPAWNS: " + string.Format("[{0}]", string.Join(", ", test.Value.floorSpawns)));
                    Debug.Log("FLOOR TYPE SPAWNS: " + string.Format("[{0}]", string.Join(", ", test.Value.floorTypeSpawns)));
                    Debug.Log("SPAWN WEIGHTS: " + string.Format("[{0}]", string.Join(", ", test.Value.spawnWeights)));
                    Debug.Log("MIN ITEM VALUE: " + test.Value.minItemValue);
                    Debug.Log("MAX ITEM VALUE: " + test.Value.maxItemValue);
                    Debug.Log("WINDOW CHANCE: " + test.Value.windowChance);
                    Debug.Log("IN ENDLESS: " + test.Value.inEndless);
                    Debug.Log("--------------------------");
                }
            }
        }
    }
}
