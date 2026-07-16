using System;
using System.Collections;
using System.DirectoryServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LoipvRemote.Security;

namespace LoipvRemote.Tools
{
    [SupportedOSPlatform("windows")]
    public class AdHelper(string domain)
    {
        private DirectoryEntry _dEntry = null!;

        public Hashtable Children { get; } = [];

        private string Domain { get; } = domain;

        public void GetChildEntries(string adPath = "")
        {
            // Sanitize inputs to prevent LDAP injection
            string sanitizedDomain = string.IsNullOrEmpty(Domain) ? string.Empty : LdapPathSanitizer.SanitizeDistinguishedName(Domain);
            string sanitizedAdPath = string.IsNullOrEmpty(adPath) ? string.Empty : SanitizeLdapPath(adPath);

            _dEntry = sanitizedAdPath.Length <= 0
                ? sanitizedDomain.Length <= 0 ? new DirectoryEntry() : new DirectoryEntry("LDAP://" + sanitizedDomain)
                : new DirectoryEntry(sanitizedAdPath);
            try
            {
                foreach (DirectoryEntry child in _dEntry.Children)
                    Children.Add(child.Name, child.Path);
            }
            catch (COMException ex)
            {
                if (ex.Message.Equals("the server is not operational", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Could not find AD Server", ex);
            }
        }

        private static string SanitizeLdapPath(string ldapPath)
        {
            // Validate the LDAP path format
            if (!LdapPathSanitizer.IsValidDistinguishedNameFormat(ldapPath))
            {
                throw new ArgumentException("Invalid LDAP path format", nameof(ldapPath));
            }

            // For LDAP paths (URIs like LDAP://...), we need to sanitize the DN portion
            if (ldapPath.StartsWith("LDAP://", StringComparison.OrdinalIgnoreCase) ||
                ldapPath.StartsWith("LDAPS://", StringComparison.OrdinalIgnoreCase))
            {
                int schemeEndIndex = ldapPath.IndexOf("://", StringComparison.OrdinalIgnoreCase) + 3;
                if (schemeEndIndex < ldapPath.Length)
                {
                    // Find the server/domain part (before the first /)
                    int pathStartIndex = ldapPath.IndexOf('/', schemeEndIndex);
                    if (pathStartIndex > 0)
                    {
                        string scheme = ldapPath.Substring(0, schemeEndIndex);
                        string serverPart = ldapPath.Substring(schemeEndIndex, pathStartIndex - schemeEndIndex);
                        string dnPart = ldapPath.Substring(pathStartIndex + 1);

                        // Sanitize the DN part
                        string sanitizedDn = LdapPathSanitizer.SanitizeDistinguishedName(dnPart);
                        return scheme + serverPart + "/" + sanitizedDn;
                    }
                }
                // If no DN part found, return the path as-is (just the server)
                return ldapPath;
            }
            else
            {
                // For plain DN strings, sanitize directly
                return LdapPathSanitizer.SanitizeDistinguishedName(ldapPath);
            }
        }
    }
}
