
//information stored inside zip LFH record should not be used to decode file size etc
//as they may be not reliable (sic!) - we should use Central Directory record for those
//the only information we MUST read from here is extraFieldLength, becuase it is not present

import { computed, signal } from "@angular/core";
import { toNameAndExtension } from "./filte-type";
import { countSelectedDescendants, StaticTreeNode, StaticFolderNode, StaticFileNode } from "../shared/static-file-tree-view/static-file-tree-view.component";

//anywhere else, and the one from Central Directory Record may be different than this one
export type ZipLfhRecord = {
    versionNeededToExtract: number;
    generalPurposeBitFlag: number;
    compressionMethod: number;
    lastModificationTime: number;
    lastModificationDate: number;
    crc32: number;
    compressedSize: number;
    uncompressedSize: number;
    fileNameLength: number;
    extraFieldLength: number;
};

export type ZipEocdRecord = {
    numberOfThisDisk: number;
    diskWhereCentralDirectoryStarts: number;
    numbersOfCentralDirectoryRecordsOnThisDisk: number;
    totalNumberOfCentralDirectoryRecords: number;
    sizeOfCentralDirectoryInBytes: number;
    offsetToStartCentralDirectory: number;
    commentLength: number;
};

export type Zip64LocatorRecord = {
    diskWithZip64Eocd: number;
    zip64EocdOffset: number;
    totalNumberOfDisks: number;
};

export type Zip64EocdRecord = {
    sizeOfZip64EocdRecord: number;
    versionMadeBy: number;
    versionNeededToExtract: number;
    diskNumber: number;
    diskWithCentralDirectoryStart: number;
    numberOfEntriesOnDisk: number;
    totalNumberOfEntries: number;
    sizeOfCentralDirectory: number;
    offsetToCentralDirectory: number;
};

export type ZipCdfhRecord = {
    versionMadeBy: number;
    minimumVersionNeededToExtract: number;
    bitFlag: number;
    compressionMethod: number;
    fileLastModificationTime: number;
    fileLastModificationDate: number;
    crc32OfUncompressedData: number;
    compressedSize: number;
    uncompressedSize: number;
    fileNameLength: number;
    extraFieldLength: number;
    fileCommentLength: number;
    diskNumberWhereFileStarts: number;
    internalFileAttributes: number;
    externalFileAttributes: number;
    offsetToLocalFileHeader: number;
    fileName: string;
    extraField: Uint8Array;
    fileComment: string;
    indexInArchive: number;
};

export class Zip {
    // Constants
    static readonly EOCD_SIGNATURE = 0x06054b50;
    static readonly EOCD_MINIMUM_SIZE = 22;
    static readonly EOCD_MAXIMUM_SIZE = 65557; // EOCD_MINIMUM_SIZE + maximum comment length (65535)
    static readonly ZIP64_LOCATOR_SIGNATURE = 0x07064b50;
    static readonly ZIP64_LOCATOR_SIZE = 20;
    static readonly ZIP64_EOCD_SIGNATURE = 0x06064b50;
    static readonly ZIP64_EOCD_MINIMUM_SIZE = 56;
    static readonly CDFH_SIGNATURE = 0x02014b50;
    static readonly CDFH_MINIMUM_SIZE = 46;
    static readonly LFH_SIGNATURE = 0x04034b50;
    static readonly LFH_MINIMUM_SIZE = 30;
    static readonly ZIP64_EXTRA_FIELD_ID = 0x0001;

