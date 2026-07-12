using System;
using System.Collections.Generic;
using System.Globalization;

namespace LoipvRemote.Connection.Monitoring
{
    public static class LinuxResourceSampleParser
    {
        private static readonly string[] RequiredKeys =
        [
            "cpu_total", "cpu_idle", "mem_total", "mem_available", "disk_total",
            "disk_used", "net_rx", "net_tx", "uptime_seconds"
        ];

        public static LinuxResourceSample Parse(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                throw new FormatException("The resource probe returned no data.");

            Dictionary<string, long> values = new(StringComparer.Ordinal);
            foreach (string line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int separator = line.IndexOf('=');
                if (separator <= 0 || separator == line.Length - 1)
                    continue;

                string key = line[..separator];
                string value = line[(separator + 1)..];
                if (Array.IndexOf(RequiredKeys, key) < 0)
                    continue;
                if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) || parsed < 0)
                    throw new FormatException($"The resource probe returned an invalid value for {key}.");

                values[key] = parsed;
            }

            foreach (string key in RequiredKeys)
            {
                if (!values.ContainsKey(key))
                    throw new FormatException($"The resource probe did not return {key}.");
            }

            if (values["cpu_idle"] > values["cpu_total"] ||
                values["mem_available"] > values["mem_total"] ||
                values["disk_used"] > values["disk_total"])
            {
                throw new FormatException("The resource probe returned inconsistent counters.");
            }

            return new LinuxResourceSample(
                values["cpu_total"],
                values["cpu_idle"],
                values["mem_total"],
                values["mem_total"] - values["mem_available"],
                values["disk_total"],
                values["disk_used"],
                values["net_rx"],
                values["net_tx"],
                TimeSpan.FromSeconds(values["uptime_seconds"]));
        }
    }
}
