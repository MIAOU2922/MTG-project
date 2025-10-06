using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.StringLoading;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.Data;

namespace MTG
{
    public class MTG_Manager : UdonSharpBehaviour
    {
        [UdonSynced, SerializeField]
        protected int instanceID = -1;
        [SerializeField]
        private MTG_SyncInterface syncInterface;
        [SerializeField]
        private bool _isSyncing = false;
        [SerializeField]
        private bool agree = false;
        [SerializeField]
        protected VRCUrl createURL;
        [SerializeField]
        protected VRCUrl searchURL;
        [SerializeField]
        protected VRCUrl[] joinURLs;
        [SerializeField]
        protected VRCUrl[] tempURLs;


#if !COMPILER_UDONSHARP && UNITY_EDITOR
        public VRCUrl BaseURL = new VRCUrl("http://localhost:5000/a");

        private void OnValidate()
        {
            createURL = new VRCUrl($"{BaseURL}c");
            searchURL = new VRCUrl($"{BaseURL}s?q=");
            joinURLs = new VRCUrl[32];
            for (int i = 0; i < joinURLs.Length; i++)
                joinURLs[i] = new VRCUrl($"{BaseURL}j{ToBase36(i)}");
            tempURLs = new VRCUrl[2048];
            for (int i = 0; i < tempURLs.Length; i++)
                tempURLs[i] = new VRCUrl($"{BaseURL}t{ToBase36(i)}");
        }

        private string ToBase36(int value)
        {
            const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
            if (value == 0) return "0";
            string result = "";
            while (value > 0)
            {
                result = chars[value % chars.Length] + result;
                value /= chars.Length;
            }
            return result;
        }
#endif

        public override void OnDeserialization()
        {
            if (_isSyncing || agree) return;
            syncInterface.Show();
        }

        public override void OnMasterTransferred(VRCPlayerApi newMaster)
        {
            if (_isSyncing || !newMaster.isLocal || agree) return;
            syncInterface.Show();
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (_isSyncing || !player.isLocal || agree) return;
            syncInterface.Show();
        }

        internal void JoinGame(bool show)
        {
            if (_isSyncing || agree) return;
            if (instanceID == -1)
                VRCStringDownloader.LoadUrl(createURL, (IUdonEventReceiver)this);
            else VRCStringDownloader.LoadUrl(joinURLs[instanceID], (IUdonEventReceiver)this);
            if (show) syncInterface.ShowLoading();
            _isSyncing = true;
        }

        private bool IsJoinOrCreateResponse(IVRCStringDownload json)
        {
            if (json == null)
            {
                Debug.LogError("Null JSON in response");
                return false;
            }

            if (json.Url == null)
            {
                Debug.LogError("Null URL in response");
                return false;
            }

            var url = json.Url.ToString();
            if (url == createURL.ToString())
                return true;
            for (int i = 0; i < joinURLs.Length; i++)
                if (url == joinURLs[i].ToString())
                    return true;
            return false;
        }

        public override void OnStringLoadSuccess(IVRCStringDownload json)
        {
            if (!IsJoinOrCreateResponse(json)) return;

            if (VRCJson.TryDeserializeFromJson(json.Result, out DataToken result))
            {
                if (result.TokenType != TokenType.DataDictionary)
                {
                    Debug.LogError($"Error parsing response: {json.Result}");
                    _isSyncing = false;
                    if (!agree)
                        syncInterface.Show();
                    return;
                }

                var dict = result.DataDictionary;
                
                // Vérifier si l'objet "instance" existe
                if (!dict.ContainsKey("instance") || dict["instance"].TokenType != TokenType.DataDictionary)
                {
                    Debug.LogError($"Error parsing response: missing 'instance' object: {json.Result}");
                    _isSyncing = false;
                    if (!agree)
                        syncInterface.Show();
                    return;
                }

                var instanceDict = dict["instance"].DataDictionary;
                
                // Vérifier si l'ID existe dans l'instance
                if (!instanceDict.ContainsKey("id") || instanceDict["id"].TokenType != TokenType.Double)
                {
                    Debug.LogError($"Error parsing response: missing 'id' in instance object: {json.Result}");
                    _isSyncing = false;
                    if (!agree)
                        syncInterface.Show();
                    return;
                }

                // SOLUTION : Utiliser .Double puis convertir en int
                instanceID = (int)instanceDict["id"].Double;
                
                // OU alternative plus sûre :
                // instanceID = Mathf.RoundToInt((float)instanceDict["id"].Double);

                Debug.Log($"Joined game {instanceID}");
                agree = true;
                _isSyncing = false;
                syncInterface.Hide();
                return;
            }

            Debug.LogError($"Error parsing response: {json.Result}");
            _isSyncing = false;
            if (!agree)
                syncInterface.Show();
        }


        public override void OnStringLoadError(IVRCStringDownload result)
        {
            if (!IsJoinOrCreateResponse(result)) return;

            Debug.LogError($"Error loading URL: {result.Error}");
            _isSyncing = false;
            if (!agree)
                syncInterface.Show();
        }
        
        public int GetInstanceID()
        {
            return instanceID;
        }
    }
}