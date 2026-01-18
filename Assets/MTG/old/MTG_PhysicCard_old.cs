using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

namespace MTG
{
    public class MTG_PhysicCard_old : UdonSharpBehaviour
    {
        
        // Préfixe coloré pour les logs
        private const string LOG_PREFIX = "<color=#FF1493>[MTG_PhysicCard]</color> ";
        
        [Header("=== REFERENCES ===")]
        public RawImage cardImage; // Face avant
        public RawImage cardImageBack; // Face arrière
        public GameObject loading;
        public GameObject flipButton; // Bouton pour flip la carte
        public MTG_Manager manager;
        public VRC_Pickup pickup; // Reference au VRCPickup component
        
        [Header("=== SYNC DATA ===")]
        [UdonSynced] public string cardKey = "";
        [UdonSynced] private Vector3 syncedPosition;
        [UdonSynced] private Quaternion syncedRotation;

        [Header("=== ATLAS DATA ===")]
        public Rect uvRect;
        public int atlasIndex = -1;
        public Rect uvRectBack; // UV pour la face arrière
        public int atlasIndexBack = -1; // Atlas pour la face arrière
        public float lastRetryTime = 0f;
        public const float RETRY_INTERVAL = 10f;
        [SerializeField] private bool imageLoaded = false;
        [SerializeField] private bool imageBackLoaded = false;
        [SerializeField] private bool isFlipped = false; // État actuel de la carte
        [SerializeField] private bool isDoubleFaced = false; // La carte a-t-elle deux faces?
        
        [Header("=== ZOOM SETTINGS ===")]
        [SerializeField] private float zoomScale = 2.0f; // Taille quand zoomed
        private Vector3 originalScale; // Taille initiale
        private bool isZoomed = false;
        
        // Éviter la boucle de déserialisation
        private string lastDeserializedKey = "";
        
        private void Start()
        {
            Debug.Log(LOG_PREFIX + "Start called");
            
            // Sauvegarder la taille initiale
            originalScale = transform.localScale;
            
            // Trouver le VRCPickup component
            if (pickup == null)
            {
                pickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
            }
            
            // Trouver le manager automatiquement s'il n'est pas assigné
            if (manager == null)
            {
                TryFindManager();
            }
            else
            {
                Debug.Log(LOG_PREFIX + "Manager already assigned");
            }
            
            // S'assurer que le loading est activé au départ si présent
            if (loading != null)
            {
                loading.SetActive(true);
            }
            
            // Initialiser l'état du flip
            UpdateFlipVisibility();
            
            // Si on a déjà un cardKey au start (pour les cartes déjà spawned), charger l'image
            if (!string.IsNullOrEmpty(cardKey))
            {
                Debug.Log(LOG_PREFIX + $"Start: cardKey already set = {cardKey}");
                imageLoaded = false;
                lastRetryTime = -RETRY_INTERVAL;
                SetImageFromId();
            }
        }
        
        public override void OnDeserialization()
        {
            // Éviter la boucle: ne traiter que si le cardKey a changé
            if (cardKey == lastDeserializedKey)
            {
                // Mais toujours synchroniser la position
                if (!Networking.IsOwner(gameObject))
                {
                    transform.position = syncedPosition;
                    transform.rotation = syncedRotation;
                }
                return;
            }
            
            Debug.Log(LOG_PREFIX + $"OnDeserialization: cardKey = {cardKey}, manager = {manager != null}");
            
            // Sauvegarder le cardKey pour éviter de retraiter
            lastDeserializedKey = cardKey;
            
            // Synchroniser la position
            if (!Networking.IsOwner(gameObject))
            {
                transform.position = syncedPosition;
                transform.rotation = syncedRotation;
            }
            
            // Trouver le manager si pas encore assigné
            if (manager == null)
            {
                TryFindManager();
            }
            
            // Quand on reçoit de nouvelles données sync, charger l'image
            if (!string.IsNullOrEmpty(cardKey))
            {
                Debug.Log(LOG_PREFIX + $"OnDeserialization: cardKey synced = {cardKey}");
                imageLoaded = false;
                lastRetryTime = -RETRY_INTERVAL; // Permettre un retry immédiat
                SetImageFromId();
            }
        }
        
        public void InitializeCard(string newCardKey, MTG_Manager newManager)
        {
            Debug.Log(LOG_PREFIX + $"InitializeCard called with cardKey: {newCardKey}, isOwner: {(Networking.LocalPlayer != null ? Networking.IsOwner(gameObject).ToString() : "LocalPlayer null")}");
            
            // Assigner le manager
            manager = newManager;
            
            // Set le cardKey (l'appelant doit être le owner au moment du spawn)
            cardKey = newCardKey;
            
            // Initialiser la position synced avec la position actuelle
            syncedPosition = transform.position;
            syncedRotation = transform.rotation;
            
            Debug.Log(LOG_PREFIX + $"Card initialized with ID: {cardKey}");
            
            // Si on est le owner, synchroniser
            if (Networking.LocalPlayer != null && Networking.IsOwner(gameObject))
            {
                RequestSerialization();
                Debug.Log(LOG_PREFIX + "Card serialization requested");
                
                // Charger l'image immédiatement pour le owner
                imageLoaded = false;
                lastRetryTime = -RETRY_INTERVAL;
                SetImageFromId();
            }
        }
        
