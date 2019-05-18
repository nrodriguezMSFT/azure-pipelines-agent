using System;
using Agent.Sdk;
using Microsoft.VisualStudio.Services.Content.Common.Telemetry;
using Microsoft.VisualStudio.Services.BlobStore.WebApi.Telemetry;
using Microsoft.VisualStudio.Services.BlobStore.WebApi;

namespace Agent.Plugins.PipelineArtifact.Telemetry
{
    /// <summary>
    /// Generic telemetry record for use with Pipeline Artifact events.
    /// </summary>
    public class PipelineArtifactActionRecord : PipelineTelemetryRecord
    {
        public static string PublishResult { get; private set; }

        public PipelineArtifactActionRecord(TelemetryInformationLevel level, Uri baseAddress, string eventNamePrefix, string eventNameSuffix, AgentTaskPluginExecutionContext context, uint attemptNumber = 1)
            : base(level, baseAddress, eventNamePrefix, eventNameSuffix, context, attemptNumber)
        {
        }

        internal override void SetReturnedProperty<T>(T value)
        {
            Type valueType = typeof(value);
            switch (valueType)
            {
                case BlobStore.WebApi.PublishResult:
                    {
                        PublishResult = value.ToString();
                        break;
                    }
                default:
                    break;
            }
        }
    }
}