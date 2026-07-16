using System;
using System.Collections.Generic;
using System.Security;
using System.Text.RegularExpressions;
using LoipvRemote.Resources.Language;


namespace LoipvRemote.Security.PasswordCreation
{
    public class PasswordIncludesSpecialCharactersConstraint : IPasswordConstraint
    {
        private readonly int _minimumCount;

        public IEnumerable<char> SpecialCharacters { get; } = new[] { '!', '@', '#', '$', '%', '^', '&', '*' };

        public string ConstraintHint { get; }

        public PasswordIncludesSpecialCharactersConstraint(int minimumCount = 1)
        {
            if (minimumCount < 0)
                throw new ArgumentException($"{nameof(minimumCount)} must be a non-negative value");

            _minimumCount = minimumCount;
            ConstraintHint = FormatText(Language.PasswordConstainsSpecialCharactersConstraintHint, _minimumCount,
                                           string.Concat(SpecialCharacters));
        }

        public PasswordIncludesSpecialCharactersConstraint(IEnumerable<char> specialCharacters, int minimumCount = 1)
            : this(minimumCount)
        {
            ArgumentNullException.ThrowIfNull(specialCharacters);

            SpecialCharacters = specialCharacters;
            ConstraintHint = FormatText(Language.PasswordConstainsSpecialCharactersConstraintHint, _minimumCount,
                                           string.Concat(SpecialCharacters));
        }

        public bool Validate(SecureString password)
        {
            Regex regex = new($"[{string.Concat(SpecialCharacters)}]");
            return regex.Count(password.ConvertToUnsecureString()) >= _minimumCount;
        }
    }
}
