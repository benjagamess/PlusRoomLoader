using HarmonyLib;
using BepInEx;
using MTM101BaldAPI;
using PlusStudioLevelLoader;
using UnityEngine;
using MTM101BaldAPI.Registers;
using MTM101BaldAPI.SaveSystem;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MTM101BaldAPI.AssetTools;
using System.Linq;
using Newtonsoft.Json;
using PlusStudioLevelFormat;
using System;

namespace RewriteRoomLoader
{
    [BepInPlugin("benjagamess.plusmods.roomloader", "Custom Room Loader", "2.0.0.0")]
    public class RewriteRoomLoader : BaseUnityPlugin
    {
        public static RewriteRoomLoader Instance { get; private set; }

        public Dictionary<string, string> roomPaths = new Dictionary<string, string>();
        public Dictionary<ExtendedRoomAsset, RoomJsonData> loadedRooms = new Dictionary<ExtendedRoomAsset, RoomJsonData>();

        void Awake()
        {
            Harmony h = new Harmony("benjagamess.plusmods.roomloader");
            h.PatchAllConditionals();

            Instance = this;

            LoadingEvents.RegisterOnAssetsLoaded(this.Info, OnAssetsLoaded(), LoadingEventOrder.Pre);
            GeneratorManagement.Register(this, GenerationModType.Addend, AddToGenerator);

            ModdedSaveGame.AddSaveHandler(this.Info);
        }

        IEnumerator OnAssetsLoaded()
        {
            yield return 2;

            yield return "Filtering out bad rooms";

            if (!Directory.Exists(Path.Combine(AssetLoader.GetModPath(this), "Rooms")))
            {
                Debug.LogWarning("The Rooms folder could not be found. Re-install the mod or create the directory -> StreamingAssets/Modded/benjagamess.plusmods.roomloader/Rooms");
            }

            List<string> paths = Directory.GetFiles(Path.Combine(AssetLoader.GetModPath(this), "Rooms"), "*.rbpl", SearchOption.AllDirectories).ToList();

            foreach (string path in paths)
            {
                string pathWithoutExtension = path.Replace(".rbpl", "");
                if (File.Exists(pathWithoutExtension + ".json"))
                {
                    string jsonPath = pathWithoutExtension + ".json";

                    string content = File.ReadAllText(jsonPath);
                    if (!content.Contains("roomType"))
                    {
                        Debug.LogWarning("JSON file for room: " + Path.GetFileNameWithoutExtension(path) + " does not contain the required property 'roomType'. Room will not be loaded.");
                        continue;
                    }

                    roomPaths.Add(path, jsonPath);
                }
                else
                {
                    Debug.LogWarning("JSON file for room: " + Path.GetFileNameWithoutExtension(path) + " was not found. Check for typos and make sure the JSON file is named the exact same as the rbpl file. Room will not be loaded.");
                }
            }

            yield return "Loading rooms";

            foreach (string room in roomPaths.Keys)
            {
                string json = File.ReadAllText(roomPaths[room]);
                string roomName = Path.GetFileNameWithoutExtension(room);
                RoomJsonData roomData;
                try
                {
                    roomData = JsonConvert.DeserializeObject<RoomJsonData>(json);
                }
                catch (System.Exception)
                {
                    Debug.LogWarning("JSON file for room: " + roomName + " could not be read. Room will not be loaded.");
                    roomPaths.Remove(room);
                    continue;
                }

                ExtendedRoomAsset roomAsset;
                roomAsset = LevelImporter.CreateRoomAsset(BaldiRoomAsset.Read(new BinaryReader(File.Open(room, FileMode.Open))));
                if (roomAsset.potentialDoorPositions.Count < 1 && roomAsset.forcedDoorPositions.Count < 1)
                {
                    Debug.Log("Room " + roomName + " has no door positions! Room will not be loaded.");
                }
                roomAsset.name = "CUSTOM_" + roomName;
                roomAsset.minItemValue = roomData.minItemValue;
                roomAsset.maxItemValue = roomData.maxItemValue;
                roomAsset.windowChance = roomData.windowChance;
                loadedRooms.Add(roomAsset, roomData);

                Debug.Log("Custom room: " + roomName + " loaded succesfully.");
            }

            yield break;
        }

