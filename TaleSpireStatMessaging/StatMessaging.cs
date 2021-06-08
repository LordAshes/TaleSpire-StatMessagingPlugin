using BepInEx;
using Newtonsoft.Json;
using UnityEngine;

using System;
using System.Collections.Generic;

namespace LordAshes
{
    [BepInPlugin(Guid, Name, Version)]
    public partial class StatMessaging : BaseUnityPlugin
    {
        // Plugin info
        public const string Name = "Stat Messaging Plug-In";
        public const string Guid = "org.lordashes.plugins.statmessaging";
        public const string Version = "1.1.0.0";

        private static object exclusionLock = new object();

        private static Dictionary<CreatureGuid, string> data = new Dictionary<CreatureGuid, string>();

        /// <summary>
        /// Enumeration to determine what type of change occured
        /// </summary>
        public enum ChangeType
        {
            added = 1,
            modified,
            removed
        }

        /// <summary>
        /// Class to define Stat Message block changes
        /// </summary>
        public class Change
        {
            public CreatureGuid cid { get; set; }
            public ChangeType action { get; set; }
            public string key { get; set; }
            public string previous { get; set; }
            public string value { get; set; }
        }

        // Variable to prevent overlapping checks in case the check is taking too long
        private static bool checkInProgress = false;

        /// <summary>
        /// Method that checks for Stat Messaging changes (changes to Creature Name) itentifies the changes via callback
        /// </summary>
        /// <param name="dataChangeCallback">Callback that accepts a array of </param>
        public static void Check(Action<Change[]> dataChangeCallback)
        {
            if (checkInProgress) { return; }

            // Prevent overlapping checks (in case checks are taking too long)
            checkInProgress = true;

            try
            {
                // Check all creatures
                foreach (CreatureBoardAsset asset in CreaturePresenter.AllCreatureAssets)
                {
                    // Read the creature name into a string. Routine will use this because setting name takes time (i.e. is not reflected immediately).
                    string creatureName = asset.Creature.Name;

                    // Ensure creature has a JSON Block
                    if (!creatureName.Contains("<size=0>"))
                    {
                        Debug.Log("Adding JSON Block to '" + asset.Creature.CreatureId + "'");
                        if (data.ContainsKey(asset.Creature.CreatureId))
                        {
                            Debug.Log("Restoring previous data. Probably lost due to a character rename");
                            CreatureManager.SetCreatureName(asset.Creature.CreatureId, data[asset.Creature.CreatureId]);
                            creatureName = data[asset.Creature.CreatureId];
                        }
                        else
                        {
                            Debug.Log("Creating new data block. This is probably a new asset");
                            CreatureManager.SetCreatureName(asset.Creature.CreatureId, creatureName + " <size=0>{}");
                            creatureName = creatureName + " <size=0>{}";
                        }
                    }
                    // Ensure that creature has a entry in the data dictionary
                    if (!data.ContainsKey(asset.Creature.CreatureId))
                    {
                        Debug.Log("Adding Mini '" + asset.Creature.CreatureId + "' to the data dictionary...");
                        data.Add(asset.Creature.CreatureId, creatureName.Substring(0, creatureName.IndexOf("<size=0>")) + "<size=0>{}");
                    }
                    // Check to see if the creature name has changed
                    if (creatureName != data[asset.Creature.CreatureId])
                    {
                        Debug.Log("Change Detected (current) '" + creatureName + "' vs (dictionary) '" + data[asset.Creature.CreatureId] + "'");
                        // Extract JSON ending
                        string lastJson = data[asset.Creature.CreatureId].Substring(data[asset.Creature.CreatureId].IndexOf("<size=0>") + "<size=0>".Length);
                        string currentJson = creatureName.Substring(creatureName.IndexOf("<size=0>") + "<size=0>".Length);
                        Debug.Log("Last: " + lastJson);
                        Debug.Log("Current: " + currentJson);
                        // Compare entries
                        Debug.Log("Deserializing Last");
                        Dictionary<string, string> last = JsonConvert.DeserializeObject<Dictionary<string, string>>(lastJson);
                        Debug.Log("Deserializing Current");
                        Dictionary<string, string> current = JsonConvert.DeserializeObject<Dictionary<string, string>>(currentJson);
                        // Update data dictionary with current info
                        Debug.Log("Updating Data To Match Name");
                        data[asset.Creature.CreatureId] = creatureName;
                        Debug.Log("Conversion to dictionaries complete");
                        List<Change> changes = new List<Change>();
                        // Compare entries in the last data to current data
                        foreach (KeyValuePair<string, string> entry in last)
                        {
                            // If last data does not appear in current data then the data was removed
                            Debug.Log("Last had '" + entry.Key + "'. Current too? " + current.ContainsKey(entry.Key));
                            if (!current.ContainsKey(entry.Key))
                            {
                                Debug.Log("Adding Removed Change");
                                changes.Add(new Change() { action = ChangeType.removed, key = entry.Key, previous = entry.Value, value = "", cid = asset.Creature.CreatureId });
                            }
                            else
                            {
                                // If last data does not match current data then the data has been modified
                                Debug.Log("Last had '" + entry.Key + "'='" + entry.Value + "'. Current has '" + entry.Key + "'='" + current[entry.Key] + "'");
                                if (entry.Value != current[entry.Key])
                                {
                                    Debug.Log("Adding Modified Change");
                                    changes.Add(new Change() { action = ChangeType.modified, key = entry.Key, previous = entry.Value, value = current[entry.Key], cid = asset.Creature.CreatureId });
                                };
                            }
                        }
                        // Compare entries in current data to last data
                        foreach (KeyValuePair<string, string> entry in current)
                        {
                            // If current data does not exist in last data then a new entry has been added
                            Debug.Log("Current has '" + entry.Key + "'. Current too? " + last.ContainsKey(entry.Key));
                            if (!last.ContainsKey(entry.Key))
                            {
                                Debug.Log("Adding Added Change");
                                changes.Add(new Change() { action = ChangeType.added, key = entry.Key, previous = "", value = entry.Value, cid = asset.Creature.CreatureId });
                            };
                        }
                        Debug.Log("Comparisons complete");
                        Debug.Log("Data updated. Changes = " + changes.Count);

                        // Process callback if there were any changes
                        if (changes.Count > 0) { Debug.Log("Triggering callback"); dataChangeCallback(changes.ToArray()); }
                    }
                }
            }
            catch (Exception x) { Debug.LogWarning(x); }

            // Indicated that next check is allowed
            checkInProgress = false;
        }

