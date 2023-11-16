using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace WSM.ServerRealtime.Scripts;

internal static class Tools
{

    public static readonly string logFolderPath = "C:\\Log\\Realtime Networking\\";

    public static void LogError(string message, string trace, string folder = "")
    {
        Console.WriteLine("Error:" + "\n" + message + "\n" + trace);
        Task task = Task.Run(() =>
        {
            try
            {
                string folderPath = logFolderPath;
                if (!string.IsNullOrEmpty(folder))
                {
                    folderPath = folderPath + folder + "\\";
                }
                string path = folderPath + DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss-ffff") + ".txt";
                if (!Directory.Exists(folderPath))
                {
                    _ = Directory.CreateDirectory(folderPath);
                }
                File.WriteAllText(path, message + "\n" + trace);
            } catch (Exception ex)
            {
                Console.WriteLine("Error:" + "\n" + ex.Message + "\n" + ex.StackTrace);
            }
        });
    }

    public static string GenerateToken()
    {
        return Path.GetRandomFileName().Remove(8, 1);
    }

    public static string GetIP(AddressFamily type)
    {
        IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (IPAddress ip in host.AddressList)
        {
            if (ip.AddressFamily == type)
            {
                return ip.ToString();
            }
        }
        return "0.0.0.0";
    }

    public static string GetExternalIP()
    {
        try
        {
            IPAddress ip = IPAddress.Parse(new WebClient().DownloadString("https://icanhazip.com").Replace("\\r\\n", "").Replace("\\n", "").Trim());
            return ip.ToString();
        } catch (Exception)
        {
            try
            {
                StreamReader sr = new(WebRequest.Create("https://checkip.dyndns.org").GetResponse().GetResponseStream());
                string[] ipAddress = sr.ReadToEnd().Trim().Split(':')[1][1..].Split('<');
                return ipAddress[0];
            } catch (Exception)
            {
                return "0.0.0.0";
            }
        }
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