using Agent.Plugins.PipelineArtifact;
using Agent.Sdk;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.BlobStore.Common;
using Microsoft.VisualStudio.Services.BlobStore.WebApi;
using Microsoft.VisualStudio.Services.Content.Common;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Content.Common.Tracing;
using Microsoft.VisualStudio.Services.PipelineCache.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Agent.Plugins.PipelineCache
{    
    public class PipelineCacheServer
    {
        public static readonly string RootId = "RootId";
        public static readonly string ProofNodes = "ProofNodes";

        internal async Task UploadAsync(
            AgentTaskPluginExecutionContext context,
            IEnumerable<string> fingerprintPaths,
            string sourceDirectory,
            CancellationToken cancellationToken)
        {
            VssConnection connection = context.VssConnection;
            DedupManifestArtifactClient dedupManifestClient = DedupManifestArtifactClientFactory.CreateDedupManifestClient(context, connection);

            var result = await dedupManifestClient.PublishAsync(sourceDirectory, cancellationToken);

            Dictionary<string, string> propertiesDictionary = new Dictionary<string, string>();
            propertiesDictionary.Add(RootId, result.RootId.ValueString);
            propertiesDictionary.Add(ProofNodes, StringUtil.ConvertToJson(result.ProofNodes.ToArray()));
            var branchScope = "myscope";
            var salt = "salt";
            Console.WriteLine("Root id is: {0} and manifest id is:{1} and proof node is:{2}", result.RootId.ValueString, result.ManifestId.ValueString, result.ProofNodes);

            CreatePipelineCacheArtifactOptions options = new CreatePipelineCacheArtifactOptions();
            options.FingerprintFilePaths = fingerprintPaths;
            options.RootId = result.RootId;
            options.ManifestId = result.ManifestId;
            options.Scope = branchScope;
            options.ProofNodes = result.ProofNodes.ToArray();
            options.Salt = salt;

            IClock clock = UtcClock.Instance;

            var pipelineCacheHttpClient = connection.GetClient<PipelineCacheHttpClient>();

            var pipelineCacheClient = new PipelineCacheClient(pipelineCacheHttpClient, clock);
            await pipelineCacheClient.CreatePipelineCacheArtifactAsync(options, cancellationToken);

            Console.WriteLine("Cache Stored!");
        }

        internal async Task DownloadAsync(
            AgentTaskPluginExecutionContext context,
            IEnumerable<string> fingerprintPaths,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            VssConnection connection = context.VssConnection;
            DedupManifestArtifactClient dedupManifestClient = DedupManifestArtifactClientFactory.CreateDedupManifestClient(context, connection);

            // Get the manifest ID from pipeline cache based on the fingerprint.
            IClock clock = UtcClock.Instance;           
            var pipelineCacheHttpClient = connection.GetClient<PipelineCacheHttpClient>();
            var pipelineCacheClient = new PipelineCacheClient(pipelineCacheHttpClient, clock); // Do we need expiration time?

            if(pipelineCacheClient == null)
            {
                Console.WriteLine(" No cache item exists!");
                return;
            }

            GetPipelineCacheArtifactOptions options = new GetPipelineCacheArtifactOptions();
            options.FingerprintFilePaths = fingerprintPaths;
            options.Scope = "myscope";
            options.Salt = "salt";

            var result = await pipelineCacheClient.GetPipelineCacheArtifactAsync(options, cancellationToken);

            Console.WriteLine("Manifest ID is : {0}", result.ManifestId.ValueString);

            //Now we have the manifest ID, call BDM to get the content.
            await this.DownloadPipelineCache(dedupManifestClient, result.ManifestId , targetDirectory, cancellationToken);
            Console.WriteLine("Cache Restored!");
        }

        private Task DownloadPipelineCache(
            DedupManifestArtifactClient dedupManifestClient,
            DedupIdentifier manifestId,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            DownloadPipelineArtifactOptions options = DownloadPipelineArtifactOptions.CreateWithManifestId(
                manifestId,
                targetDirectory,
                proxyUri: null,
                minimatchPatterns: null);
            return dedupManifestClient.DownloadAsync(options, cancellationToken);
        }
    }
}