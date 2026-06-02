using System.IO.Compression;
using System.IO.Pipelines;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.Preview.GetZipDetails;
using PlikShare.Files.Preview.GetZipDetails.Contracts;
using PlikShare.Files.Records;
using PlikShare.Storages.Zip;
using PlikShare.Workspaces.Cache;
using Serilog;
using Serilog.Events;
using static PlikShare.Files.PreSignedLinks.PreSignedUrlsService;

namespace PlikShare.Files.Preview.GetZipBulkDownloadLink;

public static class ZipBulkDownloadStreamer
{
    // Resolves a selection against the source zip's CDFH and streams the surviving
    // entries into a fresh zip on the wire. Selection is interpreted by the same
    // include/exclude semantics the frontend uses (a folder selection cascades to
    // descendants; an excluded ancestor wins over a selected one). Re-compression
    // happens on the fly via ZipArchive(Create) — pass-through deflate is a
    // possible later optimization but not needed for first cut.
    public static async Task StreamAsync(
        FileRecord sourceFile,
        WorkspaceContext workspace,
        ZipBulkDownloadPayload payload,
        IReadOnlyList<ZipCdfhRecord> cdfhEntries,
        PipeWriter output,
        Func<FileRecord, FileEncryptionMode> getFileEncryptionMode,
        CancellationToken cancellationToken)
    {
        var preview = ZipPreviewResponseBuilder.Build(cdfhEntries);
        var foldersById = preview.Folders.ToDictionary(f => f.Id);
        var entryByIndex = cdfhEntries.ToDictionary(e => e.IndexInArchive);

        var selectedFolders = payload.SelectedFolderIds.ToHashSet();
        var selectedEntries = payload.SelectedEntryIndices.ToHashSet();
        var excludedFolders = payload.ExcludedFolderIds.ToHashSet();
        var excludedEntries = payload.ExcludedEntryIndices.ToHashSet();

        var plan = ResolveSelection(
            items: preview.Items,
            foldersById: foldersById,
            selectedFolders: selectedFolders,
            selectedEntries: selectedEntries,
            excludedFolders: excludedFolders,
            excludedEntries: excludedEntries);

        Log.Information(
            "Zip bulk download starting: {SelectedCount} entries resolved from {TotalEntries} CDFH records of File '{FileExternalId}'",
            plan.Count, cdfhEntries.Count, sourceFile.ExternalId);

        await using var archive = new ZipArchive(
            stream: output.AsStream(),
            mode: ZipArchiveMode.Create,
            leaveOpen: true);

        var uniqueNames = new UniqueFileNames(
            capacity: plan.Count);

        var startTime = DateTime.UtcNow;
        var processedFiles = 0;
        var totalBytes = 0L;

        foreach (var (item, outputPath) in plan)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!entryByIndex.TryGetValue(item.IndexInArchive, out var cdfhRecord))
            {
                // Defensive: the planner walks the same Items list the builder produces,
                // so a miss here means the CDFH was mutated under us — skip rather than throw.
                Log.Warning("Zip bulk download: planned item with index {Index} has no CDFH record in File '{FileExternalId}', skipping.",
                    item.IndexInArchive, sourceFile.ExternalId);
                continue;
            }

            // Collisions across selection roots: two items selected from different
            // folders that both have the same basename. UniqueFileNames appends
            // " (N)" to disambiguate without dropping any entry.
            var (entryPath, wasCollision) = uniqueNames.EnsureUniqueFileName(
                fullFileName: outputPath);

            if (wasCollision)
            {
                Log.Debug("Zip bulk download: name collision for {OriginalPath} resolved to {EntryPath}",
                    outputPath, entryPath);
            }

            var entry = archive.CreateEntry(
                entryPath,
                CompressionLevel.Fastest);

            await using var entryStream = await entry.OpenAsync(
                cancellationToken);

            var entryWriter = PipeWriter.Create(
                stream: entryStream,
                writerOptions: new StreamPipeWriterOptions(leaveOpen: false));

