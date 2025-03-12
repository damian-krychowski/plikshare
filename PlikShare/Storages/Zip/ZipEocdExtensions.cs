namespace PlikShare.Storages.Zip;

public static class ZipEocdExtensions
{
    public static bool HasZip64Markers(this ZipEocdRecord eocd)
    {
        return eocd.NumberOfThisDisk == 0xFFFF
               || eocd.DiskWhereCentralDirectoryStarts == 0xFFFF
               || eocd.NumbersOfCentralDirectoryRecordsOnThisDisk == 0xFFFF
               || eocd.TotalNumberOfCentralDirectoryRecords == 0xFFFF
               || eocd.SizeOfCentralDirectoryInBytes == 0xFFFFFFFF
               || eocd.OffsetToStartCentralDirectory == 0xFFFFFFFF;
    }
}