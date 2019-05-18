using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;
using Agent.Plugins.PipelineArtifact;
using Agent.Plugins.PipelineArtifact.Telemetry;
using Agent.Sdk;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.BlobStore.Common;
using Microsoft.VisualStudio.Services.BlobStore.WebApi;
using Microsoft.VisualStudio.Services.BlobStore.WebApi.Telemetry;
using Microsoft.VisualStudio.Services.Content.Common;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Content.Common.Tracing;
using Microsoft.VisualStudio.Services.PipelineCache.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;

namespace Agent.Plugins.PipelineCache
{    
    public class PipelineCacheServer
    {
        public const string RootId = "RootId";
        public const string ProofNodes = "ProofNodes";
        public const string SaveCache = "SaveCache";
        public const string RestoreCache = "RestoreCache";
        private const string PipelineCacheVarPrefix = "PipelineCache";

        internal async Task UploadAsync(
            AgentTaskPluginExecutionContext context,
            IEnumerable<string> key,
            string path,
            string salt,
            CancellationToken cancellationToken)
        {
            VssConnection connection = context.VssConnection;
            BlobStoreClientTelemetry clientTelemetry;
            DedupManifestArtifactClient dedupManifestClient = DedupManifestArtifactClientFactory.CreateDedupManifestClient(context, connection, out clientTelemetry);

            using (clientTelemetry)
            {
                //Upload the pipeline artifact.
                PipelineCachingActionRecord uploadRecord = clientTelemetry.CreateRecord<PipelineCachingActionRecord>((level, uri, type) =>
                    new PipelineCachingActionRecord(level, uri, type, nameof(dedupManifestClient.PublishAsync), context));
                PublishResult result = await clientTelemetry.MeasureActionAsync(
                    record: uploadRecord,
                    actionAsync: async () =>
                    {
                        return await dedupManifestClient.PublishAsync(path, cancellationToken);
                    }
                ).ConfigureAwait(false);

                CreatePipelineCacheArtifactOptions options = new CreatePipelineCacheArtifactOptions
                {
                    Key = key,
                    RootId = result.RootId,
                    ManifestId = result.ManifestId,
                    ProofNodes = result.ProofNodes.ToArray(),
                    Salt = salt
                };

                // Cache the artifact
                // TODO: Determine what telemetry needs to be captured from inside the PipelineCacheClient
                // and if telemetry instance needs to be passed through
                PipelineCacheClient pipelineCacheClient = this.CreateClient(context, connection);
                PipelineCachingActionRecord cachingRecord = clientTelemetry.CreateRecord<PipelineCachingActionRecord>((level, uri, type) =>
                    new PipelineCachingActionRecord(level, uri, type, SaveCache, context));
                CreateStatus status = await clientTelemetry.MeasureActionAsync(
                    record: cachingRecord,
                    actionAsync: async () =>
                    {
                        return await pipelineCacheClient.CreatePipelineCacheArtifactAsync(options, cancellationToken);
                    }
                ).ConfigureAwait(false);
                context.Output("Saved item.");
            }
        }

        internal async Task DownloadAsync(
            AgentTaskPluginExecutionContext context,
            IEnumerable<string> key,
            string path,
            string salt,
            string variableToSetOnHit,
            CancellationToken cancellationToken)
        {
            VssConnection connection = context.VssConnection;
            
            BlobStoreClientTelemetry clientTelemetry;
            DedupManifestArtifactClient dedupManifestClient = DedupManifestArtifactClientFactory.CreateDedupManifestClient(context, connection, out clientTelemetry);
            var pipelineCacheClient = this.CreateClient(context, connection);

            GetPipelineCacheArtifactOptions options = new GetPipelineCacheArtifactOptions
            {
                Key = key,
                Salt = salt,
            };
            
            PipelineCachingActionRecord cachingRecord = clientTelemetry.CreateRecord<PipelineCachingActionRecord>((level, uri, type) =>
                    new PipelineCachingActionRecord(level, uri, type, RestoreCache, context));
            PipelineCacheArtifact result = await clientTelemetry.MeasureActionAsync(
                record: cachingRecord,
                actionAsync: async () =>
                {
                    return await pipelineCacheClient.GetPipelineCacheArtifactAsync(options, cancellationToken);
                }
            ).ConfigureAwait(false);

            if (result == null)
            {
                return;
            }
            else
            {
                context.Output($"Manifest ID is: {result.ManifestId.ValueString}");
                PipelineCachingActionRecord downloadRecord = clientTelemetry.CreateRecord<PipelineCachingActionRecord>((level, uri, type) =>
                    new PipelineCachingActionRecord(level, uri, type, nameof(DownloadAsync), context));
                await clientTelemetry.MeasureActionAsync(
                    record: downloadRecord,
                    actionAsync: async () =>
                    {
                        await this.DownloadPipelineCacheAsync(dedupManifestClient, result.ManifestId, path, cancellationToken);
                    }
                ).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(variableToSetOnHit))
                {
                    context.SetVariable($"{PipelineCacheVarPrefix}.{variableToSetOnHit}", "True");
                }
                Console.WriteLine("Cache restored.");
            }
        }

        private PipelineCacheClient CreateClient(
            AgentTaskPluginExecutionContext context,
            VssConnection connection)
        {
            var tracer = new CallbackAppTraceSource(str => context.Output(str), System.Diagnostics.SourceLevels.Information);
            IClock clock = UtcClock.Instance;           
            var pipelineCacheHttpClient = connection.GetClient<PipelineCacheHttpClient>();
            var pipelineCacheClient = new PipelineCacheClient(pipelineCacheHttpClient, clock, tracer);

            return pipelineCacheClient;
        }

        private Task DownloadPipelineCacheAsync(
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