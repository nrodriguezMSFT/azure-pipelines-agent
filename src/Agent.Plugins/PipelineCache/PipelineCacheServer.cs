using Agent.Plugins.PipelineArtifact;
using Agent.Sdk;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.BlobStore.Common;
using Microsoft.VisualStudio.Services.BlobStore.WebApi;
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

            //Upload the pipeline artifact.
            var result = await dedupManifestClient.PublishAsync(sourceDirectory, cancellationToken);

            Dictionary<string, string> propertiesDictionary = new Dictionary<string, string>();
            propertiesDictionary.Add(RootId, result.RootId.ValueString);
            propertiesDictionary.Add(ProofNodes, StringUtil.ConvertToJson(result.ProofNodes.ToArray()));
            var branchScope = "myscope";

            var pipelineCacheHttpClient = connection.GetClient<PipelineCacheHttpClient>();
            pipelineCacheHttpClient.CreatePipelineCacheArtifactAsync(fingerprint, branchScope, result.RootId.ValueString, result.ManifestId.ValueString);
        }

        internal Task DownloadAsync(
            AgentTaskPluginExecutionContext context,
            IEnumerable<string> fingerprintPaths,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            VssConnection connection = context.VssConnection;
            DedupManifestArtifactClient dedupManifestClient = DedupManifestArtifactClientFactory.CreateDedupManifestClient(context, connection);

            // Get the manifest ID from pipeline cache based on the fingerprint.
            var pipelineCacheHttpClient = connection.GetClient<PipelineCacheHttpClient>();
            var res = pipelineCacheHttpClient.GetPipelineCacheArtifactAsync(fingerprint, branchScope);

            //Now we have the manifest ID, call BDM to get the content.
            await this.DownloadPipelineCache(dedupManifestClient, res, targetDirectory, cancellationToken);
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
                minimatchPatterns: minimatchFilters);
            return dedupManifestClient.DownloadAsync(options, cancellationToken);
        }
    }
}