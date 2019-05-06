using System;
using Agent.Sdk;
using Microsoft.VisualStudio.Services.Content.Common.Telemetry;
using Microsoft.VisualStudio.Services.BlobStore.WebApi.Telemetry;

namespace Agent.Plugins.PipelineArtifact.Telemetry
{
    /// <summary>
    /// Generic telemetry record for use with Pipeline events.
    /// </summary>
    public class PipelineActionTelemetryRecord : AbstractBlobStoreActionTelemetryRecord
    {
        public Guid PlanId { get; private set; }
        public Guid JobId { get; private set; }
        public Guid TaskInstanceId { get; private set; }

        // We pass clientType to the base class's actionName field to make the client type
        // available as a top level field so we might filter on e.g. PipelineArtifact
        public PipelineActionTelemetryRecord(TelemetryInformationLevel level, Uri baseAddress, string clientType, string action, AgentTaskPluginExecutionContext context, uint attemptNumber = 1)
            : base(level, baseAddress, clientType, action, attemptNumber)
        {
            PlanId = Guid.Parse(context.Variables["system.planId"].Value);
            JobId = Guid.Parse(context.Variables["system.jobId"].Value);
            TaskInstanceId = Guid.Parse(context.Variables["system.taskInstanceId"].Value);
        }
    }
}