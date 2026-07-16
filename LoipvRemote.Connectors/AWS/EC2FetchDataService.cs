using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Connectors.AWS
{
    public class EC2FetchDataService
    {
        private static DateTime lastFetch;
        private static List<InstanceInfo>? lastData;

        // input must be in format "AWSAPI:instanceid" where instanceid is the ec2 instance id, e.g. i-066f750a76c97583d
        public static async Task<string> GetEC2InstanceDataAsync(
            string input,
            string region,
            IExternalCredentialPrompt prompt,
            IExternalCredentialSettingsStore settings,
            CancellationToken cancellationToken = default)
        {
            // get secret id
            if (!input.StartsWith("AWSAPI:", StringComparison.Ordinal))
                throw new ArgumentException("Calling this function requires AWSAPI: input.", nameof(input));
            string InstanceID = input[7..];

            // init connection credentials, display popup if necessary
            if (!await AWSConnectionData.InitAsync(prompt, settings, cancellationToken).ConfigureAwait(false))
                return string.Empty;
            var alldata = await GetEC2IPDataAsync(region, cancellationToken).ConfigureAwait(false);
            var found = alldata.Where(x => x.InstanceId == InstanceID).SingleOrDefault();
            return (found == null) ? "" : found.PublicIP;
        }

        private static async Task<List<InstanceInfo>> GetEC2IPDataAsync(string region, CancellationToken cancellationToken)
        {
            // caching
            TimeSpan timeSpan = DateTime.Now - lastFetch;
            if (timeSpan.TotalMinutes < 1 && lastData != null)
                return lastData;

            //AWSConfigs.AWSRegion = AWSConnectionData.region;
            AWSConfigs.AWSRegion = region;
            string awsAccessKeyId = AWSConnectionData.awsKeyID;
            string awsSecretAccessKey = AWSConnectionData.awsKey;

            var _client = new AmazonEC2Client(awsAccessKeyId, awsSecretAccessKey, RegionEndpoint.EUCentral1);
            bool done = false;

            List<InstanceInfo> instanceList = new();
            var request = new DescribeInstancesRequest();
            while (!done)
            {
                DescribeInstancesResponse response = await _client.DescribeInstancesAsync(request, cancellationToken).ConfigureAwait(false);

                foreach (var reservation in response.Reservations)
                {
                    foreach (var instance in reservation.Instances)
                    {
                        string vmname = "";
                        foreach (var tag in instance.Tags)
                        {
                            if (tag.Key == "Name")
                            {
                                vmname = tag.Value;
                            }
                        }
                        InstanceInfo inf = new(instance, vmname);
                        instanceList.Add(inf);
                    }
                }

                request.NextToken = response.NextToken;

                if (response.NextToken == null)
                {
                    done = true;
                }
            }

            lastData = instanceList.OrderBy(x => x.Name).ToList();
            lastFetch = DateTime.Now;
            return lastData;
        }


        public static class AWSConnectionData
        {
            internal static string awsKeyID = "";
            internal static string awsKey = "";
            //public static string _region = "eu-central-1";

            public static async Task<bool> InitAsync(
                IExternalCredentialPrompt prompt,
                IExternalCredentialSettingsStore settings,
                CancellationToken cancellationToken)
            {
                if (awsKey != "")
                    return true;
                cancellationToken.ThrowIfCancellationRequested();
                AwsPromptResult? result = await prompt.PromptAwsAsync(
                    new AwsPromptRequest(
                        settings.GetString("AWS", "KeyID") ?? string.Empty,
                        settings.GetString("AWS", "Key") ?? string.Empty),
                    cancellationToken).ConfigureAwait(true);

                if (result is null)
                    return false;

                // store values to memory
                awsKeyID = result.AccessKeyId;
                awsKey = result.SecretKey;


                // write values to registry
                settings.SetString("AWS", "KeyID", awsKeyID);
                settings.SetString("AWS", "Key", awsKey);
                return true;
            }
        }

    }
}
