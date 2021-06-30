using BepInEx;
using BepInEx.Configuration;
using Newtonsoft.Json;
using UnityEngine;

using System;
using System.Collections.Generic;
using System.Linq;

namespace LordAshes
{
    [BepInPlugin(Guid, Name, Version)]
    public partial class StatMessaging : BaseUnityPlugin
    {
        // Plugin info
        public const string Name = "Stat Messaging Plug-In";
        public const string Guid = "org.lordashes.plugins.statmessaging";
        public const string Version = "1.3.0.0";

        // Prevent multiple sources from modifying data at once
        private static object exclusionLock = new object();

        // Holds the data associated with each mini
        private static Dictionary<CreatureGuid, string> data = new Dictionary<CreatureGuid, string>();

        // Holds callback subscriptions for message distribution
        private static Dictionary<System.Guid, Subscription> subscriptions = new Dictionary<Guid, Subscription>();

        // Configuration for diagnostic mode
        private bool diagnosticMode = false;
        private ConfigEntry<KeyboardShortcut> triggerDebugMode;

        /// <summary>
        /// Class for holding callback subscriptions
        /// </summary>
        public class Subscription
        {
            public string key { get; set; }
            public Action<Change[]> callback { get; set; }
        }

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

        private static bool ready = false;


        /// <summary>
        /// Method triggered when the plugin loads
        /// </summary>
        public void Awake()
        {
            // Read diagnostic toggle configuration
            triggerDebugMode = Config.Bind("Hotkeys", "Toggle Diagnostic Mode", new KeyboardShortcut(KeyCode.Greater, KeyCode.LeftControl));

            Debug.Log("Stat Messaging Plugin now active. Automatic message checks will being when the board loads.");
            BoardSessionManager.OnStateChange += (s) =>
            {
                if (s.ToString().Contains("+Active"))
                { 
                    ready = true;
                    Debug.Log("Stat Messaging started looking for messages.");
                } 
                else 
                { 
                    ready = false;
                    StatMessaging.Reset();
                    Debug.Log("Stat Messaging stopped looking for messages.");
                }
            };
        }

        /// <summary>
        /// Method triggered periodically
        /// </summary>
        public void Update()
        {
            if (ready) { StatMessagingCheck(); }

            if(StrictKeyCheck(triggerDebugMode.Value))
            {
                diagnosticMode = !diagnosticMode;
            }
        }

        /// <summary>
        /// Method to display the diagnostic information when diagnostic mode is on
        /// </summary>
        public void OnGUI()
        {
            if(diagnosticMode)
            {
                CreatureBoardAsset asset;
                CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out asset);
                if (asset != null)
                {
                    GUIStyle gs = new GUIStyle() { wordWrap = true };
                    GUI.Label(new Rect(10, 30, 1900, 80), "Debug: "+asset.Creature.Name, gs);
                }
            }
        }

        /// <summary>
        /// Method that checks for Stat Messaging changes (changes to Creature Name) itentifies the changes via callback
        /// </summary>
        /// <param name="dataChangeCallback">Callback that accepts a array of </param>
        [Obsolete]
        public static void Check(Action<Change[]> dataChangeCallback)
        {
            foreach (Subscription subscription in subscriptions.Values)
            {
                if(subscription.callback == dataChangeCallback) { return; }
            }
            Debug.LogWarning("Stat Messaging method Check(callback) is obsolete. Use the Subscribe() method instead.");
            Subscribe("*", dataChangeCallback);
        }

        /// <summary>
        /// Method to subscribe to Stat Messages of a certain key
        /// (Guids are used for subscription removal instead of the key so that multiple plugins can be looking at the same messages)
        /// </summary>
        /// <param name="key">The key of the messages for which changes should trigger callbacks</param>
        /// <param name="dataChangeCallback">Callback that receives the changes</param>
        /// <returns>Guid associated with the subscription which can be used to unsubscribe</returns>
        public static System.Guid Subscribe(string key, Action<Change[]> dataChangeCallback)
        {
            System.Guid guid = System.Guid.NewGuid();
            subscriptions.Add(guid, new Subscription() { key = key, callback = dataChangeCallback });
            return guid;
        }

