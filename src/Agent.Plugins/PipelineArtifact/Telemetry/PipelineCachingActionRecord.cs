using System;
using Agent.Sdk;
using Microsoft.VisualStudio.Services.Content.Common.Telemetry;
using Microsoft.VisualStudio.Services.BlobStore.WebApi.Telemetry;

namespace Agent.Plugins.PipelineArtifact.Telemetry
{
    /// <summary>
    /// Generic telemetry record for use with Pipeline Caching events.
    /// </summary>
    public class PipelineCachingActionRecord : PipelineTelemetryRecord
    {
        public PipelineCachingActionRecord(TelemetryInformationLevel level, Uri baseAddress, string eventNamePrefix, string eventNameSuffix, AgentTaskPluginExecutionContext context, uint attemptNumber = 1)
            : base(level, baseAddress, eventNamePrefix, eventNameSuffix, context, attemptNumber)
        {
        }
    }
}