        public void SetImageFromId()
        {
            if (manager == null || string.IsNullOrEmpty(cardKey))
            {
                Debug.LogWarning(LOG_PREFIX + $"SetImageFromId: Missing manager ({manager != null}) or cardKey ({!string.IsNullOrEmpty(cardKey)})");
                return;
            }
            
            // Exception pour les cartes Debug - ne pas charger d'image
            if (cardKey == "Debug")
            {
                Debug.Log(LOG_PREFIX + "Card is Debug card, skipping image load");
                imageLoaded = true; // Marquer comme chargé pour ne plus retenter
                if (loading != null)
                {
                    loading.SetActive(false);
                }
                return;
            }
            
            Debug.Log(LOG_PREFIX + $"SetImageFromId: Searching for card {cardKey} in atlas cache");
            
            // Extraire l'ID de base et l'index de face depuis le cardKey (format: "cardId:0" ou "cardId:1")
            string baseCardId = cardKey;
            int faceIndex = 0;
            if (cardKey.Contains(":"))
            {
                string[] parts = cardKey.Split(':');
                if (parts.Length == 2)
                {
                    baseCardId = parts[0];
                    int.TryParse(parts[1], out faceIndex);
                }
            }
            
            // Charger la face avant (face 0)
            string frontFaceKey = baseCardId + ":0";
            int foundAtlasIndex;
            Rect foundRect;
            if (manager.GetAtlasInfoForCard(frontFaceKey, out foundAtlasIndex, out foundRect))
            {
                Debug.Log(LOG_PREFIX + $"SetImageFromId: Found front face {frontFaceKey} in atlas {foundAtlasIndex}");
                this.atlasIndex = foundAtlasIndex;
                this.uvRect = foundRect;
                
                Texture2D atlasTexture = manager.GetAtlasTexture(atlasIndex);
                if (atlasTexture != null)
                {
                    Debug.Log(LOG_PREFIX + $"SetImageFromId: Atlas texture ready for front face, applying now");
                    ApplyAtlasTexture(atlasTexture, false);
                }
            }
            
            // Tenter de charger la face arrière (face 1)
            string backFaceKey = baseCardId + ":1";
            int foundAtlasIndexBack;
            Rect foundRectBack;
            if (manager.GetAtlasInfoForCard(backFaceKey, out foundAtlasIndexBack, out foundRectBack))
            {
                Debug.Log(LOG_PREFIX + $"SetImageFromId: Found back face {backFaceKey} in atlas {foundAtlasIndexBack}");
                this.atlasIndexBack = foundAtlasIndexBack;
                this.uvRectBack = foundRectBack;
                this.isDoubleFaced = true;
                
                Texture2D atlasTextureBack = manager.GetAtlasTexture(atlasIndexBack);
                if (atlasTextureBack != null)
                {
                    Debug.Log(LOG_PREFIX + $"SetImageFromId: Atlas texture ready for back face, applying now");
                    ApplyAtlasTexture(atlasTextureBack, true);
                }
            }
            else
            {
                // Pas de face arrière = carte simple face
                this.isDoubleFaced = false;
            }
            
            // Mettre à jour la visibilité du bouton flip
            UpdateFlipVisibility();
        }
        
        private void Update()
        {
            // Synchroniser la position si on est owner
            if (Networking.IsOwner(gameObject))
            {
                // Vérifier si la position a changé
                if (Vector3.Distance(transform.position, syncedPosition) > 0.01f || 
                    Quaternion.Angle(transform.rotation, syncedRotation) > 0.5f)
                {
                    syncedPosition = transform.position;
                    syncedRotation = transform.rotation;
                    RequestSerialization();
                }
            }
            
            // Essayer de trouver le manager si manquant
            if (manager == null)
            {
                if (Time.time - lastRetryTime >= 1f) // Retry toutes les secondes
                {
                    lastRetryTime = Time.time;
                    TryFindManager();
                }
            }
            
            // Détecter touche "E" ou clic gauche (Use) pour zoom en desktop (quand on tient la carte)
            // En VR, le zoom est géré par OnPickupUseDown/Up (trigger)
            if (pickup != null && Networking.LocalPlayer != null && Networking.LocalPlayer.IsUserInVR() == false)
            {
                if (pickup.currentPlayer == Networking.LocalPlayer)
                {
                    // Maintenir "E" ou clic gauche = zoom, relâcher = dezoom
                    if (Input.GetKey(KeyCode.E) || Input.GetMouseButton(0))
                    {
                        if (!isZoomed)
                        {
                            ZoomIn();
                        }
                    }
                    else
                    {
                        if (isZoomed)
                        {
                            ZoomOut();
                        }
                    }
                }
                else if (isZoomed)
                {
                    // Si on ne tient plus la carte, dezoom
                    ZoomOut();
                }
            }
            
            // Si on n'a pas encore chargé l'image, essayer de la récupérer via le manager
            if (!imageLoaded && manager != null && !string.IsNullOrEmpty(cardKey))
            {
                if (Time.time - lastRetryTime >= RETRY_INTERVAL)
                {
                    lastRetryTime = Time.time;
                    SetImageFromId();
                }
            }
        }
        
