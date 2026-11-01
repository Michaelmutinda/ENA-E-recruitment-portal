using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Web;

namespace Afidep_E_Recruitment.Models
{
    public static class HelperFunctions
    {
        public static string MaskPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return "Phone Number unavailable";
            }

            string digitsOnly = new string(phoneNumber.Where(char.IsDigit).ToArray());

            if (digitsOnly.Length < 4)
            {
                return "Phone Number unavailable";
            }

            string lastNineDigits = digitsOnly.Substring(digitsOnly.Length - 9);

            string firstThree = lastNineDigits.Substring(0, 3);
            string lastTwo = lastNineDigits.Substring(7, 2);

            return $"+254{firstThree}****{lastTwo}";
        }

        public static string MaskIDNumber(string idNumber)
        {
            if (string.IsNullOrWhiteSpace(idNumber))
            {
                return "ID Number unavailable";
            }
            string digitsOnly = new string(idNumber.ToArray());

            const int digitsToShowAtStart = 3;
            const int digitsToShowAtEnd = 2;
            const int minLengthForMasking = digitsToShowAtStart + digitsToShowAtEnd + 1;

            if (digitsOnly.Length < minLengthForMasking)
            {
                return "ID Number unavailable";
            }

            string firstPart = digitsOnly.Substring(0, digitsToShowAtStart);
            string lastPart = digitsOnly.Substring(digitsOnly.Length - digitsToShowAtEnd);

            int numberOfMaskedDigits = digitsOnly.Length - digitsToShowAtStart - digitsToShowAtEnd;
            string maskedPart = new string('*', numberOfMaskedDigits);

            return $"{firstPart}{maskedPart}{lastPart}";
        }

        public static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return "Email unavailable";
            }
            int atIndex = email.IndexOf('@');
            if (atIndex <= 0)
            {
                return "Email unavailable";
            }

            string username = email.Substring(0, atIndex);
            string domain = email.Substring(atIndex + 1);

            string maskedUsername;
            if (username.Length <= 3)
            {
                maskedUsername = username.Substring(0, 1) + new string('*', username.Length - 1);
            }
            else
            {
                string usernamePrefix = username.Substring(0, 3);
                string usernameSuffix = username.Substring(username.Length - 2);
                string maskedMiddle = new string('*', username.Length - 5);
                maskedUsername = $"{usernamePrefix}{maskedMiddle}{usernameSuffix}";
            }

            string maskedDomain;
            int dotIndex = domain.IndexOf('.');
            if (dotIndex <= 0)
            {
                return "Email unavailable";
            }

            string domainPrefix = domain.Substring(0, dotIndex);
            string domainSuffix = domain.Substring(dotIndex);

            if (domainPrefix.Length <= 3)
            {
                maskedDomain = new string('*', domainPrefix.Length) + domainSuffix;
            }
            else
            {
                string maskedDomainPrefix = new string('*', domainPrefix.Length - 3);
                string unmaskedDomainSuffix = domainPrefix.Substring(domainPrefix.Length - 3);
                maskedDomain = maskedDomainPrefix + unmaskedDomainSuffix + domainSuffix;
            }
            return $"{maskedUsername}@{maskedDomain}";
        }

        public static string ToOrdinalDateString(this DateTime date)
        {
            int day = date.Day;
            string suffix = GetDaySuffix(day);
            return $"{day}{suffix} {date.ToString("MMMM yyyy")}";
        }

        private static string GetDaySuffix(int day)
        {
            if (day >= 11 && day <= 13)
            {
                return "th";
            }
            switch (day % 10)
            {
                case 1: return "st";
                case 2: return "nd";
                case 3: return "rd";
                default: return "th";
            }
        }
        public static string ToCurrencyString(this decimal amount)
        {
            return amount.ToString("N2");
        }


    }


    public static class PasswordHashers
    {
        private const int SaltSize = 128; // 128 bits
        private const int HashSize = 160; // 160 bits (for SHA1, adjust for SHA256/512)
        private const int Iterations = 10000;

        public static string GenerateSalt()
        {
            byte[] salt;
            new RNGCryptoServiceProvider().GetBytes(salt = new byte[SaltSize]);
            return Convert.ToBase64String(salt);
        }

        public static string HashPassword(string password, string salt)
        {
            byte[] saltBytes = Convert.FromBase64String(salt);
            var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, Iterations);
            byte[] hash = pbkdf2.GetBytes(HashSize);

            byte[] hashBytes = new byte[SaltSize + HashSize];
            Array.Copy(saltBytes, 0, hashBytes, 0, SaltSize);
            Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

            return Convert.ToBase64String(hashBytes);
        }

        public static bool VerifyPassword(string password, string storedHash)
        {
            byte[] hashBytes = Convert.FromBase64String(storedHash);
            byte[] salt = new byte[SaltSize];
            Array.Copy(hashBytes, 0, salt, 0, SaltSize);

            var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations);
            byte[] hash = pbkdf2.GetBytes(HashSize);

            for (int i = 0; i < HashSize; i++)
            {
                if (hashBytes[i + SaltSize] != hash[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}