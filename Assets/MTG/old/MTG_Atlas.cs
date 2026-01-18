using UdonSharp;
using UnityEngine;

namespace MTG
{
    [System.Serializable]
    public struct MTG_Atlas
    {
        public Texture2D image;
        public string[] cardIds; // 24 ids max
        public Rect[] cardRects; // 24 offsets UV
    }
}