        void AddToGenerator(string floorName, int floorNumber, SceneObject sceneObject)
        {
            CustomLevelObject[] lds = sceneObject.GetCustomLevelObjects();

            foreach (KeyValuePair<ExtendedRoomAsset, RoomJsonData> room in loadedRooms)
            {
                for (int i = 0; i < lds.Length; i++)
                {
                    if (room.Value.floorSpawns.Contains(floorNumber) && floorName.StartsWith("F") && DoesRoomFitWithLevelType(room, lds[i]))
                    {
                        RoomGroup group;
                        switch (room.Value.roomType)
                        {
                            default: // Halls
                                lds[i].potentialPrePlotSpecialHalls = lds[i].potentialPrePlotSpecialHalls.AddToArray(new WeightedRoomAsset() { selection = room.Key, weight = room.Value.spawnWeights[Array.IndexOf(room.Value.floorSpawns, floorNumber)] });
                                break;
                            case 1: // Classrooms
                                group = lds[i].roomGroup.FirstOrDefault(x => x.name == "Class");
                                if (group != null)
                                {
                                    group.potentialRooms = group.potentialRooms.AddToArray(new WeightedRoomAsset() { selection = room.Key, weight = room.Value.spawnWeights[Array.IndexOf(room.Value.floorSpawns, floorNumber)] });
                                }
                                break;
                            case 2: // Faculty Rooms
                                group = lds[i].roomGroup.FirstOrDefault(x => x.name == "Faculty");
                                if (group != null)
                                {
                                    group.potentialRooms = group.potentialRooms.AddToArray(new WeightedRoomAsset() { selection = room.Key, weight = room.Value.spawnWeights[Array.IndexOf(room.Value.floorSpawns, floorNumber)] });
                                }
                                break;
                            case 3: // Offices
                                group = lds[i].roomGroup.FirstOrDefault(x => x.name == "Office");
                                if (group != null)
                                {
                                    group.potentialRooms = group.potentialRooms.AddToArray(new WeightedRoomAsset() { selection = room.Key, weight = room.Value.spawnWeights[Array.IndexOf(room.Value.floorSpawns, floorNumber)] });
                                }
                                break;
                            case 6: // Libraries
                                lds[i].potentialSpecialRooms = lds[i].potentialSpecialRooms.AddToArray(new WeightedRoomAsset() { selection = room.Key, weight = room.Value.spawnWeights[Array.IndexOf(room.Value.floorSpawns, floorNumber)] });
                                break;
                            case 7: // Cafeterias
                                lds[i].potentialSpecialRooms = lds[i].potentialSpecialRooms.AddToArray(new WeightedRoomAsset() { selection = room.Key, weight = room.Value.spawnWeights[Array.IndexOf(room.Value.floorSpawns, floorNumber)] });
                                break;
                            case 8: // Playgrounds
                                lds[i].potentialSpecialRooms = lds[i].potentialSpecialRooms.AddToArray(new WeightedRoomAsset() { selection = room.Key, weight = room.Value.spawnWeights[Array.IndexOf(room.Value.floorSpawns, floorNumber)] });
                                break;
                        }
                        sceneObject.MarkAsNeverUnload();
                    }
                    if (room.Value.inEndless && floorName == "END" && DoesRoomFitWithLevelType(room, lds[i]) && DoesRoomFitWithEndlessSize(room, lds[i]))
                    {
                        RoomGroup group;
                        switch (room.Value.roomType)
                        {
                            default: // Halls
                                lds[i].potentialPrePlotSpecialHalls = lds[i].potentialPrePlotSpecialHalls.AddToArray(new WeightedRoomAsset() { selection = room.Key, weight = GetWeightFromEndlessSize(room, lds[i]) });
                                break;
                            case 1: // Classrooms
                                group = lds[i].roomGroup.FirstOrDefault(x => x.name == "Class");
                                if (group != null)
                                {
                                    group.potentialRooms = group.potentialRooms.AddToArray(new WeightedRoomAsset() { selection = room.Key, weight = GetWeightFromEndlessSize(room, lds[i]) });
                                }
                                break;
                            case 2: // Faculty Rooms
                                group = lds[i].roomGroup.FirstOrDefault(x => x.name == "Faculty");
                                if (group != null)
                                {
                                    group.potentialRooms = group.potentialRooms.AddToArray(new WeightedRoomAsset() { selection = room.Key, weight = GetWeightFromEndlessSize(room, lds[i]) });
                                }
                                break;
                            case 3: // Offices
                                group = lds[i].roomGroup.FirstOrDefault(x => x.name == "Office");
                                if (group != null)
                                {
                                    group.potentialRooms = group.potentialRooms.AddToArray(new WeightedRoomAsset() { selection = room.Key, weight = GetWeightFromEndlessSize(room, lds[i]) });
                                }
                                break;
                            case 6: // Libraries
                                lds[i].potentialSpecialRooms = lds[i].potentialSpecialRooms.AddToArray(new WeightedRoomAsset() { selection = room.Key, weight = GetWeightFromEndlessSize(room, lds[i]) });
                                break;
                            case 7: // Cafeterias
                                lds[i].potentialSpecialRooms = lds[i].potentialSpecialRooms.AddToArray(new WeightedRoomAsset() { selection = room.Key, weight = GetWeightFromEndlessSize(room, lds[i]) });
                                break;
                            case 8: // Playgrounds
                                lds[i].potentialSpecialRooms = lds[i].potentialSpecialRooms.AddToArray(new WeightedRoomAsset() { selection = room.Key, weight = GetWeightFromEndlessSize(room, lds[i]) });
                                break;
                        }
                        sceneObject.MarkAsNeverUnload();
                    }
                }
            }
        }

