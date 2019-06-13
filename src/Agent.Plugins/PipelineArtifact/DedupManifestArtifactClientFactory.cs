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
using Microsoft.VisualStudio.Services.BlobStore.Common.Telemetry;
using Microsoft.VisualStudio.Services.BlobStore.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Content.Common.Tracing;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;

namespace Agent.Plugins.PipelineArtifact
{    
    public static class DedupManifestArtifactClientFactory
    {
        public static DedupManifestArtifactClient CreateDedupManifestClient(AgentTaskPluginExecutionContext context, VssConnection connection, out BlobStoreClientTelemetry telemetry)
        {
            var dedupStoreHttpClient = connection.GetClient<DedupStoreHttpClient>();
            var tracer = new CallbackAppTraceSource(str => context.Output(str), SourceLevels.Information);
            dedupStoreHttpClient.SetTracer(tracer);
            var client = new DedupStoreClientWithDataport(dedupStoreHttpClient, PipelineArtifactProvider.GetDedupStoreClientMaxParallelism(context));
            return new DedupManifestArtifactClient(telemetry = new BlobStoreClientTelemetry(tracer, dedupStoreHttpClient.BaseAddress), client, tracer);
        }
    }
}