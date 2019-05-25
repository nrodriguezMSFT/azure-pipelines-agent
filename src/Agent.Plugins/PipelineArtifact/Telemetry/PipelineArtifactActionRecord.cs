using System;
using Agent.Sdk;
using Microsoft.VisualStudio.Services.Content.Common.Telemetry;
using Microsoft.VisualStudio.Services.BlobStore.WebApi;
using Microsoft.VisualStudio.Services.BlobStore.WebApi.Telemetry;

namespace Agent.Plugins.PipelineArtifact.Telemetry
{
    /// <summary>
    /// Generic telemetry record for use with Pipeline Artifact events.
    /// </summary>
    public class PipelineArtifactActionRecord : PipelineTelemetryRecord
    {
        public static long FileCount { get; private set; }
        private const string PublishResultTypeName = "PublishResult";

        public PipelineArtifactActionRecord(TelemetryInformationLevel level, Uri baseAddress, string eventNamePrefix, string eventNameSuffix, AgentTaskPluginExecutionContext context, uint attemptNumber = 1)
            : base(level, baseAddress, eventNamePrefix, eventNameSuffix, context, attemptNumber)
        {
        }

        protected override void SetMeasuredActionResult<T>(T value)
        {
            string valueType = value.GetType().ToString();
            switch (valueType)
            {
                case PublishResultTypeName:
                    {
                        PublishResult result = value as PublishResult;
                        FileCount = result.FileCount;
                        break;
                    }
                default:
                    break;
            }
        }
    }
}