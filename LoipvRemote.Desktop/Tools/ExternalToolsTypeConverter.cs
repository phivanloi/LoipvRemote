using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using System;

namespace LoipvRemote.Tools
{
    [SupportedOSPlatform("windows")]
    public class ExternalToolsTypeConverter : StringConverter
    {
        private static Func<IEnumerable<ExternalTool>> s_source = static () => [];

        public static void Configure(Func<IEnumerable<ExternalTool>> source)
        {
            s_source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public static string[] ExternalTools
        {
            get
            {
                List<string> externalToolList = new()
                {
                    // Add a blank entry to signify that no external tool is selected
                    string.Empty
                };

                foreach (ExternalTool externalTool in s_source())
                {
                    externalToolList.Add(externalTool.DisplayName);
                }

                return externalToolList.ToArray();
            }
        }

        public override StandardValuesCollection GetStandardValues([NotNull] ITypeDescriptorContext? context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return new StandardValuesCollection(ExternalTools);
        }

        public override bool GetStandardValuesExclusive([NotNull] ITypeDescriptorContext? context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return true;
        }

        public override bool GetStandardValuesSupported([NotNull] ITypeDescriptorContext? context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return true;
        }
    }
}