        /// <summary>
        /// Method to set a new piece of information or modify an existing one in Stat Messaging block
        /// </summary>
        /// <param name="cid">CreatureId whose block is to be changed</param>
        /// <param name="key">String key for which data is to be changed (e.g. plugin unique identifier)</param>
        /// <param name="value">String value of the key (e.g. value to be communicated)</param>
        public static void SetInfo(CreatureGuid cid, string key, string value)
        {
            // Minimize race conditions
            lock (exclusionLock)
            {
                // Get access to the corresponding asset
                CreatureBoardAsset asset = null;
                CreaturePresenter.TryGetAsset(cid, out asset);
                if (asset != null)
                {
                    // Extract the JSON portion of the name
                    string json = asset.Creature.Name.Substring(asset.Creature.Name.IndexOf("<size=0>") + "<size=0>".Length);
                    Debug.Log("JSON: '" + json + "'");
                    // Convert to a dictionary
                    Dictionary<string, string> info = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    // Modify or add the specified value under the specified key
                    if (info.ContainsKey(key)) { info[key] = value; } else { info.Add(key, value); }
                    // Update character name
                    CreatureManager.SetCreatureName(cid, asset.Creature.Name.Substring(0, asset.Creature.Name.IndexOf("<size=0>") + "<size=0>".Length) + JsonConvert.SerializeObject(info));
                }
            }
        }

        /// <summary>
        /// Method to clear a piece of information in Stat Messaging block
        /// </summary>
        /// <param name="cid">CreatureId whose block is to be changed</param>
        /// <param name="key">String key for which data is to be cleared (e.g. plugin unique identifier)</param>
        public static void ClearInfo(CreatureGuid cid, string key)
        {
            // Minimize race conditions
            lock (exclusionLock)
            {
                CreatureBoardAsset asset = null;
                CreaturePresenter.TryGetAsset(cid, out asset);
                if (asset != null)
                {
                    // Extract the JSON portion of the name
                    string json = asset.Creature.Name.Substring(asset.Creature.Name.IndexOf("<size=0>") + "<size=0>".Length);
                    // Convert to a dictionary
                    Dictionary<string, string> info = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    // Remove the key if it exists
                    if (info.ContainsKey(key)) { info.Remove(key); }
                    // Update character name
                    CreatureManager.SetCreatureName(cid, asset.Creature.Name.Substring(0, asset.Creature.Name.IndexOf("<size=0>") + "<size=0>".Length) + JsonConvert.SerializeObject(info));
                }
            }
        }

        /// <summary>
        /// Method used to reset the data dictionary and thus reprocess all Stat Message changes.
        /// Typically used on a new board load to dump old board data and also to re-process it if the board is reloaded.
        /// </summary>
        public static void Reset()
        {
            Debug.Log("Stat Messaging data dictionary reset");
            data.Clear();
        }
    }
}
