using System;
using Agent.Sdk;
using Microsoft.VisualStudio.Services.Content.Common.Telemetry;
using Microsoft.VisualStudio.Services.BlobStore.Common.Telemetry;
using Microsoft.VisualStudio.Services.BlobStore.WebApi;

namespace Agent.Plugins.PipelineArtifact.Telemetry
{
    /// <summary>
    /// Generic telemetry record for use with Pipeline Artifact events.
    /// </summary>
    public class PipelineArtifactActionRecord : PipelineTelemetryRecord
    {
        public static long FileCount { get; private set; }

        public PipelineArtifactActionRecord(TelemetryInformationLevel level, Uri baseAddress, string eventNamePrefix, string eventNameSuffix, AgentTaskPluginExecutionContext context, uint attemptNumber = 1)
            : base(level, baseAddress, eventNamePrefix, eventNameSuffix, context, attemptNumber)
        {
        }

        protected override void SetMeasuredActionResult<T>(T value)
        {
            string valueType = value.GetType().ToString();
            if (valueType == nameof(PublishResult)
            {
                PublishResult result = value as PublishResult;
                FileCount = result.FileCount;
            }
        }
    }
}