        public bool DoesRoomFitWithLevelType(KeyValuePair<ExtendedRoomAsset, RoomJsonData> room, LevelObject level)
        {
            if (room.Value.floorTypeSpawns[0] && level.name.Contains("Schoolhouse"))
            {
                return true;
            }
            if (room.Value.floorTypeSpawns[1] && level.name.Contains("Factory"))
            {
                return true;
            }
            if (room.Value.floorTypeSpawns[2] && level.name.Contains("Maintenance"))
            {
                return true;
            }
            if (room.Value.floorTypeSpawns[3] && level.name.Contains("Laboratory"))
            {
                return true;
            }
            return false;
        }

        public bool DoesRoomFitWithEndlessSize(KeyValuePair<ExtendedRoomAsset, RoomJsonData> room, LevelObject level)
        {
            if (room.Value.floorSpawns.Contains(0) && level.name.Contains("Small"))
            {
                return true;
            }
            if ((room.Value.floorSpawns.Contains(1) || room.Value.floorSpawns.Contains(3)) && level.name.Contains("Medium"))
            {
                return true;
            }
            if ((room.Value.floorSpawns.Contains(2) || room.Value.floorSpawns.Contains(4)) && level.name.Contains("Large"))
            {
                return true;
            }
            return false;
        }

        public int GetWeightFromEndlessSize(KeyValuePair<ExtendedRoomAsset, RoomJsonData> room, LevelObject level)
        {
            if (room.Value.floorSpawns.Contains(0) && level.name.Contains("Small"))
            {
                return room.Value.spawnWeights[Array.IndexOf(room.Value.floorSpawns, 0)];
            }
            if (room.Value.floorSpawns.Contains(1) && level.name.Contains("Medium") && level.name.Contains("Schoolhouse"))
            {
                return room.Value.spawnWeights[Array.IndexOf(room.Value.floorSpawns, 1)];
            }
            if (room.Value.floorSpawns.Contains(2) && level.name.Contains("Large") && level.name.Contains("Schoolhouse"))
            {
                return room.Value.spawnWeights[Array.IndexOf(room.Value.floorSpawns, 2)];
            }
            if (room.Value.floorSpawns.Contains(3) && level.name.Contains("Medium") && !level.name.Contains("Schoolhouse"))
            {
                return room.Value.spawnWeights[Array.IndexOf(room.Value.floorSpawns, 3)];
            }
            if (room.Value.floorSpawns.Contains(4) && level.name.Contains("Large") && !level.name.Contains("Schoolhouse"))
            {
                return room.Value.spawnWeights[Array.IndexOf(room.Value.floorSpawns, 4)];
            }
            return 0;
        }
    }
}
