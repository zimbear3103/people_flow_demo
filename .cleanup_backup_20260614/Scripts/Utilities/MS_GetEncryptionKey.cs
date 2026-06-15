using UnityEngine;
using System.Security.Cryptography;

namespace Game.Utilities
{
    /// <summary> 
    /// A class that provides functionality to print out a key upon start. 
    /// Only used to get a key once and replace the key in the ES_Encryption script with the newly printed one. 
    /// </summary>
    public class MS_GetEncryptionKey : MonoBehaviour
    {
        private void Start()
        {
            AesCryptoServiceProvider newAes = new AesCryptoServiceProvider();
            newAes.GenerateKey();
#if UNITY_EDITOR
            Debug.Log(MS_Encryption.GetString(newAes.Key));
#endif
        }
    }
}