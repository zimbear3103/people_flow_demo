using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ReadOnlyAttribute : PropertyAttribute
{
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;
        EditorGUI.PropertyField(position, property, label, true);
        GUI.enabled = true;
    }
}
#endif

public static class Utils
{
    private const float BASE_WIDTH = 540;
    private const float BASE_HEIGHT = 960;

    public static void OnUpdateQuestText(TMPro.TextMeshProUGUI text, int iconHeight = 130, int adjustLineSpacing = 73, float fontDisplayPer = 0.6f)
    {
        text.lineSpacing = -adjustLineSpacing;
        int countLine = 1;

        if (text.text.Contains("\n"))
            countLine = System.Text.RegularExpressions.Regex.Matches(text.text, "\n").Count + 1;
        else if (text.text.Contains("<br>"))
            countLine = System.Text.RegularExpressions.Regex.Matches(text.text, "<br>").Count + 1;
       
        RectTransform parentRect = text.transform.parent.GetComponent<RectTransform>();
        float height = text.fontSize * (fontDisplayPer * countLine);

        parentRect.sizeDelta = new Vector2(parentRect.sizeDelta.x, height < iconHeight ? iconHeight : height);
    }

    public static float GetBaseScale(float w, float h)
    {
        float scaleH = h / BASE_HEIGHT;
        float scaleW = w / BASE_WIDTH;
        float scale = scaleH < scaleW ? scaleH : scaleW;

        return (float)Math.Round(scale, 3);
    }
    public static Sprite GetSliceSprite(Texture2D texture, Vector2 pivot, Vector2 position, Vector2 size)
    {
        return Sprite.Create(texture, new Rect(position, size), pivot);
    }

    public static async Task<Texture2D> DownloadTextureAsync(string downloadLink, Texture2D defaultText = null)        {

        var downloadRequest = UnityWebRequestTexture.GetTexture(downloadLink);

        var tcs = new TaskCompletionSource<Texture2D>();

        async void RequestCompleted(AsyncOperation asyncOperation)
        {
            await Task.Yield(); // Allow the async operation to complete before continuing

            switch (downloadRequest.result)
            {
                case UnityWebRequest.Result.Success:
                    var downloadedTexture = ((DownloadHandlerTexture)downloadRequest.downloadHandler).texture;
                    tcs.SetResult(downloadedTexture);
                    break;
                default:
                    tcs.SetResult(defaultText);
                    break;
            }
        }

        var asyncOperation = downloadRequest.SendWebRequest();
        asyncOperation.completed += RequestCompleted;

        await tcs.Task; // Wait for the task completion source to be set

        return tcs.Task.Result;
    }
    public static IEnumerator DownloadTextureCoroutine(string url, Action<Texture2D> onComplete)
    {
        var requestTexture = UnityWebRequestTexture.GetTexture(url);

        yield return requestTexture.SendWebRequest();

        switch (requestTexture.result)
        {
            case UnityWebRequest.Result.Success:
                var texture = ((DownloadHandlerTexture)requestTexture.downloadHandler).texture;
                onComplete?.Invoke(texture);
                break;
            default:
                onComplete?.Invoke(null);
                break;
        }
    }

    public static Sprite LoadSprite(string name, float pivotX = 0.5f, float pivotY = 0.5f)
    {
        Sprite sprite = null;
        Texture2D texture = Resources.Load<Texture2D>(name);

        if (texture != null)
        {               
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(pivotX, pivotY));
        }
        else
        {
            Debug.LogError("File not found: " + name);
        }

