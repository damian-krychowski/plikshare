namespace PlikShare.Storages.Exceptions;

public class FileNotFoundInStorageException: Exception
{
    public FileNotFoundInStorageException()
    {
        
    }

    public FileNotFoundInStorageException(string message): base(message)
    {
        
    }
}