    static async readZipFile(file: File): Promise<{ entries: ZipCdfhRecord[], isBroken: boolean }> {
        try {
            // First try without comment
            let eocdResult = await this.findEocdRecord(file, false);
            
            // If not found, try with maximum comment length
            if (!eocdResult.found) {
                eocdResult = await this.findEocdRecord(file, true);
            }

            if (!eocdResult.found) {
                return { entries: [], isBroken: true };
            }

            const { eocd, zip64Locator } = eocdResult;

            let finalEocd: {
                totalEntries: number;
                centralDirSize: number;
                centralDirOffset: number;
            };

            if (zip64Locator) {
                const zip64Eocd = await this.readZip64Eocd(
                    file, 
                    zip64Locator.zip64EocdOffset);
                
                if (!zip64Eocd) {
                    return { entries: [], isBroken: true };
                }

                finalEocd = {
                    totalEntries: zip64Eocd.totalNumberOfEntries,
                    centralDirSize: zip64Eocd.sizeOfCentralDirectory,
                    centralDirOffset: zip64Eocd.offsetToCentralDirectory
                };
            } else {
                finalEocd = {
                    totalEntries: eocd!.totalNumberOfCentralDirectoryRecords,
                    centralDirSize: eocd!.sizeOfCentralDirectoryInBytes,
                    centralDirOffset: eocd!.offsetToStartCentralDirectory
                };
            }

            const entries = await this.readCentralDirectory(
                file,
                finalEocd.centralDirOffset,
                finalEocd.centralDirSize
            );

            return { entries, isBroken: false };
        } catch (error) {
            console.error('Error reading ZIP file:', error);
            return { entries: [], isBroken: true };
        }
    }

    private static async findEocdRecord(file: File, withComment: boolean): Promise<{
        found: boolean;
        eocd?: ZipEocdRecord;
        zip64Locator?: Zip64LocatorRecord;
    }> {
        const fileSize = file.size;
        // Include ZIP64 locator size in the read size
        const readSize = Math.min(fileSize, (withComment ? this.EOCD_MAXIMUM_SIZE : this.EOCD_MINIMUM_SIZE) + this.ZIP64_LOCATOR_SIZE);
        const startPos = Math.max(0, fileSize - readSize);
        
        const buffer = await file.slice(startPos, fileSize).arrayBuffer();
        const view = new DataView(buffer);
        
        // Search for EOCD signature
        for (let i = buffer.byteLength - this.EOCD_MINIMUM_SIZE; i >= 0; i--) {
            if (view.getUint32(i, true) === this.EOCD_SIGNATURE) {
                const eocd = this.parseEocdRecord(view, i);
                
                // Now we can safely check for ZIP64 locator because we included its size in our read
                const locatorStart = i - this.ZIP64_LOCATOR_SIZE;
                let zip64Locator: Zip64LocatorRecord | undefined;
                
                // Make sure we're still within the buffer we read
                if (locatorStart >= 0 && 
                    view.getUint32(locatorStart, true) === this.ZIP64_LOCATOR_SIGNATURE) {
                    zip64Locator = this.parseZip64LocatorRecord(view, locatorStart);
                }
                
                return { found: true, eocd, zip64Locator };
            }
        }
        
        return { found: false };
    }

    private static parseEocdRecord(view: DataView, offset: number): ZipEocdRecord {
        offset += 4; // Skip signature
        return {
            numberOfThisDisk: view.getUint16(offset, true),
            diskWhereCentralDirectoryStarts: view.getUint16(offset + 2, true),
            numbersOfCentralDirectoryRecordsOnThisDisk: view.getUint16(offset + 4, true),
            totalNumberOfCentralDirectoryRecords: view.getUint16(offset + 6, true),
            sizeOfCentralDirectoryInBytes: view.getUint32(offset + 8, true),
            offsetToStartCentralDirectory: view.getUint32(offset + 12, true),
            commentLength: view.getUint16(offset + 16, true)
        };
    }

    private static parseZip64LocatorRecord(view: DataView, offset: number): Zip64LocatorRecord {
        offset += 4; // Skip signature
        return {
            diskWithZip64Eocd: view.getUint32(offset, true),
            zip64EocdOffset: Number(view.getBigUint64(offset + 4, true)),
            totalNumberOfDisks: view.getUint32(offset + 12, true)
        };
    }

