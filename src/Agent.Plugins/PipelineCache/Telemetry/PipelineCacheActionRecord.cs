using System;
using Agent.Sdk;
using Agent.Plugins.PipelineArtifact.Telemetry;
using Microsoft.VisualStudio.Services.Content.Common.Telemetry;
using Microsoft.VisualStudio.Services.BlobStore.WebApi;
using Microsoft.VisualStudio.Services.BlobStore.WebApi.Telemetry;
using Microsoft.VisualStudio.Services.PipelineCache.WebApi;

namespace Agent.Plugins.PipelineCache.Telemetry
{
    /// <summary>
    /// Generic telemetry record for use with Pipeline Caching events.
    /// </summary>
    public class PipelineCacheActionRecord : PipelineTelemetryRecord
    {
        public static string CacheResult { get; private set; }
        public static string FileCount { get; private set; }
        private const string CacheHit = "Hit";
        private const string CacheMiss = "Miss";
        // switch cases must be constant, and switching on type is not supported.
        private const string CreateStatusTypeName = "CreateStatus";
        private const string PipelineCacheArtifactTypeName = "PipelineCacheArtifact";
        private const string PublishResultTypeName = "PublishResult";

        public PipelineCacheActionRecord(TelemetryInformationLevel level, Uri baseAddress, string eventNamePrefix, string eventNameSuffix, AgentTaskPluginExecutionContext context, uint attemptNumber = 1)
            : base(level, baseAddress, eventNamePrefix, eventNameSuffix, context, attemptNumber)
        {
        }

        protected override void SetMeasuredActionResult<T>(T value)
        {
            if (value == null)
            {
                // if value is null, then GetPipelineCacheArtifactAsync() returned null, a cache miss.
                CacheResult = CacheMiss;
                return;
            }

            string valueType = value.GetType().Name;

            switch (valueType)
            {
                case PipelineCacheArtifactTypeName:
                    {                        
                        // If the artifact exists, it is a hit.
                        CacheResult = CacheHit;
                        break;
                    }
                case PublishResultTypeName:
                    {
                        PublishResult result = value as PublishResult;
                        FileCount = result.FileCount.ToString();
                        break;
                    }
                case CreateStatusTypeName:
                    {
                        // If the cache entry was created, then there was a cache miss.
                        CacheResult = value.ToString() == CreateStatus.Success.ToString() ? CacheMiss : CacheHit;
                        break;
                    }
                default:
                    break;
            }
        }
    }
}