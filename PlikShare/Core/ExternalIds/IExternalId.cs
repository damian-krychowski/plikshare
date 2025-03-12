namespace PlikShare.Core.ExternalIds;

public interface IExternalId<T>: IParsable<T> where T: IParsable<T>
{
    public string Value { get; }
}
