using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Runtime.Versioning;


namespace LoipvRemote.Connection
{
    [SupportedOSPlatform("windows")]
    public class DefaultConnectionInfo : ConnectionInfo
    {
        [Browsable(false)]
        public static DefaultConnectionInfo Instance { get; } = new DefaultConnectionInfo();

        private DefaultConnectionInfo()
        {
            IsDefault = true;
            Inheritance = DefaultConnectionInheritance.Instance;
        }

        public void LoadFrom<TSource>(TSource sourceInstance, Func<string, string>? propertyNameMutator = null)
        {
            if (propertyNameMutator == null)
                propertyNameMutator = a => a;

            System.Collections.Generic.IEnumerable<System.Reflection.PropertyInfo> connectionProperties = GetSerializableProperties();
            foreach (System.Reflection.PropertyInfo property in connectionProperties)
            {
                try
                {
                    string expectedPropertyName = propertyNameMutator(property.Name);
                    System.Reflection.PropertyInfo? propertyFromSource = typeof(TSource).GetProperty(expectedPropertyName);
                    if (propertyFromSource == null)
                        throw new SettingsPropertyNotFoundException($"No property with name '{expectedPropertyName}' found.");

                    object? valueFromSource = propertyFromSource.GetValue(sourceInstance, null);
                    if (valueFromSource is null)
                    {
                        property.SetValue(Instance, null, null);
                        continue;
                    }

                    if (property.PropertyType.IsEnum)
                    {
                        property.SetValue(Instance, Enum.Parse(property.PropertyType, valueFromSource.ToString()!), null);
                        continue;
                    }

                    property.SetValue(Instance, Convert.ChangeType(valueFromSource, property.PropertyType), null);
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"Error loading default connection info property {property.Name}.{Environment.NewLine}{ex}");
                }
            }
        }

        public void SaveTo<TDestination>(TDestination destinationInstance, Func<string, string>? propertyNameMutator = null)
        {
            if (propertyNameMutator == null)
                propertyNameMutator = (a) => a;

            System.Collections.Generic.IEnumerable<System.Reflection.PropertyInfo> connectionProperties = GetSerializableProperties();

            foreach (System.Reflection.PropertyInfo property in connectionProperties)
            {
                try
                {
                    string expectedPropertyName = propertyNameMutator(property.Name);
                    System.Reflection.PropertyInfo? propertyFromDestination = typeof(TDestination).GetProperty(expectedPropertyName);

                    if (propertyFromDestination == null)
                        throw new SettingsPropertyNotFoundException($"No property with name '{expectedPropertyName}' found.");

                    // ensure value is of correct type
                    object? sourceValue = property.GetValue(Instance, null);
                    object? value = sourceValue is null
                        ? null
                        : Convert.ChangeType(sourceValue, propertyFromDestination.PropertyType);

                    propertyFromDestination.SetValue(destinationInstance, value, null);
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"Error saving default connection info property {property.Name}.{Environment.NewLine}{ex}");
                }
            }
        }
    }
}
