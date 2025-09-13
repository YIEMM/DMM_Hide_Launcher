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
    public static class CryptoHelper
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
            catch (Exception ex)
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
        /// <returns>加密后的Base64字符串</returns>
        public static string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = GenerateDeviceSpecificKey();
                    aesAlg.IV = GenerateDeviceSpecificIV();

                    ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                    using (MemoryStream msEncrypt = new MemoryStream())
                    {
                        using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                            {
                                swEncrypt.Write(plainText);
                            }
                            return Convert.ToBase64String(msEncrypt.ToArray());
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 如果加密失败，返回原字符串（不推荐，但为了保持兼容性）
                return plainText;
            }
        }

        /// <summary>
        /// 使用AES算法解密字符串
        /// </summary>
        /// <param name="cipherText">要解密的Base64编码的密文</param>
        /// <returns>解密后的明文</returns>
        public static string DecryptString(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return string.Empty;

            try
            {
                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = GenerateDeviceSpecificKey();
                    aesAlg.IV = GenerateDeviceSpecificIV();

                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                    using (MemoryStream msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText)))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                            {
                                return srDecrypt.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 如果解密失败，返回原字符串（可能是未加密的旧数据）
                return cipherText;
            }
        }
    }
}