        return sprite;
    }
    public static string FormatLevel(int level)
    {
        return (level + 1).ToString("D4");
    }

    public static string GetBlockTime(float timeInSeconds, int blockSizeInSeconds)
    {
        int block = Mathf.FloorToInt(timeInSeconds / blockSizeInSeconds) * blockSizeInSeconds;
        return block.ToString("D4");
    }
    public static IEnumerator CheckInternetConnection(Action<bool> callback)
    {
        using (UnityWebRequest request = UnityWebRequest.Get("https://www.google.com"))
        {
            request.timeout = 5; // Set a timeout to avoid long waits
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
                callback(true);
            else
                callback(false);
        }
    }
    #region Hash
    private static string SecretKey = "";
    private static string KeyIv = "";
    private const string KeyA = "ZXkgdmF5ISBEYXkgbGEgc2FuIHBoYW0g";
    private const string KeyB = "YWkgZGFuZyByaW5oIG1vIHRpbSBr";
    private const string KeyC = "Qgcm9pLCB2dWkgbG9uZyBkdW5nIHBoYSBudWEh";
    private const string KeyD = "Y3VhIFZlciwgbW90IHRlYW0gc2FwIGNoZX";

    public static void GenerateHash2(string key, string value)
    {
        string data = value + KeyB + KeyA + KeyD + KeyC;
        int a1 = 16;
        int a2 = (a1 % 10) * 10 + a1 / 10;
        int a3 = a1 + 2;
        int a4 = a1 + a2;
        int a5 = a4 - (a1 / 10 + a1 % 10);

        //Debug.Log("data:" + data);

        int[] array = new int[] { a1, a5, 71, 72, 84, 42, 112, 54, 21, 47, a4, 94, a2, 99, 71, a3 };
        byte[] bytes = Convert.FromBase64String(data);

        data = System.Text.Encoding.UTF8.GetString(bytes);
        //Debug.Log("data:" + data);

        int count = 0;
        int max = 6;
        KeyIv = SecretKey = "";

        foreach (int item in array)
        {
            string str = data[item].ToString();
            if (count == max || count == max * 2)
                str = str.ToUpper();

            KeyIv += item.ToString()[0];
            SecretKey += str;
            count++;
        }

        //Debug.Log("KeyIv:" + KeyIv);
        //Debug.Log("SecretKey:" + SecretKey);
        //Debug.Log("Encrypt:" + Encrypt(""));
        //Debug.Log("Decrypt:" + Decrypt(""));      
    }

    public static string GenerateHash(string key, string value)
    {
        string SecrectKey = Decrypt(key);
        string hashMd5 = GenerateMD5Hash(SecrectKey + value);

        return hashMd5;
    }  
    
    public static string GenerateTrackingEventHash(string key, string value)
    {           
        string hashMd5 = GenerateMD5Hash(key + "hg8v" + value);

        return hashMd5;
    }
    public static string Encrypt(string plainText)
    {
        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = Encoding.UTF8.GetBytes(SecretKey);
            aesAlg.IV = Encoding.UTF8.GetBytes(KeyIv);

            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

            byte[] encrypted;

            using (var memoryStream = new System.IO.MemoryStream())
            {
                using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                {
                    using (var streamWriter = new System.IO.StreamWriter(cryptoStream))
                    {
                        streamWriter.Write(plainText);
                    }
                    encrypted = memoryStream.ToArray();
                }
            }

            return Convert.ToBase64String(encrypted);
        }
    }

    public static string Decrypt(string cipherText)
    {
        byte[] cipherBytes = Convert.FromBase64String(cipherText);

        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = Encoding.UTF8.GetBytes(SecretKey);
            aesAlg.IV = Encoding.UTF8.GetBytes(KeyIv);

            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

            using (var memoryStream = new System.IO.MemoryStream(cipherBytes))
            {
                using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                {
                    using (var streamReader = new System.IO.StreamReader(cryptoStream))
                    {
                        return streamReader.ReadToEnd();
                    }
                }
            }
        }
    }
    public static string OnDecryptGameConfig(string cipherText)
    {
        byte[] cipherBytes = Convert.FromBase64String(cipherText);

        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = Encoding.UTF8.GetBytes("azbx1b1azbx1b1y2r3i5o4p7s9c9y2r3");
            aesAlg.IV = Encoding.UTF8.GetBytes("i5o4p7s9c9y2r3i5");

            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

            using (var memoryStream = new System.IO.MemoryStream(cipherBytes))
            {
                using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                {
                    using (var streamReader = new System.IO.StreamReader(cryptoStream))
                    {
                        return streamReader.ReadToEnd();
                    }
                }
            }
        }
    }

    public static string GenerateMD5Hash(string input)
    {
        using (MD5 md5 = MD5.Create())
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < hashBytes.Length; i++)
            {
                builder.Append(hashBytes[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }
    #endregion //Hash

    #region Time
    public const string STR_FORMAT_DATE_TIME = "dd/MM/yyyy HH:mm:ss";
    public static DateTime GetDateTime(string date)
    {
        DateTime startdate = DateTime.ParseExact(date, STR_FORMAT_DATE_TIME, CultureInfo.InvariantCulture);
        return startdate;
    }  
    
    public static long GetMilisecondsOffsetTime(long startGameTime) //return mili seconds
    {
        var epoch = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        var dtime = epoch - startGameTime;

        //GameLog.Log(LogType.Warning, $"[Utils] GetOffsetTime() dtime = {dtime}s");
        return dtime;
    }  
    
    public static long GetSecondsOffsetTime(long startGameTime) //return seconds
    {
        var epoch = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        var dtime = epoch - startGameTime;

        //GameLog.Log(LogType.Warning, $"[Utils] GetOffsetTime() dtime = {dtime}s");
        return dtime;
    }
    public static DateTime ConvertUTC7DateTime(DateTime dateTime)
    {     
        DateTime utc7 = dateTime.AddHours(7);         

        return utc7;
    }

    public static bool IsCooldownExpired(TimeSpan cooldownTime, DateTime lastDataTime)
    {
        bool isCooldownExpired = false;   
        TimeSpan elapsedTime = DateTime.Now - lastDataTime;
        isCooldownExpired = elapsedTime >= cooldownTime;

        return isCooldownExpired;
    }

    #endregion Time
}
