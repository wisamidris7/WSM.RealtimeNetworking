using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace WSM.ClientRealtime.Scripts;
public static class Tools
{

    public static string GenerateToken()
    {
        return Path.GetRandomFileName().Remove(8, 1);
    }

    public static T CloneClass<T>(this T target)
    {
        return target.Serialize().Desrialize<T>();
    }

    public static void CopyTo(Stream source, Stream target)
    {
        byte[] bytes = new byte[4096]; int count;
        while ((count = source.Read(bytes, 0, bytes.Length)) != 0)
        {
            target.Write(bytes, 0, count);
        }
    }

    #region Encryption
    public static string EncrypteToMD5(string data)
    {
        UTF8Encoding ue = new();
        byte[] bytes = ue.GetBytes(data);
        MD5CryptoServiceProvider md5 = new();
        byte[] hashBytes = md5.ComputeHash(bytes);
        string hashString = "";
        for (int i = 0; i < hashBytes.Length; i++)
        {
            hashString += Convert.ToString(hashBytes[i], 16).PadLeft(2, '0');
        }
        return hashString.PadLeft(32, '0');
    }
    #endregion

    #region Serialization
    public static string Serialize<T>(this T target)
    {
        XmlSerializer xml = new(typeof(T));
        StringWriter writer = new();
        xml.Serialize(writer, target);
        return writer.ToString();
    }

    public static T Desrialize<T>(this string target)
    {
        XmlSerializer xml = new(typeof(T));
        StringReader reader = new(target);
        return (T)xml.Deserialize(reader);
    }

    public static async Task<string> SerializeAsync<T>(this T target)
    {
        Task<string> task = Task.Run(() =>
        {
            XmlSerializer xml = new(typeof(T));
            StringWriter writer = new();
            xml.Serialize(writer, target);
            return writer.ToString();
        });
        return await task;
    }

    public static async Task<T> DesrializeAsync<T>(this string target)
    {
        Task<T> task = Task.Run(() =>
        {
            XmlSerializer xml = new(typeof(T));
            StringReader reader = new(target);
            return (T)xml.Deserialize(reader);
        });
        return await task;
    }
    #endregion

    #region Compression
    public static async Task<byte[]> CompressAsync(string target)
    {
        Task<byte[]> task = Task.Run(() =>
        {
            return Compress(target);
        });
        return await task;
    }

    public static byte[] Compress(string target)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(target);
        using MemoryStream msi = new(bytes);
        using MemoryStream mso = new();
        using (GZipStream gs = new(mso, CompressionMode.Compress))
        {
            CopyTo(msi, gs);
        }
        return mso.ToArray();
    }

    public static async Task<string> DecompressAsync(byte[] bytes)
    {
        Task<string> task = Task.Run(() =>
        {
            return Decompress(bytes);
        });
        return await task;
    }

    public static string Decompress(byte[] bytes)
    {
        using MemoryStream msi = new(bytes);
        using MemoryStream mso = new();
        using (GZipStream gs = new(msi, CompressionMode.Decompress))
        {
            CopyTo(gs, mso);
        }
        return Encoding.UTF8.GetString(mso.ToArray());
    }
    #endregion

}