    private static async readZip64Eocd(file: File, offset: number): Promise<Zip64EocdRecord | null> {
        try {
            const buffer = await file.slice(offset, offset + this.ZIP64_EOCD_MINIMUM_SIZE).arrayBuffer();
            const view = new DataView(buffer);

            if (view.getUint32(0, true) !== this.ZIP64_EOCD_SIGNATURE) {
                return null;
            }

            return {
                sizeOfZip64EocdRecord: Number(view.getBigUint64(4, true)),
                versionMadeBy: view.getUint16(12, true),
                versionNeededToExtract: view.getUint16(14, true),
                diskNumber: view.getUint32(16, true),
                diskWithCentralDirectoryStart: view.getUint32(20, true),
                numberOfEntriesOnDisk: Number(view.getBigUint64(24, true)),
                totalNumberOfEntries: Number(view.getBigUint64(32, true)),
                sizeOfCentralDirectory: Number(view.getBigUint64(40, true)),
                offsetToCentralDirectory: Number(view.getBigUint64(48, true))
            };
        } catch (error) {
            console.error('Error reading ZIP64 EOCD:', error);
            return null;
        }
    }

    private static async readCentralDirectory(
        file: File,
        offset: number,
        size: number
    ): Promise<ZipCdfhRecord[]> {
        const buffer = await file
            .slice(offset, offset + size)
            .arrayBuffer();

        const view = new DataView(buffer);
        const decoder = new TextDecoder();
        const entries: ZipCdfhRecord[] = [];

        let pos = 0;

        for (let i = 0; pos < buffer.byteLength; i++) {
            const record = await this.parseCdfhRecord(
                view, 
                pos,
                decoder);

            entries.push({ 
                ...record, 
                indexInArchive: i 
            });
            
            pos += this.CDFH_MINIMUM_SIZE + 
                   record.fileNameLength + 
                   record.extraFieldLength + 
                   record.fileCommentLength;
        }

        return entries.filter(e => e.compressedSize > 0);
    }

    private static async parseCdfhRecord(
        view: DataView, 
        offset: number,
        decoder: TextDecoder
    ): Promise<Omit<ZipCdfhRecord, 'indexInArchive'>> {
        const signature = view.getUint32(offset, true);
        offset += 4;

        if ( signature !== this.CDFH_SIGNATURE) {
            throw new Error('Invalid CDFH signature');
        }

        const versionMadeBy = view.getUint16(offset, true);
        offset += 2;
        
        const minimumVersionNeededToExtract = view.getUint16(offset, true);
        offset += 2;

        const bitFlag = view.getUint16(offset, true);
        offset += 2;

        const compressionMethod = view.getUint16(offset, true);
        offset += 2;

        const filetLastModificationTime = view.getUint16(offset, true);
        offset += 2;
        
        const fileLastModificationDate = view.getUint16(offset, true);
        offset += 2;
        
        const crc32OfUncompressedData = view.getUint32(offset, true);
        offset += 4;
        
        const compressedSize = view.getUint32(offset, true);
        offset += 4;
        
        const uncompressedSize = view.getUint32(offset, true);
        offset += 4;

        const fileNameLength = view.getUint16(offset, true);
        offset += 2;

        const extraFieldLength = view.getUint16(offset, true);
        offset += 2;
        
        const fileCommentLength = view.getUint16(offset, true);
        offset += 2;

        const diskNumberWhereFileStarts = view.getUint16(offset, true);
        offset += 2;
        
        const internalFileAttributes = view.getUint16(offset, true);
        offset += 2;

        const externalFileAttributes = view.getUint32(offset, true);
        offset += 4;
        
        const offsetToLocalFileHeader = view.getUint32(offset, true);
        offset += 4;

        const fileName = decoder.decode(
            new Uint8Array(view.buffer, view.byteOffset + offset, fileNameLength)
        );
        offset += fileNameLength;

        const extraField = new Uint8Array(view.buffer, view.byteOffset + offset, extraFieldLength);
        offset += extraFieldLength;

        const fileComment = decoder.decode(
            new Uint8Array(view.buffer, view.byteOffset + offset, fileCommentLength)
        );
        offset += fileCommentLength;

        // Read extra fields if ZIP64
        const needsZip64 = compressedSize === 0xFFFFFFFF || 
                          uncompressedSize === 0xFFFFFFFF || 
                          offsetToLocalFileHeader === 0xFFFFFFFF ||
                          diskNumberWhereFileStarts === 0xFFFF;

        let finalCompressedSize = compressedSize;
        let finalUncompressedSize = uncompressedSize;
        let finalOffset = offsetToLocalFileHeader;
        let finalDiskNumber = diskNumberWhereFileStarts;

        if (needsZip64) {
            const zip64Values = this.parseZip64ExtraField(
                extraField,
                compressedSize === 0xFFFFFFFF,
                uncompressedSize === 0xFFFFFFFF,
                offsetToLocalFileHeader === 0xFFFFFFFF,
                diskNumberWhereFileStarts === 0xFFFF
            );

            if (zip64Values) {
                finalCompressedSize = zip64Values.compressedSize ?? compressedSize;
                finalUncompressedSize = zip64Values.uncompressedSize ?? uncompressedSize;
                finalOffset = zip64Values.localHeaderOffset ?? offsetToLocalFileHeader;
                finalDiskNumber = zip64Values.diskStart ?? diskNumberWhereFileStarts;
            }
        }

        return {
            versionMadeBy: versionMadeBy,
            minimumVersionNeededToExtract: minimumVersionNeededToExtract,
            bitFlag: bitFlag,
            compressionMethod: compressionMethod,
            fileLastModificationTime: filetLastModificationTime,
            fileLastModificationDate: fileLastModificationDate,
            crc32OfUncompressedData: crc32OfUncompressedData,
            fileNameLength: fileNameLength,
            extraFieldLength: extraFieldLength,
            fileCommentLength: fileCommentLength,
            internalFileAttributes: internalFileAttributes,
            externalFileAttributes: externalFileAttributes,
            fileName: fileName,
            extraField: extraField,
            fileComment: fileComment,
        
            compressedSize: finalCompressedSize,
            uncompressedSize: finalUncompressedSize,
            offsetToLocalFileHeader: finalOffset,
            diskNumberWhereFileStarts: finalDiskNumber
        };
    }

