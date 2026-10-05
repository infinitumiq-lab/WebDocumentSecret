using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VaultEncryptionPOC.Services
{
    public class EncryptionService
    {
        private readonly byte[] _key;

        public EncryptionService()
        {
            _key = new byte[32];
            RandomNumberGenerator.Fill(_key);
        }

        public string Encrypt<T>(T data)
        {
            string  json = JsonSerializer.Serialize(data);

            byte[] plaintext = Encoding.UTF32.GetBytes(json);

            byte[] nonce = new byte[12];
            RandomNumberGenerator.Fill(nonce);

            //var nonce = byte[]
            //    {
            //    m,a,r,y,j,a,n,e,1,2,3,3
            //}

            byte[] ciphertext = new byte[plaintext.Length];

            byte[] tag = new byte[16];


            using (AesGcm aes = new AesGcm(_key, 16))
            {
                aes.Encrypt(
                    nonce,
                    plaintext,
                    ciphertext,
                    tag
                );
            }

            var payload = new EncryptedPayload
            {
                    Nonce = Convert.ToBase64String(nonce),
                    Ciphertext = Convert.ToBase64String(ciphertext),
                    Tag = Convert.ToBase64String(tag)
            };

            return JsonSerializer.Serialize(payload);
        }


        public T Decrypt<T>(string encryptedPayload)
        {

            EncryptedPayload? payload = JsonSerializer.Deserialize<EncryptedPayload>(encryptedPayload);

            if (payload == null)
            {
                throw new InvalidOperationException(
                    "Invalid encrypted payload."
                    );
            }

            byte[] nonce = Convert.FromBase64String(payload.Nonce);

            byte[] ciphertext = Convert.FromBase64String(payload.Ciphertext);
            byte[] tag = Convert.FromBase64String(payload.Tag);
            
            byte[] plaintext = new byte[ciphertext.Length];

            using (AesGcm aes = new AesGcm(_key, 16))
            {
                aes.Decrypt(
                    nonce,
                    ciphertext,
                    tag,
                    plaintext
                );
            }

            string json = Encoding.UTF32.GetString(plaintext);

            T? result = JsonSerializer.Deserialize<T>(json); 
            if (result ==null)
            {
                throw new InvalidOperationException("" +
                    "Unable to decrypt the data"
                    );
            }
            return result;
        }

    }


    public class EncryptedPayload
    {
        public string Nonce { get; set; } = string.Empty;
        public string Ciphertext { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
    }
}
