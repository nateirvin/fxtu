using System;
using System.Text.RegularExpressions;

namespace XmlToTable.Core
{
    public class NameHandler
    {
        public string GetValidName(string rawName, int maxLength, TooLongNameBehavior behavior)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return rawName;
            }

            if (maxLength < 1)
            {
                throw new ArgumentException("A name cannot contain fewer than 1 character.", "maxLength");
            }

            string trimmedName = rawName.Trim();
            if (trimmedName.Length <= maxLength)
            {
                return trimmedName;
            }

            if (behavior == TooLongNameBehavior.Throw)
            {
                throw new InvalidNameException("The value is too long.", rawName);
            }
            if (behavior == TooLongNameBehavior.Truncate)
            {
                return trimmedName.Substring(0, maxLength);
            }

            char? delimiter = null;
            if (Regex.IsMatch(trimmedName, @"[a-z0-9]+?\-[a-z0-9]+?", RegexOptions.IgnoreCase))
            {
                delimiter = '-';
            }

            string finalName = trimmedName;

            if (delimiter.HasValue)
            {
                char trueDelimiter = delimiter.Value;
                string[] parts = trimmedName.Split(trueDelimiter);

                int i = 0;
                while (finalName.Length > maxLength && i < parts.Length)
                {
                    parts[i] = Abbreviate(parts[i]);
                    finalName = string.Join(trueDelimiter.ToString(), parts);
                    i++;
                }

                if (finalName.Length > maxLength)
                {
                    finalName = finalName.Replace(trueDelimiter.ToString(), String.Empty);
                }
            }
            else
            {
                finalName = Abbreviate(trimmedName);
            }
            
            if (finalName.Length > maxLength)
            {
                throw new InvalidNameException("The value is too long.", rawName);
            }

            return finalName;
        }

        private string Abbreviate(string value)
        {
            string header = value[0].ToString();
            string target = value;
            if ("aeiou".Contains(header.ToLower()))
            {
                target = value.Substring(1, value.Length - 1);
            }
            else
            {
                header = String.Empty;
            }

            string abbreviatedChunk = Regex.Replace(target, "[aeiou]", String.Empty, RegexOptions.IgnoreCase);

            return header + abbreviatedChunk;
        }
    }
}