        /// <summary>
        /// Method to remove a subscription associated with a specific Guid
        /// (Guids are used for subscription removal instead of the key so that multiple plugins can be looking at the same messages)
        /// </summary>
        /// <param name="subscriptionId">Guid of the subscription to be removed (provided by the Subscribe method)</param>
        public static void Unsubscribe(System.Guid subscriptionId)
        {
            if (subscriptions.ContainsKey(subscriptionId)) { subscriptions.Remove(subscriptionId); }
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
        /// Method used to read the last recorded value for a particular key on a particular creature
        /// (typically used to get current values for things like inputs)
        /// </summary>
        /// <param name="cid">Identification of the creature whose key is to bb read</param>
        /// <param name="key">Identification of the key to be read</param>
        /// <returns>Value of the key or an empty string if the key is not set or the cid is invalid</returns>
        public static string ReadInfo(CreatureGuid cid, string key)
        {
            // Minimize race conditions
            lock (exclusionLock)
            {
                if (data.ContainsKey(cid))
                {
                    string json = data[cid];
                    json = json.Substring(json.IndexOf("<size=0>") + "<size=0>".Length);
                    Dictionary<string, string> keys = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if(keys.ContainsKey(key))
                    {
                        return keys[key];
                    }
                }
                else
                {
                    Debug.LogWarning("Creature '" + cid + "' not defined in data dictionary");
                }
                return "";
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

        /// <summary>
        /// Method to get Creature Name
        /// </summary>
        /// <param name="asset">CreatureBoardAsset</param>
        /// <returns>String representation of the creature name</returns>
        public static string GetCreatureName(CreatureBoardAsset asset)
        {
            string name = asset.Creature.Name;
            if (name.Contains("<size=0>")) { name = name.Substring(0, name.IndexOf("<size=0>")).Trim(); }
            return name;
        }

        /// <summary>
        /// Method to get Creature Name
        /// </summary>
        /// <param name="asset">CreatureBoardAsset</param>
        /// <returns>String representation of the creature name</returns>
        public static string GetCreatureName(Creature creature)
        {
            string name = creature.Name;
            if (name.Contains("<size=0>")) { name = name.Substring(0, name.IndexOf("<size=0>")).Trim(); }
            return name;
        }

        /// <summary>
        /// Method that performs actual checks for stat messages
        /// </summary>
        private static void StatMessagingCheck()
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
                        if (data.ContainsKey(asset.Creature.CreatureId))
                        {
                            Debug.Log("Restoring previous data for Creature '"+GetCreatureName(asset)+"' ("+asset.Creature.CreatureId+"). Probably lost due to a character rename.");
                            data[asset.Creature.CreatureId] = GetCreatureName(asset) + data[asset.Creature.CreatureId].Substring(data[asset.Creature.CreatureId].IndexOf("<size=0>"));
                            CreatureManager.SetCreatureName(asset.Creature.CreatureId, data[asset.Creature.CreatureId]);
                            Debug.Log("Creature '" + GetCreatureName(asset) + "' is now '" + data[asset.Creature.CreatureId] + "'");
                            creatureName = data[asset.Creature.CreatureId];
                        }
                        else
                        {
                            Debug.Log("Creating new data block for Creature '" + GetCreatureName(asset) + "' (" + asset.Creature.CreatureId + "). This is probably a new asset.");
                            CreatureManager.SetCreatureName(asset.Creature.CreatureId, creatureName + " <size=0>{}");
                            creatureName = creatureName + " <size=0>{}";
                        }
                    }
                    // Ensure that creature has a entry in the data dictionary
                    if (!data.ContainsKey(asset.Creature.CreatureId))
                    {
                        data.Add(asset.Creature.CreatureId, creatureName.Substring(0, creatureName.IndexOf("<size=0>")) + "<size=0>{}");
                    }
                    // Check to see if the creature name has changed
                    if (creatureName != data[asset.Creature.CreatureId])
                    {
                        // Extract JSON ending
                        string lastJson = data[asset.Creature.CreatureId].Substring(data[asset.Creature.CreatureId].IndexOf("<size=0>") + "<size=0>".Length);
                        string currentJson = creatureName.Substring(creatureName.IndexOf("<size=0>") + "<size=0>".Length);
                        // Compare entries
                        Dictionary<string, string> last = JsonConvert.DeserializeObject<Dictionary<string, string>>(lastJson);
                        Dictionary<string, string> current = JsonConvert.DeserializeObject<Dictionary<string, string>>(currentJson);
                        // Update data dictionary with current info
                        data[asset.Creature.CreatureId] = creatureName;
                        List<Change> changes = new List<Change>();
                        // Compare entries in the last data to current data
                        foreach (KeyValuePair<string, string> entry in last)
                        {
                            // If last data does not appear in current data then the data was removed
                            if (!current.ContainsKey(entry.Key))
                            {
                                changes.Add(new Change() { action = ChangeType.removed, key = entry.Key, previous = entry.Value, value = "", cid = asset.Creature.CreatureId });
                            }
                            else
                            {
                                // If last data does not match current data then the data has been modified
                                if (entry.Value != current[entry.Key])
                                {
                                    changes.Add(new Change() { action = ChangeType.modified, key = entry.Key, previous = entry.Value, value = current[entry.Key], cid = asset.Creature.CreatureId });
                                };
                            }
                        }
                        // Compare entries in current data to last data
                        foreach (KeyValuePair<string, string> entry in current)
                        {
                            // If current data does not exist in last data then a new entry has been added
                            if (!last.ContainsKey(entry.Key))
                            {
                                changes.Add(new Change() { action = ChangeType.added, key = entry.Key, previous = "", value = entry.Value, cid = asset.Creature.CreatureId });
                            };
                        }

                        // Process callback if there were any changes
                        if (changes.Count > 0)
                        {
                            // Run through each change
                            foreach (Change change in changes)
                            {
                                Debug.Log("Stat Messaging Change - Cid: " + change.cid + ", Type: " + change.action.ToString() + ", Key: " + change.key + ", Previous: " + change.previous + ", Current: " + change.value);
                                // Check each subscription
                                foreach (Subscription subscription in subscriptions.Values)
                                {
                                    // Trigger a callback for anyone subscription matching the key
                                    if (subscription.key == change.key)
                                    {
                                        subscription.callback(new Change[] { change });
                                    }
                                }
                            }
                            // Check for legacy wild card subscriptions
                            foreach (Subscription subscription in subscriptions.Values)
                            {
                                if (subscription.key == "*") { subscription.callback(changes.ToArray()); }
                            }
                        }
                    }
                }
            }
            catch (Exception x) { Debug.LogWarning(x); }

            // Indicated that next check is allowed
            checkInProgress = false;
        }

        /// <summary>
        /// Method to properly evaluate shortcut keys. 
        /// </summary>
        /// <param name="check"></param>
        /// <returns></returns>
        public bool StrictKeyCheck(KeyboardShortcut check)
        {
            if (!check.IsUp()) { return false; }
            foreach (KeyCode modifier in new KeyCode[] { KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftShift, KeyCode.RightShift })
            {
                if (Input.GetKey(modifier) != check.Modifiers.Contains(modifier)) { return false; }
            }
            return true;
        }
    }
}
