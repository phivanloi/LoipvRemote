using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using LoipvRemote.App.Info;


namespace LoipvRemote.Connection
{
    [SupportedOSPlatform("windows")]
    public class ConnectionIcon : StringConverter
    {
        public const string LoipvRemoteIconName = "LoipvRemote";
        public static string[] Icons { get; private set; } = Array.Empty<string>();

        internal static void AddIcon(string iconName)
        {
            if (string.IsNullOrWhiteSpace(iconName))
                return;

            Icons = [.. Icons, iconName];
        }

        public static string GetConnectionDisplayIcon(string? iconName)
        {
            return LoipvRemoteIconName;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
        {
            return new StandardValuesCollection(Icons);
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context)
        {
            return true;
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context)
        {
            return true;
        }

        public static System.Drawing.Icon? FromString(string iconName)
        {
            try
            {
                string iconPath = $"{GeneralAppInfo.HomePath}\\Icons\\{iconName}.ico";

                if (System.IO.File.Exists(iconPath))
                {
                    System.Drawing.Icon nI = new(iconPath);
                    return nI;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Couldn't get icon from string.{Environment.NewLine}{ex}");
            }

            return null;
        }
    }
}
