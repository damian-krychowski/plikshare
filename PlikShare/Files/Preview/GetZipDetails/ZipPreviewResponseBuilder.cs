using PlikShare.Files.Preview.GetZipDetails.Contracts;
using PlikShare.Storages.Zip;

namespace PlikShare.Files.Preview.GetZipDetails;

public static class ZipPreviewResponseBuilder
{
    // Walks zip CDFH entries in IndexInArchive order, splits each entry's path into
    // segments root-first and assigns a virtual folder id the first time a
    // (parent, name) pair is encountered. CDFH byte layout is immutable for a given
    // zip file, so two independent decodings yield identical ids — the frontend can
    // send them back as an opaque selection key without server-side state.
    public static GetZipFileDetailsResponseDto Build(IReadOnlyList<ZipCdfhRecord> cdfhEntries)
    {
        var items = new List<GetZipFileDetailsItemDto>(capacity: cdfhEntries.Count);
        var folders = new List<ZipVirtualFolderDto>();
        var folderMap = new Dictionary<(uint? parentId, string name), uint>();
        // Start at 1 so that 0 is never a real folder id. proto3 collapses "field
        // absent" and "field = 0" on the wire, so reserving 0 lets the client treat
        // a decoded 0 unambiguously as "no folder / no parent".
        uint nextFolderId = 1;

        foreach (var entry in cdfhEntries)
        {
            if (entry.UncompressedSize == 0)
                continue;

            // Zip spec allows messy FileName values — leading slash, double slashes,
            // trailing slash. RemoveEmptyEntries collapses them in one shot; the
            // length guard drops degenerate "/" / "" entries entirely.
            var parts = entry.FileName.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                continue;

            var fileName = parts[^1];
            uint? parentId = null;

            // Last segment is the file name itself — iterate only over the
            // preceding segments, which are the folder names along the path.
            for (var i = 0; i < parts.Length - 1; i++)
            {
                var segment = parts[i];

                // Same segment name can appear under different parents (two "src"
                // in different subtrees are different folders), so the lookup key
                // must include parentId — segment alone would collapse them.
                var key = (parentId, segment);

                if (!folderMap.TryGetValue(key, out var id))
                {
                    id = nextFolderId;
                    nextFolderId++;
                    folderMap[key] = id;
                    folders.Add(new ZipVirtualFolderDto
                    {
                        Id = id,
                        ParentId = parentId,
                        Name = segment
                    });
                }

                parentId = id;
            }

            items.Add(new GetZipFileDetailsItemDto
            {
                FileName = fileName,
                VirtualFolderId = parentId,
                CompressedSizeInBytes = entry.CompressedSize,
                SizeInBytes = entry.UncompressedSize,
                OffsetToLocalFileHeader = entry.OffsetToLocalFileHeader,
                FileNameLength = entry.FileNameLength,
                CompressionMethod = entry.CompressionMethod,
                IndexInArchive = entry.IndexInArchive
            });
        }

        return new GetZipFileDetailsResponseDto
        {
            Items = items,
            Folders = folders
        };
    }
}
