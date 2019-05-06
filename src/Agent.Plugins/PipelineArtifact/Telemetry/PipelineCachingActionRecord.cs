using System;
using Agent.Sdk;
using Microsoft.VisualStudio.Services.Content.Common.Telemetry;
using Microsoft.VisualStudio.Services.BlobStore.WebApi.Telemetry;

namespace Agent.Plugins.PipelineArtifact.Telemetry
{
    /// <summary>
    /// Generic telemetry record for use with Pipeline Artifact Action events.
    /// </summary>
    public class PipelineCachingActionRecord : PipelineActionTelemetryRecord
    {
        public PipelineCachingActionRecord(TelemetryInformationLevel level, Uri baseAddress, string clientType, string action, AgentTaskPluginExecutionContext context, uint attemptNumber = 1)
            : base(level, baseAddress, clientType, action, context, attemptNumber)
        {
        }
    }
}