            try
            {
                await ZipEntryReader.ReadEntryAsync(
                    file: sourceFile,
                    entry: new ZipEntryPayload
                    {
                        FileName = cdfhRecord.FileName,
                        CompressedSizeInBytes = cdfhRecord.CompressedSize,
                        SizeInBytes = cdfhRecord.UncompressedSize,
                        OffsetToLocalFileHeader = cdfhRecord.OffsetToLocalFileHeader,
                        FileNameLength = cdfhRecord.FileNameLength,
                        CompressionMethod = cdfhRecord.CompressionMethod,
                        IndexInArchive = cdfhRecord.IndexInArchive
                    },
                    workspace: workspace,
                    output: entryWriter,
                    getFileEncryptionMode: getFileEncryptionMode,
                    cancellationToken: cancellationToken);

                processedFiles++;
                totalBytes += cdfhRecord.UncompressedSize;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Log.Warning(e,
                    "Zip bulk download: failed to stream entry '{EntryName}' (index {Index}) from File '{FileExternalId}', skipping.",
                    cdfhRecord.FileName, cdfhRecord.IndexInArchive, sourceFile.ExternalId);
            }
        }

        if (Log.IsEnabled(LogEventLevel.Information))
        {
            var duration = DateTime.UtcNow - startTime;
            Log.Information(
                "Zip bulk download finished: {Processed}/{Planned} entries, {Bytes:N0} bytes uncompressed, {DurationMs:N0}ms",
                processedFiles, plan.Count, totalBytes, duration.TotalMilliseconds);
        }
    }

    private static List<(GetZipFileDetailsItemDto Item, string OutputPath)> ResolveSelection(
        IReadOnlyList<GetZipFileDetailsItemDto> items,
        IReadOnlyDictionary<uint, ZipVirtualFolderDto> foldersById,
        HashSet<uint> selectedFolders,
        HashSet<uint> selectedEntries,
        HashSet<uint> excludedFolders,
        HashSet<uint> excludedEntries)
    {
        var plan = new List<(GetZipFileDetailsItemDto, string)>();

        foreach (var item in items)
        {
            if (IsExcluded(item, excludedEntries, excludedFolders, foldersById))
                continue;

            if (!IsIncluded(item, selectedEntries, selectedFolders, foldersById))
                continue;

            var outputPath = BuildOutputPath(
                item: item,
                selectedEntries: selectedEntries,
                selectedFolders: selectedFolders,
                foldersById: foldersById);

            plan.Add((item, outputPath));
        }

        return plan;
    }

    private static bool IsExcluded(
        GetZipFileDetailsItemDto item,
        HashSet<uint> excludedEntries,
        HashSet<uint> excludedFolders,
        IReadOnlyDictionary<uint, ZipVirtualFolderDto> foldersById)
    {
        if (excludedEntries.Contains(item.IndexInArchive))
            return true;

        var currentId = item.VirtualFolderId;

        while (currentId.HasValue)
        {
            if (excludedFolders.Contains(currentId.Value))
                return true;

            currentId = foldersById[currentId.Value].ParentId;
        }

        return false;
    }

    private static bool IsIncluded(
        GetZipFileDetailsItemDto item,
        HashSet<uint> selectedEntries,
        HashSet<uint> selectedFolders,
        IReadOnlyDictionary<uint, ZipVirtualFolderDto> foldersById)
    {
        if (selectedEntries.Contains(item.IndexInArchive))
            return true;

        var currentId = item.VirtualFolderId;

        while (currentId.HasValue)
        {
            if (selectedFolders.Contains(currentId.Value))
                return true;

            currentId = foldersById[currentId.Value].ParentId;
        }

        return false;
    }

    // An item lands at the zip root if it was selected individually (no folder
    // ancestor justifies its inclusion). Otherwise inclusion came via a selected
    // ancestor folder — that folder becomes the output root, and any folders
    // above it are stripped (the user did not select them and would not expect
    // them in the archive).
    private static string BuildOutputPath(
        GetZipFileDetailsItemDto item,
        HashSet<uint> selectedEntries,
        HashSet<uint> selectedFolders,
        IReadOnlyDictionary<uint, ZipVirtualFolderDto> foldersById)
    {
        if (selectedEntries.Contains(item.IndexInArchive))
            return item.FileName;

        var segments = new List<string> { item.FileName };
        var currentId = item.VirtualFolderId;

        while (currentId.HasValue)
        {
            var folder = foldersById[currentId.Value];
            segments.Insert(0, folder.Name);

            if (selectedFolders.Contains(folder.Id))
                break;

            currentId = folder.ParentId;
        }

        return string.Join('/', segments);
    }
}
