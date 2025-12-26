using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Management;

namespace DMM_Hide_Launcher.Others
{
    /// <summary>
    /// 加密助手类
    /// 提供AES加密解密功能，以及设备特定的密钥生成
    /// </summary>
    public static class AESKEY
    {
        private const string ApplicationId = "DMM_Hide_Launcher";

        /// <summary>
        /// 获取设备标识符
        /// </summary>
        /// <returns>设备唯一标识符</returns>
        public static string GetDeviceIdentifier()
        {
            try
            {
                // 获取机器序列号作为设备唯一标识
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard"))
                {
                    foreach (var managementObject in searcher.Get())
                    {
                        return managementObject["SerialNumber"]?.ToString() ?? "DefaultDeviceId";
                    }
                }
                return "DefaultDeviceId";
            }
            catch (Exception)
            {
                // 如果无法获取设备标识符，返回默认值
                return "DefaultDeviceId";
            }
        }

        /// <summary>
        /// 生成设备特定的加密密钥
        /// </summary>
        /// <returns>设备特定的AES密钥</returns>
        public static byte[] GenerateDeviceSpecificKey()
        {
            try
            {
                string deviceId = GetDeviceIdentifier();
                string combinedKey = ApplicationId + deviceId;

                // 使用SHA256哈希算法生成256位密钥
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedKey));
                    // 截取前16字节作为AES-128密钥
                    byte[] key = new byte[16];
                    Array.Copy(hashBytes, key, 16);
                    return key;
                }
            }
            catch (Exception)
            {
                // 发生异常时使用默认密钥
                return new byte[] { 0x2B, 0x7E, 0x15, 0x16, 0x28, 0xAE, 0xD2, 0xA6, 0xAB, 0xF7, 0x15, 0x88, 0x09, 0xCF, 0x4F, 0x3C };
            }
        }

        /// <summary>
        /// 生成设备特定的初始化向量
        /// </summary>
        /// <returns>设备特定的AES初始化向量</returns>
        public static byte[] GenerateDeviceSpecificIV()
        {
            try
            {
                string deviceId = GetDeviceIdentifier();
                string combinedIV = ApplicationId + "_IV_" + deviceId;

                // 使用SHA256哈希算法生成IV
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedIV));
                    // 截取前16字节作为AES-128 IV
                    byte[] iv = new byte[16];
                    Array.Copy(hashBytes, iv, 16);
                    return iv;
                }
            }
            catch (Exception)
            {
                // 发生异常时使用默认IV
                return new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F };
            }
        }

        /// <summary>
        /// 使用AES算法加密字符串
        /// </summary>
        /// <param name="plainText">要加密的明文</param>
        /// <returns>加密后的字符串</returns>
        public static string EncryptString(string plainText)
        {
            try
            {
                byte[] key = GenerateDeviceSpecificKey();
                byte[] iv = GenerateDeviceSpecificIV();

                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;

                    ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                        {
                            using (StreamWriter streamWriter = new StreamWriter(cryptoStream))
                            {
                                streamWriter.Write(plainText);
                            }
                        }

                        return Convert.ToBase64String(memoryStream.ToArray());
                    }
                }
            }
            catch (Exception)
            {
                // 发生异常时返回空字符串
                return string.Empty;
            }
        }

        /// <summary>
        /// 使用AES算法解密字符串
        /// </summary>
        /// <param name="cipherText">要解密的密文</param>
        /// <returns>解密后的明文</returns>
        public static string DecryptString(string cipherText)
        {
            try
            {
                byte[] key = GenerateDeviceSpecificKey();
                byte[] iv = GenerateDeviceSpecificIV();

                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;

                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (MemoryStream memoryStream = new MemoryStream(Convert.FromBase64String(cipherText)))
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader streamReader = new StreamReader(cryptoStream))
                            {
                                return streamReader.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 发生异常时返回空字符串
                return string.Empty;
            }
        }
    }
}