using System;
using Agent.Sdk;
using Agent.Plugins.PipelineArtifact.Telemetry;
using Microsoft.VisualStudio.Services.Content.Common.Telemetry;
using Microsoft.VisualStudio.Services.BlobStore.WebApi.Telemetry;
using Microsoft.VisualStudio.Services.PipelineCache.WebApi;

namespace Agent.Plugins.PipelineCaching.Telemetry
{
    
    /// <summary>
    /// Generic telemetry record for use with Pipeline Caching events.
    /// </summary>
    public class PipelineCachingActionRecord : PipelineTelemetryRecord
    {
        public static string CacheResult { get; private set; }
        private const string CacheHit = "Hit";
        private const string CacheMiss = "Miss";
        private const string CacheEntryExists = "EntryExists";
        public PipelineCachingActionRecord(TelemetryInformationLevel level, Uri baseAddress, string eventNamePrefix, string eventNameSuffix, AgentTaskPluginExecutionContext context, uint attemptNumber = 1)
            : base(level, baseAddress, eventNamePrefix, eventNameSuffix, context, attemptNumber)
        {
        }

        internal override void SetReturnedProperty<T>(T value)
        {
            // GetPipelineCacheArtifactAsync()
            // valueType == PipelineCachingArtifact 
            // CacheHit

            // GetPipelineCacheArtifactAsync()
            // value == null 
            // CacheMiss

            // CreatePipelineCacheArtifactAsync()
            // value == CreateStatus.Success 
            // CacheMiss
            
            // CreatePipelineCacheArtifactAsync()
            // value == CreateStatus.Conflict 
            // CacheHit
            // CacheEntryExists

            if (value == null)
            {
                // if value is null, then GetPipelineCacheArtifactAsync() returned null, a cache miss.
                CacheResult = CacheMiss;
                return;
            }

            Type valueType = value.GetType();
            switch (valueType)
            {
                case PipelineCachingArtifact:
                    {                        
                        // If the artifact exists it is a hit.
                        CacheResult = CacheHit;
                        break;
                    }
                case CreateStatus:
                    {
                        // If the cache entry was created, then there was a cache miss.
                        CacheResult = value == CreateStatus.Success ? CacheMiss : CacheEntryExists;
                    }
                default:
                    break;
            }
        }
    }
}