    private static parseZip64ExtraField(
        extraField: Uint8Array,
        needsUncompressed: boolean,
        needsCompressed: boolean,
        needsOffset: boolean,
        needsDisk: boolean
    ) {
        // Create a DataView from the Uint8Array for binary reading
        // If the Uint8Array is a view into a larger buffer, we need to account for its offset
        const buffer = extraField.buffer.slice(
            extraField.byteOffset, 
            extraField.byteOffset + extraField.byteLength);

        const view = new DataView(buffer);
        let pos = 0;
        const end = extraField.byteLength;
    
        while (pos + 4 <= end) {
            const headerId = view.getUint16(pos, true);
            const dataSize = view.getUint16(pos + 2, true);
            pos += 4;
    
            // Check if we have enough data remaining
            if (pos + dataSize > end) {
                break;
            }
    
            if (headerId === this.ZIP64_EXTRA_FIELD_ID) {
                let valuePos = pos;
                let uncompressedSize: number | undefined;
                let compressedSize: number | undefined;
                let localHeaderOffset: number | undefined;
                let diskStart: number | undefined;
    
                // Ensure we have enough bytes for each field we need to read
                const remainingBytes = end - valuePos;
                
                if (needsUncompressed && remainingBytes >= 8) {
                    uncompressedSize = Number(view.getBigUint64(valuePos, true));
                    valuePos += 8;
                }
    
                if (needsCompressed && remainingBytes >= valuePos - pos + 8) {
                    compressedSize = Number(view.getBigUint64(valuePos, true));
                    valuePos += 8;
                }
    
                if (needsOffset && remainingBytes >= valuePos - pos + 8) {
                    localHeaderOffset = Number(view.getBigUint64(valuePos, true));
                    valuePos += 8;
                }
    
                if (needsDisk && remainingBytes >= valuePos - pos + 4) {
                    diskStart = view.getUint32(valuePos, true);
                }
    
                return {
                    uncompressedSize,
                    compressedSize,
                    localHeaderOffset,
                    diskStart
                };
            }
    
            pos += dataSize;
        }
    
        return null;
    }

