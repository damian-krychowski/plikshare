namespace PlikShare.Uploads.Algorithm
{
    public enum UploadAlgorithm
    {
        /// <summary>
        /// Single-step upload algorithm optimized for small files.
        /// The client receives a single upload URL during initialization and uploads the entire file in one request.
        /// No additional callbacks or confirmations are required, making it the most efficient choice for small files.
        /// 
        /// Flow:
        /// 1. Initialize upload -> receive upload URL
        /// 2. Upload complete file to the URL
        /// </summary>
        DirectUpload = 0,

        /// <summary>
        /// Two-step upload algorithm optimized for medium-sized files that can fit in a single chunk.
        /// Utilizes pre-signed S3 URLs to optimize server bandwidth usage while maintaining simplified flow.
        /// Requires ETag verification for upload completion.
        /// 
        /// Flow:
        /// 1. Initialize upload -> receive pre-signed URL
        /// 2. Upload file as single chunk -> receive ETag
        /// 3. Confirm upload with ETag verification
        /// 
        /// Recommended for files that exceed direct upload size threshold but don't require multi-part handling.
        /// </summary>
        SingleChunkUpload = 1,

        /// <summary>
        /// Multi-step chunked upload algorithm designed for large files requiring segmented transmission.
        /// Implements a complete chunked upload protocol with individual part tracking and verification.
        /// 
        /// Flow:
        /// 1. Initialize upload -> receive upload ID
        /// 2. For each chunk:
        ///    a. Initialize part -> receive chunk-specific URL
        ///    b. Upload part -> receive part ETag
        ///    c. Confirm part upload with ETag
        /// 3. Complete upload with all part ETags
        /// 
        /// Recommended for:
        /// - Large files exceeding single chunk size limits
        /// - Scenarios requiring upload resume capability
        /// - Cases needing progress tracking and part verification
        /// </summary>
        MultiStepChunkUpload = 2,
    }
}
