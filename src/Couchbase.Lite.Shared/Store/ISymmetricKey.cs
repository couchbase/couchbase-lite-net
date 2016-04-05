using System.IO;

namespace Couchbase.Lite.Store
{
    public interface ISymmetricKey
    {
        /// <summary>
        /// The SymmetricKey's key data; can be used to reconstitute it.
        /// </summary>
        byte[] KeyData { get; }

        /// <summary>
        /// The key data encoded as hex.
        /// </summary>
        string HexData { get; }

        /// <summary>
        /// Encrypts a data blob.
        /// The output consists of a 16-byte random initialization vector,
        /// followed by PKCS7-padded ciphertext. 
        /// </summary>
        byte[] EncryptData(byte[] data);

        /// <summary>
        /// Decrypts data encoded by encryptData.
        /// </summary>
        byte[] DecryptData(byte[] encryptedData);

        /// <summary>
        /// Streaming decryption.
        /// </summary>
        Stream DecryptStream(Stream stream);

        /// <summary>
        /// Creates a stream that will encrypt the given base stream
        /// </summary>
        /// <returns>The stream to write to for encryption</returns>
        /// <param name="baseStream">The stream to read</param>
        Stream CreateStream(Stream baseStream);
    }
}