    static async readLfhRecord(file: File, offset: number): Promise<ZipLfhRecord> {
        const buffer = await file.slice(offset, offset + this.LFH_MINIMUM_SIZE).arrayBuffer();
        const view = new DataView(buffer);

        // Read and verify signature
        const signature = view.getUint32(0, true);
        if (signature !== this.LFH_SIGNATURE) {
            throw new Error(`Invalid ZIP LFH signature: ${signature.toString(16)}`);
        }
        
        return {
            versionNeededToExtract: view.getUint16(4, true),
            generalPurposeBitFlag: view.getUint16(6, true),
            compressionMethod: view.getUint16(8, true),
            lastModificationTime: view.getUint16(10, true),
            lastModificationDate: view.getUint16(12, true),
            crc32: view.getUint32(14, true),
            compressedSize: view.getUint32(18, true),
            uncompressedSize: view.getUint32(22, true),
            fileNameLength: view.getUint16(26, true),
            extraFieldLength: view.getUint16(28, true)
        };
    }

    static async readLfhRecordFromBlob(blob: Blob): Promise<ZipLfhRecord> {
        const buffer = await blob.arrayBuffer();
        const view = new DataView(buffer);

        // Read and verify signature
        const signature = view.getUint32(0, true);
        if (signature !== this.LFH_SIGNATURE) {
            throw new Error(`Invalid ZIP LFH signature: ${signature.toString(16)}`);
        }
        
        return {
            versionNeededToExtract: view.getUint16(4, true),
            generalPurposeBitFlag: view.getUint16(6, true),
            compressionMethod: view.getUint16(8, true),
            lastModificationTime: view.getUint16(10, true),
            lastModificationDate: view.getUint16(12, true),
            crc32: view.getUint32(14, true),
            compressedSize: view.getUint32(18, true),
            uncompressedSize: view.getUint32(22, true),
            fileNameLength: view.getUint16(26, true),
            extraFieldLength: view.getUint16(28, true)
        };
    }
}

export type ZipArchive = {
    folders: ZipFolder[];
    entries: ZipEntry[];

    entriesMap: Map<number, ZipEntry>;
    foldersCount: number;
}

export type ZipFolder = {
    // Server-side virtual folder id (1-based, deterministic per CDFH walk). Kept on
    // the runtime tree so the UI can identify a folder when building bulk-download
    // selection payloads without re-deriving the id from path strings.
    virtualFolderId: number;
    name: string;
    folders: ZipFolder[];
    entries: ZipEntry[];
}

export type ZipVirtualFolder = {
    id: number;
    parentId: number | null;
    name: string;
};

export type ZipEntry = {
    fileName: string;
    virtualFolderId: number | null;
    compressedSizeInBytes: number;
    sizeInBytes: number;
    offsetToLocalFileHeader: number;
    fileNameLength: number;
    compressionMethod: number;
    indexInArchive: number;
};

type ZipNode = {
    folders: ZipFolder[];
    entries: ZipEntry[];
}

export class ZipArchives {
    public static getStructure(
        items: ZipEntry[],
        folders: ZipVirtualFolder[]
    ): ZipArchive {
        const archive: ZipArchive = {
            folders: [],
            entries: [],
            entriesMap: new Map<number, ZipEntry>(),
            foldersCount: folders.length
        };

        // Wire folders into the nested tree by walking the input in order. The
        // server emits a parent before any of its children (root-first traversal),
        // so by the time we encounter a child its parent is already in the map.
        const folderById = new Map<number, ZipFolder>();

        for (const virtualFolder of folders) {
            const folder: ZipFolder = {
                virtualFolderId: virtualFolder.id,
                name: virtualFolder.name,
                folders: [],
                entries: []
            };

            folderById.set(virtualFolder.id, folder);

            // proto3 wire format cannot distinguish "field absent" from "field = 0"
            // for scalar uint32: protobuf-net omits the tag when a nullable value is
            // null, and protobufjs surfaces a missing tag as the default value 0 on
            // decode. We sidestep the ambiguity by starting folder ids at 1 on the
            // server, which makes 0 a reserved "absent" marker on both ends.
            if (virtualFolder.parentId == null || virtualFolder.parentId === 0) {
                archive.folders.push(folder);
            } else {
                const parent = folderById.get(virtualFolder.parentId);

                if (!parent) {
                    throw new Error(
                        `Parent folder ${virtualFolder.parentId} not found while attaching ${virtualFolder.name}`);
                }

                parent.folders.push(folder);
            }
        }

        for (const item of items) {
            archive.entriesMap.set(item.indexInArchive, item);

            // Same proto3 absent-vs-zero collision as above — a root-level item is
            // serialized with VirtualFolderId = null (tag absent) and decodes as 0
            // here; treating 0 as "no folder" is safe because folder ids start at 1.
            if (item.virtualFolderId == null || item.virtualFolderId === 0) {
                archive.entries.push(item);
            } else {
                const folder = folderById.get(item.virtualFolderId);

                if (!folder) {
                    throw new Error(
                        `Folder ${item.virtualFolderId} not found while attaching entry ${item.fileName}`);
                }

                folder.entries.push(item);
            }
        }

        ZipArchives.sortNodeIterative(archive);

        return archive;
    }

