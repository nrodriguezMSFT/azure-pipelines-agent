using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Agent.Sdk;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.BlobStore.Common;
using Microsoft.VisualStudio.Services.BlobStore.WebApi;
using Microsoft.VisualStudio.Services.BlobStore.WebApi.Telemetry;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Content.Common.Tracing;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;

namespace Agent.Plugins.PipelineArtifact
{    
    public static class DedupManifestArtifactClientFactory
    {
        private static readonly int DedupStoreClientMaxParallelism = 16 * Environment.ProcessorCount;

        public static DedupManifestArtifactClient CreateDedupManifestClient(AgentTaskPluginExecutionContext context, VssConnection connection, ClientType clientType = ClientType.Unknown)
        {
            var tracer = new CallbackAppTraceSource(str => context.Output(str), SourceLevels.Information);
            var artifactClientTelemetry = new ArtifactClientTelemetry(tracer, clientType);
            return CreateDedupManifestClient(connection, artifactClientTelemetry, tracer);
        }
        
        public static DedupManifestArtifactClient CreateDedupManifestClient(VssConnection connection, ArtifactClientTelemetry artifactClientTelemetry, IAppTraceSource tracer)
        {
            var dedupStoreHttpClient = connection.GetClient<DedupStoreHttpClient>();
            dedupStoreHttpClient.SetTracer(tracer);
            var client = new DedupStoreClientWithDataport(dedupStoreHttpClient, DedupStoreClientMaxParallelism);
            var dedupManifestClient = new DedupManifestArtifactClient(artifactClientTelemetry, client, tracer);
            return dedupManifestClient;
        }
    }
}