        private void TryFindManager()
        {
            GameObject managerObj = GameObject.Find("MTG_Manager");
            if (managerObj != null)
            {
                manager = managerObj.GetComponent<MTG_Manager>();
                if (manager != null)
                {
                    Debug.Log(LOG_PREFIX + "Manager found successfully!");
                }
                else
                {
                    Debug.LogWarning(LOG_PREFIX + "MTG_Manager GameObject found but no component!");
                }
            }
            else
            {
                Debug.LogWarning(LOG_PREFIX + "MTG_Manager GameObject not found in scene");
            }
        }

        private void ApplyAtlasTexture(Texture2D atlasTexture, bool isBackFace)
        {
            if (isBackFace)
            {
                if (cardImageBack == null)
                {
                    Debug.LogError(LOG_PREFIX + "cardImageBack is null!");
                    return;
                }
                
                // Appliquer la texture et les UVs à la face arrière
                cardImageBack.texture = atlasTexture;
                cardImageBack.uvRect = uvRectBack;
                imageBackLoaded = true;
                
                Debug.Log(LOG_PREFIX + $"Atlas texture applied successfully for back face");
            }
            else
            {
                if (cardImage == null)
                {
                    Debug.LogError(LOG_PREFIX + "cardImage is null!");
                    return;
                }
                
                // Appliquer la texture et les UVs à la face avant
                cardImage.texture = atlasTexture;
                cardImage.uvRect = uvRect;
                imageLoaded = true;
                
                Debug.Log(LOG_PREFIX + $"Atlas texture applied successfully for front face");
            }

            // Désactiver le loading quand au moins la face avant est chargée
            if (imageLoaded && loading != null)
            {
                loading.SetActive(false);
            }
        }
        
        // Méthode pour flip la carte (appelée par le bouton)
        public void FlipCard()
        {
            if (!isDoubleFaced)
            {
                Debug.Log(LOG_PREFIX + "Cannot flip: card is single-faced");
                return;
            }
            
            isFlipped = !isFlipped;
            UpdateFlipVisibility();
            Debug.Log(LOG_PREFIX + $"Card flipped to {(isFlipped ? "back" : "front")} face");
        }
        
        // Met à jour la visibilité des faces et du bouton
        private void UpdateFlipVisibility()
        {
            if (cardImage != null)
            {
                cardImage.gameObject.SetActive(!isFlipped);
            }
            
            if (cardImageBack != null)
            {
                cardImageBack.gameObject.SetActive(isFlipped);
            }
            
            // Afficher le bouton flip seulement si la carte a deux faces
            if (flipButton != null)
            {
                flipButton.SetActive(isDoubleFaced);
            }
        }
        
        private void ZoomIn()
        {
            if (!isZoomed)
            {
                transform.localScale = originalScale * zoomScale;
                isZoomed = true;
                Debug.Log(LOG_PREFIX + $"Card zoomed to {zoomScale}x");
            }
        }
        
        private void ZoomOut()
        {
            if (isZoomed)
            {
                transform.localScale = originalScale;
                isZoomed = false;
                Debug.Log(LOG_PREFIX + "Card returned to original size");
            }
        }
        
        public override void OnPickupUseDown()
        {
            // Trigger en VR = zoom
            ZoomIn();
        }
        
        public override void OnPickupUseUp()
        {
            // Relâcher trigger en VR = dezoom
            ZoomOut();
        }
        
        public override void OnPickup()
        {
            // Prendre ownership quand on grab la carte
            if (Networking.LocalPlayer != null)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                Debug.Log(LOG_PREFIX + "Took ownership on pickup");
            }
        }
        
        public override void OnDrop()
        {
            // Synchroniser la position finale quand on lâche la carte
            if (Networking.LocalPlayer != null && Networking.IsOwner(gameObject))
            {
                syncedPosition = transform.position;
                syncedRotation = transform.rotation;
                RequestSerialization();
                Debug.Log(LOG_PREFIX + "Synced position on drop");
            }
        }
    }
}