    // JS port of the server-side ZipPreviewResponseBuilder. Walks raw CDFH
    // records in IndexInArchive order, assigning a virtual folder id the first
    // time a (parent, name) pair is encountered. Same deterministic algorithm
    // → same ids for the same zip bytes on both ends.
    public static fromCdfhRecords(
        records: ZipCdfhRecord[]
    ): { items: ZipEntry[]; folders: ZipVirtualFolder[] } {
        const items: ZipEntry[] = [];
        const folders: ZipVirtualFolder[] = [];
        const folderMap = new Map<string, number>();
        // Start at 1 to mirror the server: 0 is reserved as the "no folder" marker
        // because proto3 cannot distinguish absent from 0 on the wire. The local
        // CDFH path never crosses protobuf, but keeping the numbering identical
        // means the same zip produces the same ids regardless of the source.
        let nextFolderId = 1;

        for (const record of records) {
            if (record.uncompressedSize === 0)
                continue;

            // Zip spec allows messy fileName values — leading slash, double slashes,
            // trailing slash. Filtering empty segments collapses them in one shot;
            // the length guard drops degenerate "/" / "" entries entirely.
            const parts = record
                .fileName
                .split('/')
                .filter(segment => segment.length > 0);

            if (parts.length === 0)
                continue;

            const fileName = parts[parts.length - 1];
            let parentId: number | null = null;

            // Last segment is the file name itself — iterate only over the
            // preceding segments, which are the folder names along the path.
            for (let i = 0; i < parts.length - 1; i++) {
                const segment = parts[i];

                // (parentId, segment) — the same name can appear under different
                // parents (two "src" folders in different subtrees are different
                // folders), so the lookup key must include parentId.
                const key = `${parentId}|${segment}`;
                let id = folderMap.get(key);

                if (id === undefined) {
                    id = nextFolderId;
                    nextFolderId++;
                    folderMap.set(key, id);
                    folders.push({
                        id: id,
                        parentId: parentId,
                        name: segment
                    });
                }

                parentId = id;
            }

            items.push({
                fileName: fileName,
                virtualFolderId: parentId,
                compressedSizeInBytes: record.compressedSize,
                sizeInBytes: record.uncompressedSize,
                offsetToLocalFileHeader: record.offsetToLocalFileHeader,
                fileNameLength: record.fileNameLength,
                compressionMethod: record.compressionMethod,
                indexInArchive: record.indexInArchive
            });
        }

        return {
            items: items,
            folders: folders
        };
    }

    private static sortNodeIterative(root: ZipNode): void {
        const nodesToProcess: ZipNode[] = [root];

        while (nodesToProcess.length > 0) {
            const currentNode = nodesToProcess.pop()!;

            // Sort entries in current node
            currentNode.entries.sort(
                (a, b) => a.fileName.localeCompare(b.fileName));

            // Sort folders in current node
            currentNode.folders.sort(
                (a, b) => a.name.localeCompare(b.name));

            // Add all subfolders to the stack
            nodesToProcess.push(...currentNode.folders);
        }
    }

