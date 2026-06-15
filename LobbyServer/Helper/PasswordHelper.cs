using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Security.Cryptography;

namespace LobbyServer.Helper
{
    public interface IPasswordHelper
    {
        (string passwordHash, string salt) HashPassword(string password);
        bool VerifyPassword(string password, string passwordHash, string salt);
    }

    public class PasswordHelper : IPasswordHelper
    {
        private const int Iterations = 10000;
        private const int SaltSize = 16; // 128 bits
        private const int KeySize = 32; // 256 bits

        public (string passwordHash, string salt) HashPassword(string password)
        {
            // 랜덤 솔트 생성
            byte[] saltBytes = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }

            // PBKDF2를 사용한 비밀번호 해싱
            byte[] hashBytes = KeyDerivation.Pbkdf2(
                password,
                saltBytes,
                KeyDerivationPrf.HMACSHA256,
                Iterations,
                KeySize);

            // 바이트 배열을 Base64 문자열로 변환
            string passwordHash = Convert.ToBase64String(hashBytes);
            string salt = Convert.ToBase64String(saltBytes);

            return (passwordHash, salt);
        }

        public bool VerifyPassword(string password, string passwordHash, string salt)
        {
            byte[] saltBytes = Convert.FromBase64String(salt);

            // 입력받은 비밀번호 해싱
            byte[] hashBytes = KeyDerivation.Pbkdf2(
                password,
                saltBytes,
                KeyDerivationPrf.HMACSHA256,
                Iterations,
                KeySize);

            string computedHash = Convert.ToBase64String(hashBytes);

            // 저장된 해시와 계산된 해시 비교
            return passwordHash == computedHash;
        }
    }
}
