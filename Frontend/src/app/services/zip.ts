
//information stored inside zip LFH record should not be used to decode file size etc
//as they may be not reliable (sic!) - we should use Central Directory record for those
//the only information we MUST read from here is extraFieldLength, becuase it is not present

import { computed, signal } from "@angular/core";
import { toNameAndExtension } from "./filte-type";
import { getBase62Guid } from "./guid-base-62";
import { ZipTreeNode, ZipFolderNode, ZipFileNode } from "../shared/zip-file-tree-view/zip-file-tree-view.component";

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
    name: string;
    folders: ZipFolder[];
    entries: ZipEntry[];
}

export type ZipEntry = {
    filePath: string;
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
    public static getStructure(entries: ZipEntry[]): ZipArchive {
        const archive: ZipArchive = {
            folders: [],
            entries: [],
            entriesMap: new Map<number, ZipEntry>(),
            foldersCount: 0
        };

        for (let index = 0; index < entries.length; index++) {
            const entry = entries[index];

            archive.entriesMap.set(entry.indexInArchive, entry);

            const pathParts = entry.filePath.split('/');
            const fileLevel = pathParts.length - 1;

            let currentLevelNode: ZipNode = archive;

            for (let level = 0; level < pathParts.length; level++) {
                const levelName = pathParts[level];
                
                if(level < fileLevel) {
                    let folder: ZipFolder | undefined = currentLevelNode
                        .folders
                        .find(f => f.name === levelName);

                    if(!folder) {
                        let folderPath = '';

                        for(let i = 0; i < level; i++) {
                            folderPath += pathParts[i] + "/";
                        }

                        folderPath += levelName;

                        folder = {
                            name: levelName,
                            folders: [],
                            entries: []
                        };

                        archive.foldersCount += 1;
                        currentLevelNode.folders.push(folder);
                    }

                    currentLevelNode = folder;
                } else {
                    currentLevelNode.entries.push(entry);
                }
            }
        }

        ZipArchives.sortNodeIterative(archive);

        return archive;
    }

    private static sortNodeIterative(root: ZipNode): void {
        const nodesToProcess: ZipNode[] = [root];
        
        while (nodesToProcess.length > 0) {
            const currentNode = nodesToProcess.pop()!;
            
            // Sort entries in current node
            currentNode.entries.sort((a, b) => a.filePath.localeCompare(b.filePath));
            
            // Sort folders in current node
            currentNode.folders.sort((a, b) => a.name.localeCompare(b.name));
            
            // Add all subfolders to the stack
            nodesToProcess.push(...currentNode.folders);
        }
    }

    public static getFileNameAndExtension(entry: ZipEntry): {name: string; extension: string} {
        const fullName = ZipArchives.getFullName(
            entry);

        return toNameAndExtension(fullName);
    }

    public static getFullName(entry: ZipEntry): string {
        const pathParts = entry.filePath.split('/');
        return pathParts[pathParts.length - 1];
    }

    public static doesMatchSearchPhrase(entry: ZipEntry, searchPhraseLower: string): boolean {
        const fullFileName = ZipArchives.getFullName(entry);

        return fullFileName
            .toLowerCase()
            .includes(searchPhraseLower);
    }

    public static  buildArchiveTree(archive: ZipArchive): ZipTreeNode[] {
        const rootLevel: ZipTreeNode[] = [];
        const dummySignal = signal(false);
        
        type SourceNode = {
            folders: ZipFolder[];
            entries: ZipEntry[];
        }

        type NodeToProcess = {
            target: ZipFolderNode;
            source: SourceNode;
        }

        const nodesToProcess: NodeToProcess[] = [{
            target: {
                type: 'folder',
                id: '',

                name: "",
                children: rootLevel,

                isExpanded: dummySignal,
                wasRendered: dummySignal,
                isVisible: dummySignal,
                wasLoaded: true
            },
            source: archive 
        }];

        while(nodesToProcess.length > 0) {
            const currentNode = nodesToProcess.pop()!;
            const target = currentNode.target;

            for (const folder of currentNode.source.folders) {
                const isExpanded = signal(false);

                const wasRenderedMemory = {
                    wasRendered: false
                };
    
                const wasRendered = computed(() => {
                    if(wasRenderedMemory.wasRendered)
                        return true;
    
                    if(isExpanded()){
                        wasRenderedMemory.wasRendered = true;
                        return true;
                    }
    
                    return false;
                });

                const folderNode: ZipFolderNode = {
                    type: 'folder',
                    id: getBase62Guid(),
                    name: folder.name,
            
                    isExpanded: isExpanded,
                    wasRendered: wasRendered,      
                    wasLoaded: true,
                    isVisible: signal(true),

                    children: []                    
                };

                target.children.push(
                    folderNode);

                nodesToProcess.push({
                    target: folderNode,
                    source: folder
                });
            }

            for (const entry of currentNode.source.entries) {
                const nameAndExt = ZipArchives.getFileNameAndExtension(
                    entry);

                const fullName = `${nameAndExt.name}${nameAndExt.extension}`;

                const zipFile: ZipFileNode = {
                    type: 'file',         
                    id: `${entry.indexInArchive}`,

                    fullName: fullName,
                    extension: nameAndExt.extension,
                    fullNameLower: fullName.toLowerCase(),                    
                    sizeInBytes: entry.sizeInBytes,
                    isVisible: signal(true),
                };

                target.children.push(zipFile);
            }
        }
        
        return rootLevel;
    }
}