    public static getFileNameAndExtension(
        entry: ZipEntry
    ): { name: string; extension: string } {
        return toNameAndExtension(entry.fileName);
    }

    public static getFullName(entry: ZipEntry): string {
        return entry.fileName;
    }

    public static doesMatchSearchPhrase(
        entry: ZipEntry,
        searchPhraseLower: string
    ): boolean {
        return entry
            .fileName
            .toLowerCase()
            .includes(searchPhraseLower);
    }

    public static buildArchiveTree(archive: ZipArchive): StaticTreeNode[] {
        const rootLevel: StaticTreeNode[] = [];

        // Iterates top-down so that when a child node is built its parent is already
        // wired up — parent refs and isParentSelected/isParentExcluded recursion
        // therefore resolve without any second pass.
        type Frame = {
            children: StaticTreeNode[];
            parent: StaticFolderNode | null;
            source: { folders: ZipFolder[]; entries: ZipEntry[] };
        };

        const queue: Frame[] = [{
            children: rootLevel,
            parent: null,
            source: archive
        }];

        while (queue.length > 0) {
            const frame = queue.pop()!;

            for (const folder of frame.source.folders) {
                const folderNode = ZipArchives.makeFolderNode(folder, frame.parent);
                frame.children.push(folderNode);

                queue.push({
                    children: folderNode.children,
                    parent: folderNode,
                    source: folder
                });
            }

            for (const entry of frame.source.entries) {
                const fileNode = ZipArchives.makeFileNode(entry, frame.parent);
                frame.children.push(fileNode);
            }
        }

        return rootLevel;
    }

    private static makeFolderNode(folder: ZipFolder, parent: StaticFolderNode | null): StaticFolderNode {
        const isExpanded = signal(false);
        const wasRenderedMemory = { wasRendered: false };

        const wasRendered = computed(() => {
            if (wasRenderedMemory.wasRendered)
                return true;

            if (isExpanded()) {
                wasRenderedMemory.wasRendered = true;
                return true;
            }

            return false;
        });

        const isSelected = signal(false);
        const isExcluded = signal(false);

        const isParentSelected = computed(() =>
            parent ? (parent.isSelected() || parent.isParentSelected()) : false);

        const isParentExcluded = computed(() =>
            parent ? (parent.isExcluded() || parent.isParentExcluded()) : false);

        const children: StaticTreeNode[] = [];

        return {
            type: 'folder',
            id: `${folder.virtualFolderId}`,
            name: folder.name,
            nameLower: folder.name.toLowerCase(),

            isExpanded: isExpanded,
            wasRendered: wasRendered,
            wasLoaded: true,
            isVisible: signal(true),

            children: children,

            isSelected: isSelected,
            isExcluded: isExcluded,
            parent: parent,
            isParentSelected: isParentSelected,
            isParentExcluded: isParentExcluded,
            selectedDescendantsCount: computed(() => countSelectedDescendants(children))
        };
    }

    private static makeFileNode(entry: ZipEntry, parent: StaticFolderNode | null): StaticFileNode {
        const nameAndExt = ZipArchives.getFileNameAndExtension(entry);
        const fullName = `${nameAndExt.name}${nameAndExt.extension}`;

        const isSelected = signal(false);
        const isExcluded = signal(false);

        const isParentSelected = computed(() =>
            parent ? (parent.isSelected() || parent.isParentSelected()) : false);

        const isParentExcluded = computed(() =>
            parent ? (parent.isExcluded() || parent.isParentExcluded()) : false);

        return {
            type: 'file',
            id: `${entry.indexInArchive}`,

            fullName: fullName,
            extension: nameAndExt.extension,
            fullNameLower: fullName.toLowerCase(),
            sizeInBytes: entry.sizeInBytes,
            isVisible: signal(true),

            isSelected: isSelected,
            isExcluded: isExcluded,
            parent: parent,
            isParentSelected: isParentSelected,
            isParentExcluded: isParentExcluded
        };
    }
}